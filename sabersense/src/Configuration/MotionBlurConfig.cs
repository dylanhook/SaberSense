// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Framework.Core;

namespace SaberSense.Configuration;

internal sealed class MotionBlurConfig : BindableSettings
{
    private bool _enabled;
    public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }

    private float _strength = 50f;
    public float Strength { get => _strength; set => SetField(ref _strength, value); }
}