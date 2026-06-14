// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.AssetPipeline.Formats.Saber;
using SaberSense.Core.Logging;
using UnityEngine;

namespace SaberSense.AssetPipeline.Assembly;

internal static class SpriteFactory
{
    private static readonly Vector2 CenterPivot = new(0.5f, 0.5f);
    private const float PixelsPerUnit = 100f;

    public static Sprite? FromRawGPU(CoverImageData coverData)
    {
        if (coverData is null) return null;

        Texture2D? tex = null;
        try
        {
            var format = (TextureFormat)coverData.Format;
            tex = new Texture2D(coverData.Width, coverData.Height, format, mipChain: false);
            tex.LoadRawTextureData(coverData.RawData);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            return Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height),
            CenterPivot, PixelsPerUnit);
        }
        catch (System.Exception ex)
        {
            if (tex != null) UnityEngine.Object.Destroy(tex);
            ModLogger.ForSource("SpriteFactory").Warn($"Failed to create sprite from raw GPU data: {ex.Message}");
            return null;
        }
    }

    public static Sprite? FromEncodedBytes(byte[] imageBytes)
    {
        if (imageBytes is null || imageBytes.Length is 0) return null;

        Texture2D? tex = null;
        try
        {
            tex = new Texture2D(2, 2);
            tex.LoadImage(imageBytes);

            return Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height),
            CenterPivot, PixelsPerUnit);
        }
        catch (System.Exception ex)
        {
            if (tex != null) UnityEngine.Object.Destroy(tex);
            ModLogger.ForSource("SpriteFactory").Warn($"Failed to create sprite from encoded bytes: {ex.Message}");
            return null;
        }
    }

    public static Sprite? FromTexture(Texture2D texture)
    {
        if (texture == null) return null;

        return Sprite.Create(texture,
        new Rect(0, 0, texture.width, texture.height),
        CenterPivot, PixelsPerUnit);
    }

    public static Texture2D? LoadTexture(byte[] bytes)
    {
        if (bytes is null || bytes.Length is 0) return null;
        var tex = new Texture2D(2, 2);
        if (ImageConversion.LoadImage(tex, bytes))
        return tex;
        UnityEngine.Object.Destroy(tex);
        return null;
    }
}