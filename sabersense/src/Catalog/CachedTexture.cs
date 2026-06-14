// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.AssetPipeline.Assembly;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SaberSense.Catalog;

internal sealed class CachedTexture : IDisposable
{
    public Sprite Sprite => _sprite ??= GenerateSprite();

    public bool IsActive { get; internal set; }
    public string Identifier { get; internal set; }
    public AssetSource Source { get; internal set; }
    public string AbsolutePath { get; internal set; }
    public Texture2D Texture { get; internal set; }

    private Sprite? _sprite;

    public CachedTexture(string identifier, string absolutePath, Texture2D texture, AssetSource source)
    {
        Identifier = identifier;
        AbsolutePath = absolutePath;
        Texture = texture;
        Source = source;
        Texture.wrapMode = ResolveWrapMode(identifier, texture);
    }

    private static TextureWrapMode ResolveWrapMode(string name, Texture2D tex)
    {
        if (tex.wrapMode == TextureWrapMode.Clamp)
        return TextureWrapMode.Clamp;

        if (name.IndexOf("_clamp", StringComparison.OrdinalIgnoreCase) is >= 0)
        return TextureWrapMode.Clamp;

        return tex.wrapMode;
    }

    public void Dispose()
    {
        if (Texture) Object.Destroy(Texture);
        if (_sprite) Object.Destroy(_sprite);
    }

    private Sprite GenerateSprite() => SpriteFactory.FromTexture(Texture)!;

    public void DisposeIfUnused()
    {
        if (!IsActive) Dispose();
    }
}