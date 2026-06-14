// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.App;
using SaberSense.AssetPipeline.Assembly;
using SaberSense.AssetPipeline.Formats.Saber;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using SaberSense.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.AssetPipeline.Formats.Whacker;

internal sealed class WhackerBundleLoader : ISaberLoader
{
    private readonly IModLogger _log;

    public WhackerBundleLoader(IModLogger log)
    {
        _log = log.ForSource(nameof(WhackerBundleLoader));
    }

    public string HandledExtension => ".whacker";

    public IAsyncEnumerable<SaberRoute> DiscoverAsync(AppPaths dirs)
    => SaberRouteDiscovery.ByExtension(dirs, "*.whacker");

    public async Task<LoadedBundle?> LoadAsync(string relativePath)
    {
        var fullPath = AssetPaths.ResolveFull(relativePath);
        if (!File.Exists(fullPath)) return null;

        using var fileStream = File.OpenRead(fullPath);
        using var archive = new System.IO.Compression.ZipArchive(fileStream, System.IO.Compression.ZipArchiveMode.Read);

        var manifest = ParseManifest(archive);
        if (manifest is null) return null;

        if (string.IsNullOrEmpty(manifest.BundleFileName))
        {
            _log.Error($"Failed to load whacker {fullPath}: fileName is missing from JSON descriptor.");
            return null;
        }

        var bundleEntry = archive.GetEntry(manifest.BundleFileName!);
        if (bundleEntry is null)
        {
            _log.Error($"Failed to load whacker {fullPath}: AssetBundle '{manifest.BundleFileName}' not found in ZIP.");
            return null;
        }

        byte[] bundleBytes;
        using (var bundleStream = bundleEntry.Open())
        using (var ms = new MemoryStream())
        {
            await bundleStream.CopyToAsync(ms);
            bundleBytes = ms.ToArray();
        }

        var bundleResult = await BundleLoader.LoadFromBytesAsync<GameObject>(bundleBytes, "_Whacker");
        if (bundleResult is null) return null;

        var saberPrefab = bundleResult.Value.Asset;
        var assetBundle = bundleResult.Value.Bundle;

        var saberDesc = saberPrefab.AddComponent<SaberDescriptor>();
        saberDesc.SaberName = manifest.Name;
        saberDesc.AuthorName = manifest.Author;

        if (!string.IsNullOrEmpty(manifest.IconName))
        {
            var iconEntry = archive.GetEntry(manifest.IconName);
            if (iconEntry is not null)
            {
                byte[] iconBytes;
                using (var iconStream = iconEntry.Open())
                using (var ms = new MemoryStream())
                {
                    await iconStream.CopyToAsync(ms);
                    iconBytes = ms.ToArray();
                }

                saberDesc.CoverImage = SpriteFactory.FromEncodedBytes(iconBytes);
            }
        }

        ConvertWhackerTrails(saberPrefab);

        var spiCompatible = await ShaderBindingFixer.FixAsync(saberPrefab);
        var loadedBundle = new LoadedBundle(relativePath, saberPrefab, assetBundle);
        loadedBundle.IsSPICompatible = spiCompatible;

        loadedBundle.ContentHash = await Task.Run(() => ContentHasher.TryCompute(fullPath));

        return loadedBundle;
    }

    public async Task<PreviewData?> ExtractPreviewAsync(string relativePath)
    {
        var fullPath = AssetPaths.ResolveFull(relativePath);
        if (!File.Exists(fullPath))
        return null;

        try
        {
            var (manifest, iconBytes, contentHash, fileSize, lastModified) = await Task.Run(() =>
            {
                using var fileStream = File.OpenRead(fullPath);
                using var archive = new System.IO.Compression.ZipArchive(fileStream, System.IO.Compression.ZipArchiveMode.Read);

                var m = ParseManifest(archive);
                if (m is null) return (default(WhackerManifest), default(byte[]), default(string), 0L, "");

                byte[]? icon = null;
                if (!string.IsNullOrEmpty(m.IconName))
                {
                    var iconEntry = archive.GetEntry(m.IconName);
                    if (iconEntry is not null)
                    {
                        using var iconStream = iconEntry.Open();
                        using var ms = new MemoryStream();
                        iconStream.CopyTo(ms);
                        icon = ms.ToArray();
                    }
                }

                var hash = ContentHasher.TryCompute(fullPath);
                var info = new FileInfo(fullPath);
                return (m, icon, hash, info.Length, info.LastWriteTimeUtc.ToString("O"));
            });

            if (manifest is null) return null;

            var coverSprite = iconBytes is not null ? SpriteFactory.FromEncodedBytes(iconBytes) : null;

            return new PreviewData(
            manifest.Name, manifest.Author, coverSprite, true,
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

    private sealed record WhackerManifest(
    string Name, string Author, string? IconName, string? BundleFileName);

    private WhackerManifest? ParseManifest(System.IO.Compression.ZipArchive archive)
    {
        var jsonEntry = archive.Entries.FirstOrDefault(
        x => x.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase));
        if (jsonEntry is null) return null;

        string jsonStr;
        using (var sr = new StreamReader(jsonEntry.Open()))
        jsonStr = sr.ReadToEnd();

        var metaData = Newtonsoft.Json.Linq.JObject.Parse(jsonStr);
        var descriptor = metaData.GetValue("descriptor", StringComparison.OrdinalIgnoreCase)
        ?? metaData.GetValue("Descriptor", StringComparison.OrdinalIgnoreCase);

        return new(
        Name: GetJsonString(descriptor, "objectName", "name") ?? "Unknown Whacker",
        Author: GetJsonString(descriptor, "author", "authorName") ?? "Unknown",
        IconName: GetJsonString(descriptor, "coverImage", "iconFileName")
        ?? GetJsonString(metaData, "coverImage", "iconFileName"),
        BundleFileName: GetJsonString(metaData, "pcFileName", "fileName"));
    }

    private static string? GetJsonString(Newtonsoft.Json.Linq.JToken? token, params string[] keys)
    {
        if (token is Newtonsoft.Json.Linq.JObject obj)
        {
            foreach (var k in keys)
            {
                var val = obj.GetValue(k, StringComparison.OrdinalIgnoreCase);
                if (val is not null) return val.ToString();
            }
        }
        return null;
    }

    private void ConvertWhackerTrails(GameObject root)
    {
        var textProps = root.GetComponentsInChildren<UnityEngine.UI.Text>(true);
        var transformDict = new Dictionary<int, (Transform? Top, Transform? Bottom)>();
        var customColors = new List<(int Id, Material? Mat, Color TColor, Color MColor, TrailColorMode CType, int Length)>();

        foreach (var t in textProps)
        {
            if (t.text.Contains("\"isTop\":"))
            {
                try
                {
                    var data = Newtonsoft.Json.Linq.JObject.Parse(t.text);
                    int id = (int?)data.GetValue("trailId", StringComparison.OrdinalIgnoreCase) ?? 0;
                    bool isTop = (bool?)data.GetValue("isTop", StringComparison.OrdinalIgnoreCase) ?? false;
                    transformDict.TryAdd(id, (null, null));

                    var pair = transformDict[id];
                    if (isTop) pair.Top = t.transform;
                    else pair.Bottom = t.transform;
                    transformDict[id] = pair;
                }
                catch (Exception ex) { _log.Warn($"Error reading whacker entry: {ex.Message}"); }
            }
            else if (t.text.Contains("\"trailColor\":"))
            {
                try
                {
                    var data = Newtonsoft.Json.Linq.JObject.Parse(t.text);
                    int id = (int?)data.GetValue("trailId", StringComparison.OrdinalIgnoreCase) ?? 0;
                    int length = (int?)data.GetValue("length", StringComparison.OrdinalIgnoreCase) ?? 14;
                    var cType = (TrailColorMode)((int?)data.GetValue("TrailColorMode", StringComparison.OrdinalIgnoreCase) ?? 0);

                    Color ReadColor(Newtonsoft.Json.Linq.JToken token) =>
                    new Color((float)token["r"]!, (float)token["g"]!, (float)token["b"]!, (float)token["a"]!);

                    var tToken = data.GetValue("trailColor", StringComparison.OrdinalIgnoreCase);
                    var tColor = tToken is not null ? ReadColor(tToken) : Color.white;

                    var mToken = data.GetValue("multiplierColor", StringComparison.OrdinalIgnoreCase);
                    var mColor = mToken is not null ? ReadColor(mToken) : Color.white;

                    var mat = t.GetComponent<MeshRenderer>()?.sharedMaterial;

                    customColors.Add((id, mat, tColor, mColor, cType, length));
                }
                catch (Exception ex) { _log.Warn($"Error reading whacker entry: {ex.Message}"); }
            }
        }

        foreach (var colorEntry in customColors)
        {
            if (!transformDict.TryGetValue(colorEntry.Id, out var points)) continue;
            if (points.Top == null || points.Bottom == null) continue;

            var go = new GameObject($"Trail_{colorEntry.Id}");
            go.transform.SetParent(points.Bottom.parent ?? root.transform, false);
            var marker = go.AddComponent<SaberTrailMarker>();

            marker.PointStart = points.Bottom;
            marker.PointEnd = points.Top;
            marker.TrailMaterial = colorEntry.Mat;
            marker.TrailColor = colorEntry.TColor;
            marker.MultiplierColor = colorEntry.MColor;
            marker.ColorMode = colorEntry.CType;
            marker.Length = colorEntry.Length;
        }

        foreach (var t in textProps)
        {
            UnityEngine.Object.Destroy(t);
        }
    }
}