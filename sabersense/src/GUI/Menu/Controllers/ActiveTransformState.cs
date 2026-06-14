// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Framework.Core;

namespace SaberSense.GUI.Menu.Controllers;

internal sealed class ActiveTransformState : BindableSettings
{
    private float _width = 1f;
    public float Width { get => _width; set => SetField(ref _width, value); }

    private float _length = 1f;
    public float Length { get => _length; set => SetField(ref _length, value); }

    private float _rotation;
    public float Rotation { get => _rotation; set => SetField(ref _rotation, value); }

    private float _offset;
    public float Offset { get => _offset; set => SetField(ref _offset, value); }

    public void SyncFrom(float width, float length, float rotation, float offset)
    {
        BatchUpdate(() =>
        {
            Width = width;
            Length = length;
            Rotation = rotation;
            Offset = offset;
        });
        UnityEngine.Canvas.ForceUpdateCanvases();
    }

    public void ResetToDefaults()
    {
        BatchUpdate(() =>
        {
            Width = 1f;
            Length = 1f;
            Rotation = 0f;
            Offset = 0f;
        });
        UnityEngine.Canvas.ForceUpdateCanvases();
    }
}