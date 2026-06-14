// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Framework.Core;

namespace SaberSense.Configuration;

internal sealed class TrailConfig : BindableSettings
{
    private int _curveSmoothnessPercent = 60;

    public int CurveSmoothnessPercent
    {
        get => _curveSmoothnessPercent;
        set => SetClamped(ref _curveSmoothnessPercent, value, 0, 100);
    }

    public int SplineResolution => 2 + (int)(CurveSmoothnessPercent * 0.98f);

    private int _captureSamplesPerSecond;

    public int CaptureSamplesPerSecond
    {
        get => _captureSamplesPerSecond;
        set => SetClamped(ref _captureSamplesPerSecond, value, 0, 144);
    }

    private bool _vertexColorOnly = true;
    public bool VertexColorOnly { get => _vertexColorOnly; set => SetField(ref _vertexColorOnly, value); }

    private bool _overrideTrailSortOrder;
    public bool OverrideTrailSortOrder { get => _overrideTrailSortOrder; set => SetField(ref _overrideTrailSortOrder, value); }

    private bool _localSpaceTrails;
    public bool LocalSpaceTrails { get => _localSpaceTrails; set => SetField(ref _localSpaceTrails, value); }
}