// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace SaberSense.Core.Utilities;

internal static class ShaderUtils
{
    internal static readonly int TintColorId = Shader.PropertyToID("_Color");
    internal static readonly int BaseTextureId = Shader.PropertyToID("_MainTex");
    internal static readonly int CustomColorToggleId = Shader.PropertyToID("_CustomColors");
    internal static readonly int GlowIntensityId = Shader.PropertyToID("_Glow");
    internal static readonly int BloomIntensityId = Shader.PropertyToID("_Bloom");
    internal static readonly int LeftHandColorId = Shader.PropertyToID("_UserColorLeft");
    internal static readonly int RightHandColorId = Shader.PropertyToID("_UserColorRight");
    internal static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
    internal static readonly int ZTestId = Shader.PropertyToID("_ZTest");

    [System.ThreadStatic]
    private static MaterialPropertyBlock? _colorBlock;

    public static bool TryGetTexture(this Material material, int propId, out Texture? tex)
    {
        tex = material.HasProperty(propId) ? material.GetTexture(propId) : null;
        return tex != null;
    }

    public static bool TryGetMainTexture(this Material material, [NotNullWhen(true)] out Texture? tex) =>
    TryGetTexture(material, ShaderUtils.BaseTextureId, out tex);

    public static bool TryGetFloat(this Material material, int propId, out float val)
    {
        if (material.HasProperty(propId))
        {
            val = material.GetFloat(propId);
            return true;
        }
        val = 0f;
        return false;
    }

    public static bool SupportsSaberColoring(Material material)
    {
        if (material is null || !material.HasProperty(TintColorId)) return false;

        if (material.HasProperty(CustomColorToggleId))
        return material.GetFloat(CustomColorToggleId) > 0;

        return (material.HasProperty(GlowIntensityId) && material.GetFloat(GlowIntensityId) > 0)
        || (material.HasProperty(BloomIntensityId) && material.GetFloat(BloomIntensityId) > 0);
    }

    public static MaterialPropertyBlock ColorBlock(Color color)
    {
        _colorBlock ??= new();
        _colorBlock.SetColor(ShaderUtils.TintColorId, color);
        return _colorBlock;
    }
}