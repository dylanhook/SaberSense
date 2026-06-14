// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using HarmonyLib;
using SaberSense.Input;

namespace SaberSense.Patches;

[HarmonyPatch(typeof(UnityXRHelper))]
internal static class MenuButtonPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(UnityXRHelper.GetMenuButton))]
    private static void GetMenuButtonPostfix(ref bool __result)
    {
        if (ShouldApplyPauseOverride())
        __result = PauseKeyInputBehavior.IsPressed;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(UnityXRHelper.GetMenuButtonDown))]
    private static void GetMenuButtonDownPostfix(ref bool __result)
    {
        if (ShouldApplyPauseOverride())
        __result = PauseKeyInputBehavior.IsPressedDown;
    }

    private static bool ShouldApplyPauseOverride()
    {
        var settings = HarmonyBridge.Settings;
        return settings is not null && settings.PauseKeyEnabled && settings.PauseKeyButton is not VrButtonBinding.None;
    }
}