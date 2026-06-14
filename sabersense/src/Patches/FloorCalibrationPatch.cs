// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using HarmonyLib;
using Unity.Mathematics;

namespace SaberSense.Patches;

[HarmonyPatch(typeof(VRCenterAdjust))]
internal static class FloorCalibrationPatch
{
    internal static SettingsApplicatorSO? SettingsApplicator { get; private set; }
    internal static SettingsManager? SettingsManager { get; private set; }

    [HarmonyPostfix]
    [HarmonyPatch("Start")]
    private static void StartPostfix(VRCenterAdjust __instance)
    {
        SettingsApplicator = __instance._settingsApplicator;
        SettingsManager = __instance._settingsManager;

        var settings = HarmonyBridge.Settings;
        if (settings is { FloorCalibrationEnabled: true })
        ApplyCalibration(settings.FloorCalibrationY);
    }

    [HarmonyPostfix]
    [HarmonyPatch("ResetRoom")]
    private static void ResetRoomPostfix()
    {
        var settings = HarmonyBridge.Settings;
        if (settings is { FloorCalibrationEnabled: true })
        ApplyCalibration(settings.FloorCalibrationY);
    }

    public static void ApplyCalibration(float calibrationY)
    {
        if (SettingsManager == null) return;

        float3 center = SettingsManager.settings.room.center;
        center.y = calibrationY;
        SettingsManager.settings.room.center = center;
        SettingsApplicator?.NotifyRoomTransformOffsetWasUpdated();
    }
}