// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SaberSense.Configuration;

public enum WorldModulationMode
{
    Rain = 0,
    Snow = 1,
    Network = 2
}

public static class WorldModulationModeRegistry
{
    private static readonly Dictionary<WorldModulationMode, string> Labels = new()
    {
        { WorldModulationMode.Rain,    "Rain" },
        { WorldModulationMode.Snow,    "Snow" },
        { WorldModulationMode.Network, "Network" }
    };

    public static int ModeCount => Labels.Count;

    public static List<string> GetAllLabels()
    {
        var labels = Labels.OrderBy(x => (int)x.Key).Select(x => x.Value).ToList();
        labels.Add("Menu only");
        labels.Add("Override color");
        return labels;
    }

    public static string GetLabel(WorldModulationMode mode)
    {
        return Labels.TryGetValue(mode, out var label) ? label : mode.ToString();
    }
}

public static class WorldModulationOptions
{
    public static int MenuOnly => WorldModulationModeRegistry.ModeCount;

    public static int OverrideColor => WorldModulationModeRegistry.ModeCount + 1;

    public static bool IsMode(int index) => Enum.IsDefined(typeof(WorldModulationMode), index);
}