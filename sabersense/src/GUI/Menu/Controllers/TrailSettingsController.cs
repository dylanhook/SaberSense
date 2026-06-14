// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog.Model;
using SaberSense.Core.Messaging;
using SaberSense.Customization;
using SaberSense.Profiles;

namespace SaberSense.GUI.Menu.Controllers;

internal sealed class TrailSettingsController
{
    private readonly SaberLoadout _loadout;
    private readonly PreviewSession _previewSession;
    private readonly EditScope _scope;
    private readonly IMessageBroker _broker;
    private bool _isSyncing;

    public bool IsSyncing => _isSyncing;

    public ActiveTrailState State { get; } = new();

    public TrailSettingsController(SaberLoadout loadout, PreviewSession previewSession, EditScope scope, IMessageBroker broker)
    {
        _loadout = loadout;
        _previewSession = previewSession;
        _scope = scope;
        _broker = broker;
    }

    public void SetLength(float value)
    {
        if (_isSyncing) return;
        _scope.ApplyTrail(trail =>
        {
            trail.TrailSettings.LengthPercent = value;
            trail.Length = trail.TrailSettings.TrailLength;
        });
        _broker?.Publish(new TrailSettingsChangedMsg());
    }

    public void SetWidth(float value)
    {
        if (_isSyncing) return;
        _scope.ApplyTrail(trail =>
        {
            trail.TrailSettings.WidthPercent = value;
            trail.Width = trail.TrailSettings.TrailWidth;
        });
        _broker?.Publish(new TrailSettingsChangedMsg());
    }

    public void SetWhitestep(float value)
    {
        if (_isSyncing) return;
        _scope.ApplyTrail(trail => trail.WhiteStep = value);
        _scope.Apply(s => s.SetWhiteStep(value));
        _broker?.Publish(new TrailSettingsChangedMsg());
    }

    public void SetOffset(float value)
    {
        if (_isSyncing) return;
        _scope.ApplyTrail(trail =>
        {
            trail.TrailSettings.OffsetPercent = value;
            trail.Offset = trail.TrailSettings.PositionOffset.z;
        });
        _broker?.Publish(new TrailSettingsChangedMsg());
    }

    public void SetFlip(bool value)
    {
        if (_isSyncing) return;
        _scope.ApplyTrail(trail => trail.Flip = value);
        _broker?.Publish(new TrailSettingsChangedMsg());
    }

    public void SetClampTexture(bool value)
    {
        if (_isSyncing) return;
        _scope.ApplyTrail(trail => trail.ClampTexture = value);
        _broker?.Publish(new TrailSettingsChangedMsg());
    }

    public void SyncFromActiveSaber()
    {
        _isSyncing = true;
        try
        {
            if (_loadout?.IsEmpty == true)
            {
                State.ResetToDefaults();
                return;
            }
            var trail = _previewSession?.FocusedSaber?.GetTrailLayout().Primary;
            State.SyncFrom(trail!);
        }
        finally { _isSyncing = false; }
    }

    public void Revert(SaberAssetEntry entry)
    {
        if (entry?.LeftPiece is not Catalog.Model.SaberAssetDefinition definition) return;

        var trail = _previewSession?.FocusedSaber?.GetTrailLayout().Primary;
        if (trail is not null) trail.RevertMaterialForSaberAsset(definition);

        var tm = definition.ExtractTrail(false);
        if (tm is not null)
        {
            var leftCustomization = _loadout?.Left?.Customization;
            if (leftCustomization is not null)
            leftCustomization.TrailSettings = tm.Clone();

            if (entry.RightPiece is Catalog.Model.SaberAssetDefinition rightDefinition)
            {
                var rtm = rightDefinition.ExtractTrail(false);
                var rightCustomization = _loadout?.Right?.Customization;
                if (rtm is not null && rightCustomization is not null)
                rightCustomization.TrailSettings = rtm.Clone();
            }

            _broker?.Publish(new PreviewSaberChangedMsg(entry));
        }
    }
}