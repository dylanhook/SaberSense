// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.AssetPipeline.Assembly;
using SaberSense.Catalog.Model;
using SaberSense.Core.Logging;
using System;
using UnityEngine;

namespace SaberSense.Catalog;

public sealed class AssetPreview : ISaberListItem, IDisposable
{
    public string DisplayName { get; private set; } = "";
    public string CreatorName { get; private set; } = "";
    public bool IsPinned { get; set; }
    public bool IsSPICompatible { get; set; } = true;
    public int MetaVersion { get; private set; }
    public long FileSize { get; private set; }
    public long FileLastModifiedTicks { get; private set; }

    public string? ContentHash { get; private set; }

    public string RelativePath { get; internal set; } = "";

    public string SubFolder => Core.Utilities.AssetPaths.GetSubfolderPath(RelativePath);

    public Texture2D? CoverTexture => _thumbTex ??= DecodeCoverTexture();

    public Sprite? CoverSprite => _thumbnail ??= CreateCoverSprite();

    public Sprite? CoverImage => CoverSprite;

    private byte[]? _coverBytes;
    private Sprite? _thumbnail;
    private Texture2D? _thumbTex;

    internal AssetPreview(PreviewRow row)
    {
        RelativePath = row.RelativePath;
        DisplayName = row.DisplayName;
        CreatorName = row.CreatorName;
        _coverBytes = row.CoverBytes;
        IsSPICompatible = row.IsSPICompatible;
        MetaVersion = row.MetaVersion;
        FileSize = row.FileSize;
        FileLastModifiedTicks = row.FileLastModifiedTicks;
        ContentHash = row.ContentHash;
    }

    internal AssetPreview(string relativePath, ISaberListItem displayable)
    {
        RelativePath = relativePath;
        DisplayName = displayable.DisplayName;
        CreatorName = displayable.CreatorName;
        IsSPICompatible = displayable.IsSPICompatible;
        ContentHash = (displayable as SaberAssetEntry)?.LeftPiece?.Asset?.ContentHash;
        _thumbnail = displayable.CoverImage;
    }

    internal AssetPreview(string relativePath, SaberSense.AssetPipeline.PreviewData data)
    {
        RelativePath = relativePath;
        DisplayName = data.DisplayName;
        CreatorName = data.CreatorName;
        IsSPICompatible = data.IsSPICompatible;
        ContentHash = data.ContentHash;
        _thumbnail = data.CoverSprite;
        FileSize = data.FileSize;
        FileLastModifiedTicks = DateTime.TryParse(data.FileLastModified,
        System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.RoundtripKind,
        out var dt) ? dt.Ticks : 0;
    }

    public PreviewRow ToRow()
    {
        byte[]? coverData = _coverBytes;
        if (coverData is null && _thumbnail != null && _thumbnail.texture != null)
        {
            try { coverData = CaptureTextureBytes(_thumbnail.texture); }
            catch (Exception ex) { ModLogger.ForSource("AssetPreview").Debug($"Cover capture failed (will retry on next save): {ex.Message}"); }
        }

        return new PreviewRow
        {
            RelativePath = RelativePath,
            DisplayName = DisplayName,
            CreatorName = CreatorName,
            CoverBytes = coverData,
            IsSPICompatible = IsSPICompatible,
            MetaVersion = PreviewDatabase.CurrentMetaVersion,
            LastModified = DateTime.UtcNow.ToString("O"),
            FileSize = FileSize,
            FileLastModifiedTicks = FileLastModifiedTicks,
            ContentHash = ContentHash
        };
    }

    public void SetPinned(bool state) => IsPinned = state;

    public void SetGeneratedCover(Sprite sprite) => _thumbnail = sprite;

    internal void InjectCoverBytes(byte[] pngBytes)
    {
        if (pngBytes is null || pngBytes.Length == 0) return;
        _coverBytes = pngBytes;

        if (_thumbnail != null) { UnityEngine.Object.Destroy(_thumbnail); _thumbnail = null; }
        if (_thumbTex != null) { UnityEngine.Object.Destroy(_thumbTex); _thumbTex = null; }
    }

    public void Dispose()
    {
        if (_thumbnail != null) { UnityEngine.Object.Destroy(_thumbnail); _thumbnail = null; }
        if (_thumbTex != null) { UnityEngine.Object.Destroy(_thumbTex); _thumbTex = null; }
        _coverBytes = null;
    }

    private byte[]? CaptureTextureBytes(Texture2D source)
    {
        if (source == null) return null;
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0);
        var prev = RenderTexture.active;
        Texture2D? readable = null;
        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            readable = new Texture2D(source.width, source.height);
            readable.ReadPixels(new(0, 0, rt.width, rt.height), 0, 0);
            readable.Apply();
            return readable.EncodeToPNG();
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            if (readable != null) UnityEngine.Object.Destroy(readable);
        }
    }

    private Texture2D? DecodeCoverTexture()
    => _coverBytes is not null ? SpriteFactory.LoadTexture(_coverBytes) : null;

    private Sprite? CreateCoverSprite()
    {
        if (CoverTexture == null) return null;
        return Sprite.Create(CoverTexture,
        new(0, 0, CoverTexture.width, CoverTexture.height),
        new(0.5f, 0.5f), 100f);
    }
}