// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;
using SaberSense.Input;
using System.ComponentModel;

namespace SaberSense.App;

internal static class ModSettingsSideEffects
{
    private static ModSettings? _previous;

    public static void Bind(ModSettings settings)
    {
        if (_previous is not null)
        _previous.PropertyChanged -= OnPropertyChanged;

        _previous = settings;
        settings.PropertyChanged += OnPropertyChanged;

        Patches.HarmonyBridge.SwingExtrapolation = settings.SwingExtrapolation;
        Patches.HarmonyBridge.Settings = settings;

        PauseKeyInputBehavior.Initialize();
        PauseKeyInputBehavior.Binding = settings.PauseKeyButton;
        ActionKeyInputBehavior.Initialize();
        ActionKeyInputBehavior.Binding = settings.ActionKeyButton;
    }

    private static void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        var s = (ModSettings)sender;
        switch (e.PropertyName)
        {
            case nameof(ModSettings.AccentColor):
            GUI.Framework.Core.UITheme.SetAccent(s.AccentColor);
            break;
            case nameof(ModSettings.SwingExtrapolation):
            Patches.HarmonyBridge.SwingExtrapolation = s.SwingExtrapolation;
            break;
            case nameof(ModSettings.PauseKeyButton):
            PauseKeyInputBehavior.Binding = s.PauseKeyButton;
            break;
            case nameof(ModSettings.ActionKeyButton):
            ActionKeyInputBehavior.Binding = s.ActionKeyButton;
            break;
            case nameof(ModSettings.FloorCalibrationEnabled):
            case nameof(ModSettings.FloorCalibrationY):
            if (s.FloorCalibrationEnabled)
            Patches.FloorCalibrationPatch.ApplyCalibration(s.FloorCalibrationY);
            break;
        }
    }
}