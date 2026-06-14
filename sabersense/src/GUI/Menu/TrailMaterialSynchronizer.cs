// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Customization;
using SaberSense.Profiles;
using UnityEngine;

namespace SaberSense.GUI.Menu;

internal sealed class TrailMaterialSynchronizer
{
    private readonly EditScope _editScope;
    private readonly PreviewSession _previewSession;
    private readonly SaberLoadout _loadout;
    private readonly TrailVisualizationRenderer _trailPreviewer;
    private readonly IMessageBroker _broker;
    private readonly IModLogger _log;
    private float _lastTrailSyncTime;

    public TrailMaterialSynchronizer(
    EditScope editScope,
    PreviewSession previewSession,
    SaberLoadout loadout,
    TrailVisualizationRenderer trailPreviewer,
    IMessageBroker broker,
    IModLogger log)
    {
        _editScope = editScope;
        _previewSession = previewSession;
        _loadout = loadout;
        _trailPreviewer = trailPreviewer;
        _broker = broker;
        _log = log.ForSource(nameof(TrailMaterialSynchronizer));
    }

    public void OnTrailPropertyChanged(Material mat)
    {
        SyncOtherTrailMaterial(mat);
        PushTrailPreviewMaterial(mat);
        _broker.Publish(new TrailMaterialEditedMsg(mat));
    }

    public void OnTrailCommit(Material mat)
    {
        try
        {
            if (mat != null)
            SyncOtherTrailMaterial(mat, force: true);
        }
        catch (System.Exception ex)
        {
            _log.Debug($"Trail commit failed: {ex.Message}");
        }
    }

    private void SyncOtherTrailMaterial(Material mat, bool force = false)
    {
        if (_editScope?.Linked == false) return;
        try
        {
            if (mat == null) return;

            if (!force)
            {
                var now = Time.unscaledTime;
                if (now - _lastTrailSyncTime < 0.1f) return;
                _lastTrailSyncTime = now;
            }

            var focusedHand = _previewSession?.FocusedHand ?? SaberHand.Left;
            var otherProfile = focusedHand == SaberHand.Left
            ? _loadout?.Right
            : _loadout?.Left;
            var otherTrail = otherProfile?.Customization?.TrailSettings;

            if (otherTrail?.Material?.Material is { } otherMat && otherMat != mat)
            otherMat.CopyPropertiesFromMaterial(mat);
        }
        catch (System.Exception ex)
        {
            _log.Info($"Trail material sync failed: {ex.Message}");
        }
    }

    private void PushTrailPreviewMaterial(Material mat)
    {
        _trailPreviewer?.SetMaterial(mat);
    }
}