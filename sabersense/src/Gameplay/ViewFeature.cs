// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using System.Linq;

namespace SaberSense.Gameplay;

public enum ViewFeature
{
    Sabers = 0,
    Trails = 1,
    WorldModulation = 2,
    MotionSmoothing = 3,
    MotionBlur = 4,
    WarningMarkers = 5
}

public enum ViewType { Desktop, Hmd }

public static class ViewFeatureRegistry
{
    private static readonly Dictionary<ViewFeature, string> Labels = new()
    {
        { ViewFeature.Sabers,           "Sabers" },
        { ViewFeature.Trails,           "Trails" },
        { ViewFeature.WorldModulation,  "World modulation" },
        { ViewFeature.MotionSmoothing,  "Motion smoothing" },
        { ViewFeature.MotionBlur,       "Motion blur" },
        { ViewFeature.WarningMarkers,   "Warning markers" }
    };

    private static readonly HashSet<ViewFeature> DesktopDefaults = new()
    {
        ViewFeature.Sabers,
        ViewFeature.Trails,
        ViewFeature.WorldModulation,
        ViewFeature.MotionSmoothing,
        ViewFeature.MotionBlur
    };

    private static readonly HashSet<ViewFeature> HmdDefaults = new()
    {
        ViewFeature.Sabers,
        ViewFeature.Trails,
        ViewFeature.WorldModulation,
        ViewFeature.WarningMarkers,
        ViewFeature.MotionBlur
    };

    public static List<string> GetAllLabels()
    {
        return Labels.OrderBy(x => (int)x.Key).Select(x => x.Value).ToList();
    }

    public static string GetLabel(ViewFeature feature)
    {
        return Labels.TryGetValue(feature, out var label) ? label : feature.ToString();
    }

    public static List<int> GetDefaults(ViewType view)
    {
        var set = view == ViewType.Desktop ? DesktopDefaults : HmdDefaults;
        return set.Select(f => (int)f).ToList();
    }

    public static bool IsEnabled(List<int> configList, ViewFeature feature, ViewType viewType = ViewType.Desktop)
    {
        return configList?.Contains((int)feature) ?? GetDefaults(viewType).Contains((int)feature);
    }
}