// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Framework.Core;
using SaberSense.Input;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Configuration;

internal sealed class ModSettings : BindableSettings
{
    private static readonly ModSettings _defaults = new();

    private bool _isActive = true;
    public bool IsActive { get => _isActive; set => SetField(ref _isActive, value); }

    private bool _randomizeSaber;
    public bool RandomizeSaber { get => _randomizeSaber; set => SetField(ref _randomizeSaber, value); }

    private bool _animateSelection = true;
    public bool AnimateSelection { get => _animateSelection; set => SetField(ref _animateSelection, value); }

    private float _maxGlobalWidth = 3f;
    public float MaxGlobalWidth { get => _maxGlobalWidth; set => SetField(ref _maxGlobalWidth, value); }

    private bool _showGameplayButton = true;
    public bool ShowGameplayButton { get => _showGameplayButton; set => SetField(ref _showGameplayButton, value); }

    private bool _showDefaultSaber;
    public bool ShowDefaultSaber { get => _showDefaultSaber; set => SetField(ref _showDefaultSaber, value); }

    private float _audioGain = 1f;
    public float AudioGain { get => _audioGain; set => SetField(ref _audioGain, value); }

    private VrButtonBinding _actionKeyButton;
    public VrButtonBinding ActionKeyButton { get => _actionKeyButton; set => SetField(ref _actionKeyButton, value); }

    private bool _pauseKeyEnabled;
    public bool PauseKeyEnabled { get => _pauseKeyEnabled; set => SetField(ref _pauseKeyEnabled, value); }

    private VrButtonBinding _pauseKeyButton;
    public VrButtonBinding PauseKeyButton { get => _pauseKeyButton; set => SetField(ref _pauseKeyButton, value); }

    private bool _enableEventManager = true;
    public bool EnableEventManager { get => _enableEventManager; set => SetField(ref _enableEventManager, value); }

    private bool _warningMarkerEnabled;
    public bool WarningMarkerEnabled { get => _warningMarkerEnabled; set => SetField(ref _warningMarkerEnabled, value); }

    private List<int> _warningTypes = [0];
    public List<int> WarningTypes { get => _warningTypes; set => SetField(ref _warningTypes, value); }

    private List<int> _warningLayerFilter = [0, 1, 2];
    public List<int> WarningLayerFilter { get => _warningLayerFilter; set => SetField(ref _warningLayerFilter, value); }

    private bool _hidePlatform;
    public bool HidePlatform { get => _hidePlatform; set => SetField(ref _hidePlatform, value); }

    private bool _keepSabersOnFocusLoss;
    public bool KeepSabersOnFocusLoss { get => _keepSabersOnFocusLoss; set => SetField(ref _keepSabersOnFocusLoss, value); }

    private bool _floorCalibrationEnabled;
    public bool FloorCalibrationEnabled { get => _floorCalibrationEnabled; set => SetField(ref _floorCalibrationEnabled, value); }

    private float _floorCalibrationY;
    public float FloorCalibrationY { get => _floorCalibrationY; set => SetField(ref _floorCalibrationY, value); }

    private Color _accentColor = new(0.62f, 0.79f, 0.16f, 1f);
    public Color AccentColor { get => _accentColor; set => SetField(ref _accentColor, value); }

    private bool _smoothingEnabled;
    public bool SmoothingEnabled { get => _smoothingEnabled; set => SetField(ref _smoothingEnabled, value); }

    private float _smoothingStrength;
    public float SmoothingStrength { get => _smoothingStrength; set => SetField(ref _smoothingStrength, value); }

    private SaberPipeline _activePipeline;
    public SaberPipeline ActivePipeline { get => _activePipeline; set => SetField(ref _activePipeline, value); }

    private List<int> _transformSelections = [];
    public List<int> TransformSelections { get => _transformSelections; set => SetField(ref _transformSelections, value); }

    private List<int> _grabSelections = [];
    public List<int> GrabSelections { get => _grabSelections; set => SetField(ref _grabSelections, value); }

    private TrailConfig _trail = new();
    public TrailConfig Trail { get => _trail; set => SetField(ref _trail, value); }

    private MotionBlurConfig _motionBlur = new();
    public MotionBlurConfig MotionBlur { get => _motionBlur; set => SetField(ref _motionBlur, value); }

    private WorldModConfig _worldMod = new();
    public WorldModConfig WorldMod { get => _worldMod; set => SetField(ref _worldMod, value); }

    private VisibilityConfig _visibility = new();
    public VisibilityConfig Visibility { get => _visibility; set => SetField(ref _visibility, value); }

    private EditorConfig _editor = new();
    public EditorConfig Editor { get => _editor; set => SetField(ref _editor, value); }

    private SwingExtrapolationConfig _swingExtrapolation = new();
    public SwingExtrapolationConfig SwingExtrapolation { get => _swingExtrapolation; set => SetField(ref _swingExtrapolation, value); }

    internal void ResetToDefaults()
    {
        ModSettingsCopier.CopyAll(_defaults, this);
        RaisePropertyChanged(null);
    }
}