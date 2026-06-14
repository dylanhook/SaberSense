// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Utilities;
using System.Linq;
using UnityEngine;

namespace SaberSense.GUI.Framework.Core;

public static class UIMaterials
{
    private static Material? _noBloomMaterial;
    private static ShaderRegistry? _shaders;

    internal static void Initialize(ShaderRegistry shaders)
    {
        _shaders = shaders;
    }

    public static Material NoBloomMaterial
    {
        get
        {
            if (_noBloomMaterial == null)
            {
                var found = Resources.FindObjectsOfTypeAll<Material>()
                .FirstOrDefault(m => m.name == "UINoGlow");

                _noBloomMaterial = found != null ? new Material(found) : CreateNoGlowMaterial();
                _noBloomMaterial.name = "SS_UINoGlow";

                Object.DontDestroyOnLoad(_noBloomMaterial);
            }
            return _noBloomMaterial;
        }
    }

    private static Material CreateNoGlowMaterial()
    {
        var shader = _shaders?.UINoGlowWithFallback ?? Shader.Find("UI/Default");

        var mat = new Material(shader);
        mat.name = "SS_UINoGlow";

        mat.SetInt("_ColorMask", 7);

        return mat;
    }
}