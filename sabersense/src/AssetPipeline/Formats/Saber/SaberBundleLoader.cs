// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.App;
using SaberSense.AssetPipeline.Assembly;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Core.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.AssetPipeline.Formats.Saber;

internal static class SaberBundleFormat
{
    public const string RootPrefabName = "_CustomSaber";
}

internal sealed class SaberBundleLoader(SaberBundleParser parser, IMessageBroker broker, IModLogger log) : ISaberLoader
{
    private readonly IModLogger _log = log.ForSource(nameof(SaberBundleLoader));

    public string HandledExtension => ".saber";

    public IAsyncEnumerable<SaberRoute> DiscoverAsync(AppPaths dirs)
    => SaberRouteDiscovery.ByExtension(dirs, "*.saber");

    public async Task<LoadedBundle?> LoadAsync(string relativePath)
    {
        var fullPath = AssetPaths.ResolveFull(relativePath);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        broker.Publish(new SaberLoadProgressMsg("Parsing saber data...", 0.05f));
        var parseResult = await Task.Run(() => parser.Parse(fullPath));

        var materialScope = new MaterialSnapshotScope();

        broker.Publish(new SaberLoadProgressMsg("Loading asset bundle...", 0.15f));
        var result = await BundleLoader.LoadFromFileAsync<GameObject>(
        fullPath, SaberBundleFormat.RootPrefabName,
        (loaded, total) => broker.Publish(new SaberLoadProgressMsg(
        $"Loading assets [{loaded}/{total}]", 0.15f + (0.25f * loaded / total))));
        if (result is null)
        {
            return null;
        }

        var bundleOwnedMaterials = materialScope.GetNewMaterials();

        await Task.Yield();

        broker.Publish(new SaberLoadProgressMsg("Injecting components...", 0.40f));
        if (parseResult is not null)
        {
            var coverSprite = TryCreateCoverFromParsedData(parseResult.CoverImage)
            ?? TryLoadCoverSprite(result.Value.Bundle);

            var metadata = parseResult.Metadata;
            if (coverSprite != null && metadata.CoverImage == null)
            {
                metadata = new(metadata.Name, metadata.Author, metadata.Description, coverSprite);
            }

            PrefabComponentInjector.InjectDescriptor(result.Value.Asset, metadata);
            PrefabComponentInjector.InjectTrails(result.Value.Asset, parseResult.Trails, parseResult, result.Value.Bundle, bundleOwnedMaterials);

            if (parseResult.Modifiers.Count is > 0)
            PrefabComponentInjector.InjectModifiers(result.Value.Asset, parseResult.Modifiers, parseResult);

            if (parseResult.SpringBones.Count is > 0 || parseResult.SpringColliders.Count is > 0)
            PrefabComponentInjector.InjectSpringBones(result.Value.Asset, parseResult);

            PrefabComponentInjector.MirrorAnimations(result.Value.Asset);
        }

        await Task.Yield();

        broker.Publish(new SaberLoadProgressMsg("Repairing shaders...", 0.50f));
        var spiCompatible = await ShaderBindingFixer.FixAsync(result.Value.Asset);

        broker.Publish(new SaberLoadProgressMsg("Finalizing...", 0.65f));
        var contentHash = await Task.Run(() => ContentHasher.TryCompute(fullPath));

        var bundle = new LoadedBundle(relativePath, result.Value.Asset, result.Value.Bundle)
        {
            IsSPICompatible = spiCompatible,
            ParsedBounds = parseResult?.ParsedBounds,
            ContentHash = contentHash,
            ParseResult = parseResult?.HasEvents == true ? parseResult : null
        };

        broker.Publish(new SaberLoadCompletedMsg());
        return bundle;
    }

    private static Sprite? TryCreateCoverFromParsedData(CoverImageData? coverData)
    => coverData is not null ? SpriteFactory.FromRawGPU(coverData) : null;

    private static Sprite? TryLoadCoverSprite(AssetBundle? bundle)
    {
        if (bundle == null) return null;

        try
        {
            var sprites = bundle.LoadAllAssets<Sprite>();
            if (sprites is { Length: > 0 })
            return sprites[0];

            var textures = bundle.LoadAllAssets<Texture2D>();
            if (textures is not null)
            {
                foreach (var tex in textures)
                {
                    if (tex.width >= 32 && tex.height >= 32 && tex.width <= 1024 && tex.height <= 1024)
                    {
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f), 100f);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ModLogger.ForSource("SaberBundleLoader").Warn($"Failed to load cover sprite: {ex.Message}");
        }

        return null;
    }

    public async Task<PreviewData?> ExtractPreviewAsync(string relativePath)
    {
        var fullPath = AssetPaths.ResolveFull(relativePath);
        if (!File.Exists(fullPath))
        return null;

        try
        {
            var (previewResult, contentHash, fileSize, lastModified) = await Task.Run(() =>
            {
                var result = parser.ParsePreviewOnly(fullPath);
                var hash = result is not null ? ContentHasher.TryCompute(fullPath) : null;
                var info = new FileInfo(fullPath);
                return (result, hash, info.Length, info.LastWriteTimeUtc.ToString("O"));
            });

            if (previewResult is null)
            return null;

            var (metadata, coverData) = previewResult.Value;

            var coverSprite = coverData is not null ? SpriteFactory.FromRawGPU(coverData) : null;

            return new PreviewData(
            metadata.Name ?? "Custom Saber",
            metadata.Author ?? "Unknown",
            coverSprite,
            true,
            fileSize,
            lastModified,
            contentHash);
        }
        catch (Exception ex)
        {
            _log?.Warn($"Preview extraction failed for {relativePath}: {ex.Message}");
            return null;
        }
    }
}