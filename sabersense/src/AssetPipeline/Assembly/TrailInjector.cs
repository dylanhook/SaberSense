// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.AssetPipeline.Formats.Saber;
using SaberSense.Core.Logging;
using SaberSense.Rendering;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.AssetPipeline.Assembly;

internal static class TrailInjector
{
    private static readonly IModLogger Log = ModLogger.ForSource(nameof(TrailInjector));

    internal static void InjectTrails(
    GameObject root,
    IReadOnlyList<TrailData> trails,
    SaberParseResult parseResult,
    AssetBundle? bundle = null,
    IReadOnlyList<Material>? bundleOwnedMaterials = null)
    {
        if (root == null || trails is null || trails.Count is 0) return;

        var transformsByName = InjectionHelpers.BuildTransformLookup(root.transform);

        var pathIdToTransform = ResolveTransformPathIdsSimple(parseResult, transformsByName);

        var materialsByName = BuildMaterialLookup(root, bundle);

        if (bundleOwnedMaterials is not null)
        {
            foreach (var mat in bundleOwnedMaterials)
            {
                if (mat != null && !string.IsNullOrEmpty(mat.name) && !materialsByName.ContainsKey(mat.name))
                materialsByName[mat.name] = mat;
            }
        }

        Log.Info($"Bundle materials loaded: {materialsByName.Count}");

        foreach (var trail in trails)
        {
            var pointStart = InjectionHelpers.ResolveTransform(trail.PointStartPathId, pathIdToTransform);
            var pointEnd = InjectionHelpers.ResolveTransform(trail.PointEndPathId, pathIdToTransform);

            if (pointStart == null || pointEnd == null)
            {
                Log.Warn("Could not resolve trail transforms, skipping trail");
                continue;
            }

            Material? trailMaterial = ResolveTrailMaterial(trail.TrailMaterialPathId, parseResult, materialsByName, bundle, bundleOwnedMaterials);

            var trailHost = FindTrailHost(pointStart, root.transform);
            var marker = trailHost.gameObject.GetComponent<SaberTrailMarker>() ?? trailHost.gameObject.AddComponent<SaberTrailMarker>();

            if (marker == null)
            {
                Log.Warn($"Failed to add SaberTrailMarker to {trailHost.name}");
                continue;
            }

            marker.PointStart = pointStart;
            marker.PointEnd = pointEnd;
            marker.TrailMaterial = trailMaterial;
            marker.ColorMode = (TrailColorMode)trail.ColorType;
            marker.TrailColor = trail.TrailColor;
            marker.MultiplierColor = trail.MultiplierColor;
            marker.Length = trail.Length;

            trail.PointStart = pointStart;
            trail.PointEnd = pointEnd;
            trail.TrailMaterial = trailMaterial;

            Log.Info($"Injected SaberTrailMarker on '{trailHost.name}' length={trail.Length} colorType={trail.ColorType}");
        }

        FillTrailGaps(root.transform);
    }

    private static Dictionary<long, Transform> ResolveTransformPathIdsSimple(
    SaberParseResult parseResult,
    Dictionary<string, List<Transform>> transformsByName)
    {
        var map = new Dictionary<long, Transform>();
        if (parseResult?.PathIdToGameObjectName is null) return map;

        foreach (var (pathId, goName) in parseResult.PathIdToGameObjectName)
        {
            if (transformsByName.TryGetValue(goName, out var transforms) && transforms.Count is > 0)
            map[pathId] = transforms[0];
        }

        return map;
    }

    private static Dictionary<string, Material> BuildMaterialLookup(GameObject prefab, AssetBundle? bundle)
    {
        var lookup = new Dictionary<string, Material>();

        if (prefab != null)
        {
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null && !string.IsNullOrEmpty(mat.name) && !lookup.ContainsKey(mat.name))
                    lookup[mat.name] = mat;
                }
            }
        }

        if (bundle != null)
        {
            try
            {
                var bundleMats = bundle.LoadAllAssets<Material>();
                if (bundleMats is not null)
                {
                    foreach (var mat in bundleMats)
                    {
                        if (mat != null && !string.IsNullOrEmpty(mat.name) && !lookup.ContainsKey(mat.name))
                        lookup[mat.name] = mat;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Fallback bundle.LoadAllAssets failed: {ex.Message}");
            }
        }

        return lookup;
    }

    private static Material? ResolveTrailMaterial(
    long materialPathId,
    SaberParseResult parseResult,
    Dictionary<string, Material> materialsByName,
    AssetBundle? bundle,
    IReadOnlyList<Material>? bundleOwnedMaterials = null)
    {
        if (materialPathId is 0 || parseResult?.PathIdToMaterialName is null)
        return null;

        if (!parseResult.PathIdToMaterialName.TryGetValue(materialPathId, out var matName))
        {
            Log.Warn($"Trail material pathId {materialPathId} not found in name map");
            return null;
        }

        if (materialsByName.TryGetValue(matName, out var material))
        return material;

        if (bundle != null)
        {
            try
            {
                material = bundle.LoadAsset<Material>(matName);
                if (material != null)
                {
                    materialsByName[matName] = material;
                    return material;
                }
            }
            catch (Exception ex) { Log.Debug($"LoadAsset failed for sub-asset '{matName}': {ex.Message}"); }
        }

        if (bundleOwnedMaterials is not null)
        {
            foreach (var mat in bundleOwnedMaterials)
            {
                if (mat != null && mat.name == matName)
                {
                    materialsByName[matName] = mat;
                    return mat;
                }
            }
        }

        Log.Error($"Trail material '{matName}' not found via any resolution path");
        return null;
    }

    private static void FillTrailGaps(Transform root)
    {
        var saberChildren = InjectionHelpers.FindSaberChildren(root);
        if (saberChildren.Count is < 2) return;

        GameObject? donor = null;
        var recipients = new List<GameObject>();
        foreach (var child in saberChildren)
        {
            if (child.GetComponentsInChildren<SaberTrailMarker>(true).Length is > 0)
            donor ??= child;
            else
            recipients.Add(child);
        }

        if (donor == null || recipients.Count is 0) return;

        var donorMarkers = donor.GetComponentsInChildren<SaberTrailMarker>(true);
        foreach (var recipient in recipients)
        CloneTrailMarkers(donorMarkers, recipient);
    }

    private static void CloneTrailMarkers(SaberTrailMarker[] sources, GameObject recipient)
    {
        foreach (var src in sources)
        {
            if (src.PointEnd == null || src.PointStart == null)
            {
                Log.Warn($"Skipping clone for marker with null attachment points (colorType={(int)src.ColorMode})");
                continue;
            }

            var marker = recipient.AddComponent<SaberTrailMarker>();
            marker.TrailMaterial = src.TrailMaterial;
            marker.ColorMode = src.ColorMode;
            marker.TrailColor = src.TrailColor;
            marker.MultiplierColor = src.MultiplierColor;
            marker.Length = src.Length;

            var tipObj = recipient.CreateGameObject("SS_TrailTip");
            var baseObj = recipient.CreateGameObject("SS_TrailBase");
            tipObj.transform.localPosition = src.PointEnd.localPosition;
            baseObj.transform.localPosition = src.PointStart.localPosition;
            marker.PointEnd = tipObj.transform;
            marker.PointStart = baseObj.transform;

            Log.Info($"Cloned SaberTrailMarker to '{recipient.name}' length={src.Length} colorType={(int)src.ColorMode}");
        }
    }

    private static Transform FindTrailHost(Transform pointStart, Transform root)
    {
        var current = pointStart;
        while (current != null && current != root && current.parent != root)
        {
            current = current.parent;
        }
        return current ?? root;
    }
}