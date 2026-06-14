// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Framework.Core;

namespace SaberSense.Configuration;

internal sealed class EditorConfig : BindableSettings
{
    private bool _previewSaber = true;
    public bool PreviewSaber { get => _previewSaber; set => SetField(ref _previewSaber, value); }

    private bool _rotation = false;
    public bool Rotation { get => _rotation; set => SetField(ref _rotation, value); }

    private float _rotationSpeed = 25f;
    public float RotationSpeed { get => _rotationSpeed; set => SetField(ref _rotationSpeed, value); }

    private bool _bloom = false;
    public bool Bloom { get => _bloom; set => SetField(ref _bloom, value); }

    private bool _displayTrails = true;
    public bool DisplayTrails { get => _displayTrails; set => SetField(ref _displayTrails, value); }

    private int _saberPreviewMode;
    public int SaberPreviewMode { get => _saberPreviewMode; set => SetField(ref _saberPreviewMode, value); }

    private int _sortMode;
    public int SortMode { get => _sortMode; set => SetField(ref _sortMode, value); }
}