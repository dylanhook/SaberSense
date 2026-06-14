// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Utilities;
using UnityEngine;

namespace SaberSense.Gameplay;

internal sealed class ProceduralMaterialFactory : System.IDisposable
{
    private const int SoftTextureSize = 64;
    private const float GaussianFalloff = 3f;
    private const float FadeMode = 2f;

    private Material? _softMaterial;
    private Material? _lineMaterial;
    private Texture2D? _softTexture;
    private readonly ShaderRegistry _shaders;

    public ProceduralMaterialFactory(ShaderRegistry shaders)
    {
        _shaders = shaders;
    }

    public Material GetSoftParticleMaterial()
    {
        if (_softMaterial != null) return _softMaterial;

        _softMaterial = new Material(_shaders.ParticleWithFallback);

        const int size = SoftTextureSize;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size / 2f;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                float alpha = Mathf.Clamp01(Mathf.Exp(-GaussianFalloff * dist * dist));
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        _softTexture = tex;
        _softMaterial.mainTexture = _softTexture;
        _softMaterial.color = Color.white;

        if (_softMaterial.HasProperty("_Mode"))
        _softMaterial.SetFloat("_Mode", FadeMode);

        return _softMaterial;
    }

    public Material GetLineMaterial()
    {
        if (_lineMaterial != null) return _lineMaterial;

        _lineMaterial = new Material(_shaders.UnlitWithFallback);
        _lineMaterial.color = Color.white;

        if (_lineMaterial.HasProperty("_Mode"))
        _lineMaterial.SetFloat("_Mode", FadeMode);

        return _lineMaterial;
    }

    public void Dispose()
    {
        if (_softTexture != null) { Object.Destroy(_softTexture); _softTexture = null; }
        if (_softMaterial != null) { Object.Destroy(_softMaterial); _softMaterial = null; }
        if (_lineMaterial != null) { Object.Destroy(_lineMaterial); _lineMaterial = null; }
    }
}