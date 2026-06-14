// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Framework.Core;
using SaberSense.Rendering.TrailGeometry;

namespace SaberSense.GUI.Menu.Controllers;

internal sealed class ActiveTrailState : BindableSettings
{
    private float _lengthPercent = 100f;
    public float LengthPercent { get => _lengthPercent; set => SetField(ref _lengthPercent, value); }

    private float _widthPercent = 100f;
    public float WidthPercent { get => _widthPercent; set => SetField(ref _widthPercent, value); }

    private float _whitestep = 0.1f;
    public float Whitestep { get => _whitestep; set => SetField(ref _whitestep, value); }

    private float _offsetPercent;
    public float OffsetPercent { get => _offsetPercent; set => SetField(ref _offsetPercent, value); }

    private bool _flip;
    public bool Flip { get => _flip; set => SetField(ref _flip, value); }

    private bool _clampTexture;
    public bool ClampTexture { get => _clampTexture; set => SetField(ref _clampTexture, value); }

    public void SyncFrom(LiveTrail trail)
    {
        BatchUpdate(() =>
        {
            if (trail is null)
            {
                ResetToDefaultsInternal();
                return;
            }
            LengthPercent = trail.TrailSettings.LengthPercent;
            WidthPercent = trail.TrailSettings.WidthPercent;
            Whitestep = trail.WhiteStep;
            OffsetPercent = trail.TrailSettings.OffsetPercent;
            Flip = trail.Flip;
            ClampTexture = trail.ClampTexture;
        });
        UnityEngine.Canvas.ForceUpdateCanvases();
    }

    public void ResetToDefaults()
    {
        BatchUpdate(ResetToDefaultsInternal);
        UnityEngine.Canvas.ForceUpdateCanvases();
    }

    private void ResetToDefaultsInternal()
    {
        LengthPercent = 100f;
        WidthPercent = 100f;
        Whitestep = 0.1f;
        OffsetPercent = 0f;
        Flip = false;
        ClampTexture = false;
    }
}