// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using CameraUtils.Core;
using SaberSense.Configuration;
using UnityEngine;

namespace SaberSense.Gameplay;

internal sealed class ViewVisibilityService
{
    private readonly ModSettings _settings;

    public ViewVisibilityService(ModSettings settings)
    {
        _settings = settings;
    }

    public bool IsVisible(ViewFeature feature, ViewType view)
    {
        var list = view == ViewType.Desktop ? _settings.Visibility?.Desktop : _settings.Visibility?.Hmd;
        return list != null && ViewFeatureRegistry.IsEnabled(list, feature, view);
    }

    public void ApplyLayers(GameObject go, ViewFeature feature, VisibilityLayer baseLayer = VisibilityLayer.Default)
    {
        if (go == null) return;

        bool hmd = IsVisible(feature, ViewType.Hmd);
        bool desk = IsVisible(feature, ViewType.Desktop);

        if (!hmd && !desk)
        {
            go.SetActive(false);
            return;
        }

        go.SetActive(true);

        if (hmd && desk)
        {
            go.SetLayerRecursively(baseLayer);
            return;
        }

        var layer = hmd ? VisibilityLayer.HmdOnlyAndReflected : VisibilityLayer.DesktopOnlyAndReflected;
        go.SetLayerRecursively(layer);
    }

    public void ApplyVisibility(GameObject go, ViewFeature feature, bool keepActiveForTrails = false, VisibilityLayer baseLayer = VisibilityLayer.Default)
    {
        if (go == null) return;
        bool hmd = IsVisible(feature, ViewType.Hmd);
        bool desk = IsVisible(feature, ViewType.Desktop);
        ApplyVisibility(go, hmd, desk, keepActiveForTrails, baseLayer);
    }

    public static void ApplyVisibility(GameObject go, bool hmd, bool desk, bool keepActiveForTrails = false, VisibilityLayer baseLayer = VisibilityLayer.Default)
    {
        if (go == null) return;

        if (hmd && desk)
        {
            go.SetActive(true);
            go.SetLayerRecursively(baseLayer);
            return;
        }

        if (!hmd && !desk)
        {
            if (keepActiveForTrails)
            {
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
                foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.enabled = false;
            }
            else
            {
                go.SetActive(false);
            }
            return;
        }

        go.SetActive(true);
        var layer = hmd ? VisibilityLayer.HmdOnlyAndReflected : VisibilityLayer.DesktopOnlyAndReflected;
        go.SetLayerRecursively(layer);
    }
}