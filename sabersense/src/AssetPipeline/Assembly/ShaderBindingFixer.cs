// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using AssetBundleLoadingTools.Utilities;
using SaberSense.Core.Logging;
using SaberSense.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.AssetPipeline.Assembly;

internal static class ShaderBindingFixer
{
    private static readonly System.Lazy<System.Reflection.MethodInfo?> _spiCheckMethod = new(() =>
    {
        try
        {
            var shaderReaderType = System.Type.GetType(
            "AssetBundleLoadingTools.Utilities.ShaderReader, AssetBundleLoadingTools");
            return shaderReaderType?.GetMethod(
            "IsSinglePassInstancedSupported",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        }
        catch (Exception ex)
        {
            ModLogger.ForSource("ShaderFixer").Warn($"Error resolving SPI check method: {ex.Message}");
            return null;
        }
    });

    private const int ShaderBatchSize = 5;

    public static async Task<bool> FixAsync(GameObject saberObject)
    {
        bool spiCompatible = true;
        try
        {
            var materials = ShaderRepair.GetMaterialsFromGameObjectRenderers(saberObject);
            var trailMaterials = saberObject.GetComponentsInChildren<SaberTrailMarker>(true)
            .Select(t => t.TrailMaterial)
            .Where(m => m != null && !materials.Contains(m))!;
            materials.AddRange(trailMaterials!);

            if (_spiCheckMethod.Value is not null)
            {
                var uniqueShaders = materials
                .Where(m => m != null && m.shader != null)
                .Select(m => m.shader).Distinct().ToList();
                foreach (var shader in uniqueShaders)
                {
                    try
                    {
                        bool supported = (bool)_spiCheckMethod.Value.Invoke(null, [shader]);
                        if (!supported)
                        {
                            spiCompatible = false;
                            break;
                        }
                    }
                    catch (Exception ex) { ModLogger.ForSource("SaberBundleLoader").Debug($"SPI shader check failed: {ex.Message}"); spiCompatible = false; break; }
                }
            }

            var missingNames = new List<string>();
            for (int i = 0; i < materials.Count; i += ShaderBatchSize)
            {
                int count = Math.Min(ShaderBatchSize, materials.Count - i);
                var batch = materials.GetRange(i, count);
                var repairResult = ShaderRepair.FixShadersOnMaterials(batch);
                if (!repairResult.AllShadersReplaced)
                missingNames.AddRange(repairResult.MissingShaderNames);

                if (i + ShaderBatchSize < materials.Count)
                await Task.Yield();
            }
            if (missingNames.Count is > 0)
            {
                ModLogger.Warn($"Some shaders could not be repaired: {string.Join(", ", missingNames)}");
            }
        }
        catch (Exception ex)
        {
            ModLogger.Error($"Failed to repair shaders on saber: {ex}");
            spiCompatible = false;
        }

        return spiCompatible;
    }
}