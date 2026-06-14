// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;
using SaberSense.Customization;

namespace SaberSense.Patches;

internal static class HarmonyBridge
{
    internal static IEditorDeactivator? Editor { get; set; }

    internal static GUI.SaberSenseMenuButton? MenuButton { get; set; }

    internal static SwingExtrapolationConfig? SwingExtrapolation { get; set; }

    internal static ModSettings? Settings { get; set; }

    internal static bool IsInGameCore { get; set; }
}