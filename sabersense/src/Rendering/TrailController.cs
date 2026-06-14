// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;
using SaberSense.Rendering.TrailGeometry;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Rendering;

internal sealed class TrailController
{
    public ITrailDriver? PrimaryHandler { get; private set; }

    private readonly LiveSaber _saber;
    private readonly TrailConfig _trailConfig;
    private List<SecondaryTrailDriver>? _extraTrails;

    public TrailController(LiveSaber saber, TrailConfig trailConfig)
    {
        _saber = saber;
        _trailConfig = trailConfig;
    }

    public void Activate(bool editorMode, global::SaberTrail fallbackTrail)
    {
        var layout = _saber.GetTrailLayout();

        if (layout.Primary is null)
        {
            if (fallbackTrail is { })
            {
                PrimaryHandler = new PrimaryTrailDriver(
                _saber.GameObject, fallbackTrail,
                _saber.PlayerTransforms);
                PrimaryHandler.CreateTrail(_trailConfig, editorMode);
            }
            return;
        }

        PrimaryHandler = new PrimaryTrailDriver(
        _saber.GameObject, _saber.PlayerTransforms);
        PrimaryHandler.SetLiveTrail(layout.Primary);
        PrimaryHandler.CreateTrail(_trailConfig, editorMode);

        if (layout.AuxMarkers is { Count: > 0 })
        {
            _extraTrails = [];
            foreach (var ct in layout.AuxMarkers)
            {
                var handler = new SecondaryTrailDriver(
                _saber.GameObject, ct, _saber.PlayerTransforms);
                handler.CreateTrail(_trailConfig, editorMode);
                _extraTrails.Add(handler);
            }
        }
    }

    public void Teardown(bool immediate = false)
    {
        PrimaryHandler?.DestroyTrail(immediate);
        if (_extraTrails is null) return;
        foreach (var trail in _extraTrails) trail.DestroyTrail();
        _extraTrails = null;
    }

    public void DisposePrimaryMaterialIfOrphaned(MaterialHandle? currentCustomizationMaterial)
    => PrimaryHandler?.DisposeOwnedMaterialIfOrphaned(currentCustomizationMaterial);

    public void TintSecondaryTrails(Color color)
    {
        if (_extraTrails is null) return;
        foreach (var trail in _extraTrails) trail.SetColor(color);
    }

    public void SetVisibilityLayer(CameraUtils.Core.VisibilityLayer layer)
    {
        PrimaryHandler?.SetVisibilityLayer(layer);
        if (_extraTrails is null) return;
        foreach (var trail in _extraTrails) trail.SetVisibilityLayer(layer);
    }
}