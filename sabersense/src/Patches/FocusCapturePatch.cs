// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using HarmonyLib;

namespace SaberSense.Patches;

[HarmonyPatch(typeof(DeactivateVRControllersOnFocusCapture), "UpdateVRControllerActiveState")]
internal static class FocusCapturePatch
{
    static bool Prefix() => !(HarmonyBridge.Settings?.KeepSabersOnFocusLoss ?? false);
}