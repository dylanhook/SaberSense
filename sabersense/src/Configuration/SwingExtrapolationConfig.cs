// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Framework.Core;

namespace SaberSense.Configuration;

internal sealed class SwingExtrapolationConfig : BindableSettings
{
    private bool _enabled;
    public bool Enabled { get => _enabled; set => SetField(ref _enabled, value); }

    private float _strength = 25f;
    public float Strength { get => _strength; set => SetField(ref _strength, value); }

    private bool _gameOnly;
    public bool GameOnly { get => _gameOnly; set => SetField(ref _gameOnly, value); }
}