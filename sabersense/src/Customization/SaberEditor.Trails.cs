// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog.Model;
using SaberSense.Core;
using SaberSense.Core.Utilities;
using SaberSense.Profiles;
using SaberSense.Rendering;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Customization;

internal sealed partial class SaberEditor
{
    private void ColorSabers()
    {
        _previewSession.Sabers.Left?.SetColor(ColorForHand(SaberHand.Left));
        _previewSession.Sabers.Right?.SetColor(ColorForHand(SaberHand.Right));
    }

    private void RecreatePreviewTrails()
    {
        if (IsSaberInHand && _isDraggingPreview)
        RebuildMirrorLiveTrail();
        else if (IsSaberInHand)
        SyncGrabbedTrailSettings();
        else if (_isDraggingPreview)
        RebuildAllTrails();
        else
        RecreateStaticTrailPreview();

        ColorSabers();
    }

    private void RebuildMirrorLiveTrail()
    {
        _log.Debug("RecreatePreviewTrails: drag+grab -> rebuilding mirror live trail");
        var mirror = _editScope?.PreviewMirror;
        if (mirror is null) return;
        _trailPreviewer?.Destroy();
        mirror.DestroyTrail(true);
        mirror.CreateTrail(editorMode: true);
        mirror.SetColor(ColorForHand(_previewSession.FocusedHand));
    }

    private void SyncGrabbedTrailSettings()
    {
        var focusedTrailSettings = (_previewSession.FocusedHand == SaberHand.Left
        ? _loadout?.Left : _loadout?.Right)?.Customization?.TrailSettings;
        if (focusedTrailSettings is not null)
        {
            SyncTrailDimensionsTo(_previewSession.Sabers, focusedTrailSettings);
            SyncTrailDimensionsToMirror(focusedTrailSettings);
        }
        RebuildGrabbedTrails();
        RecreateStaticTrailPreview();
    }

    private static void SyncTrailDimensionsTo(SaberPair sabers, TrailSettings source)
    {
        sabers.ForEachLiveTrail(trail =>
        {
            trail.Length = source.TrailLength;
            trail.Width = source.TrailWidth;
            trail.WhiteStep = source.WhiteBlend;
            if (trail.Material?.Material is { } liveMat
            && source.Material?.Material is { } sourceMat
            && liveMat != sourceMat)
            liveMat.CopyPropertiesFromMaterial(sourceMat);
        });
    }

    private void SyncTrailDimensionsToMirror(TrailSettings source)
    {
        var mirror = _editScope?.PreviewMirror;
        if (mirror is null) return;
        var mirrorTrail = mirror.GetTrailLayout().Primary;
        if (mirrorTrail is null) return;
        mirrorTrail.Length = source.TrailLength;
        mirrorTrail.Width = source.TrailWidth;
        mirrorTrail.WhiteStep = source.WhiteBlend;
        if (mirrorTrail.Material?.Material is { } mirrorMat
        && source.Material?.Material is { } sourceMat
        && mirrorMat != sourceMat)
        mirrorMat.CopyPropertiesFromMaterial(sourceMat);
    }

    private void PushToGrabSaberTrail(Material mat)
    {
        if (mat == null) return;
        try
        {
            _previewSession.Sabers?.ForEachLiveTrail(trail =>
            {
                if (trail?.TrailSettings?.Material?.Material is { } liveMat && liveMat != mat)
                liveMat.CopyPropertiesFromMaterial(mat);
            });
        }
        catch (Exception ex)
        {
            _log.Debug($"PushToGrabSaberTrail failed: {ex.Message}");
        }
    }

    private void RecreateStaticTrailPreview()
    {
        var mirror = _editScope?.PreviewMirror;
        var displaySaber = mirror ?? _previewSession?.FocusedSaber;
        var trail = displaySaber?.GetTrailLayout().Primary;
        if (trail is null || _trailPreviewer is null) return;

        var previewHost = displaySaber!.GameObject.transform.parent;

        _trailPreviewer.Create(
        previewHost,
        trail,
        _settings?.Trail?.VertexColorOnly ?? true
        );
        _trailPreviewer.SetLayer(PreviewCameraLayer);

        try
        {
            var hand = _previewSession!.FocusedHand;
            _trailPreviewer.SetColor(ColorForHand(hand));
        }
        catch (Exception ex) { _log.Debug($"PlayerDataModel unavailable: {ex.Message}"); }
    }

    private void RebuildGrabbedTrails()
    {
        var sabers = _previewSession.Sabers;
        if (GrabLeft && sabers.Left is not null)
        {
            sabers.Left.DestroyTrail(true);
            sabers.Left.CreateTrail(editorMode: true);
            sabers.Left.SetColor(ColorForHand(SaberHand.Left));
        }
        if (GrabRight && sabers.Right is not null)
        {
            sabers.Right.DestroyTrail(true);
            sabers.Right.CreateTrail(editorMode: true);
            sabers.Right.SetColor(ColorForHand(SaberHand.Right));
        }
    }

    public void OnPreviewDragStarted()
    {
        _isDraggingPreview = true;

        if (IsSaberInHand)
        {
            _trailPreviewer?.SetLayer(InvisibleLayer);

            var mirror = _editScope?.PreviewMirror;
            if (mirror is null) return;

            if (mirror.TrailHandler is not null)
            {
                mirror.SetTrailVisibilityLayer((CameraUtils.Core.VisibilityLayer)PreviewCameraLayer);
            }
            else
            {
                ErrorBoundary.FireAndForget(DeferredMirrorTrailSetupAsync(), _log, "DragMirrorTrail");
            }
        }
        else
        {
            RebuildAllTrails();
            ColorSabers();
        }
    }

    private async Task DeferredMirrorTrailSetupAsync()
    {
        await Task.Yield();
        if (!_isDraggingPreview || !IsSaberInHand) return;

        var mirror = _editScope?.PreviewMirror;
        if (mirror is null) return;

        mirror.CreateTrail(editorMode: true);

        mirror.SetTrailVisibilityLayer((CameraUtils.Core.VisibilityLayer)PreviewCameraLayer);
        mirror.SetColor(ColorForHand(_previewSession.FocusedHand));
    }

    private void RebuildAllTrails()
    {
        _trailPreviewer?.Destroy();
        _previewSession.Sabers.ForEach(saber =>
        {
            saber.DestroyTrail(true);
            saber.CreateTrail(true);
        });
    }

    public void OnPreviewDragEnded()
    {
        _isDraggingPreview = false;

        if (IsSaberInHand)
        {
            var mirror = _editScope?.PreviewMirror;
            mirror?.SetTrailVisibilityLayer((CameraUtils.Core.VisibilityLayer)InvisibleLayer);

            RecreateStaticTrailPreview();
        }
        else
        {
            _previewSession.Sabers.ForEach(saber => saber.DestroyTrail(true));

            var focusedHand = _previewSession.FocusedHand;
            var liveSaber = _previewSession.FocusedSaber;
            var trail = liveSaber?.GetTrailLayout().Primary;
            if (trail is not null && liveSaber!.TrailHandler is null)
            {
                _trailPreviewer.Create(
                liveSaber.GameObject.transform.parent,
                trail,
                _settings?.Trail?.VertexColorOnly ?? true
                );
                _trailPreviewer.SetLayer(PreviewCameraLayer);
                try
                {
                    _trailPreviewer.SetColor(ColorForHand(focusedHand));
                }
                catch (System.Exception ex) { _log.Debug($"PlayerDataModel unavailable: {ex.Message}"); }
            }
        }
    }

    public void ApplyTrailSelection(SaberAssetEntry entry, Catalog.Model.TrailSettings? trailSettings,
    List<SaberSense.Rendering.SaberTrailMarker> trailList, LiveSaber? activeSaber)
    {
        if (entry?.LeftPiece is not SaberAssetDefinition leftDefinition) return;

        ApplyTrailToCustomization(_loadout.Left, leftDefinition, trailSettings, trailList, activeSaber);

        if (entry.RightPiece is SaberAssetDefinition rightDefinition)
        ApplyTrailToCustomization(_loadout.Right, rightDefinition, trailSettings, trailList, activeSaber: null);

        _broker.Publish(new SaberSense.Core.Messaging.PreviewSaberChangedMsg(entry));
    }

    private static void ApplyTrailToCustomization(SaberProfile profile, SaberAssetDefinition definition,
    Catalog.Model.TrailSettings? trailSettings, List<SaberSense.Rendering.SaberTrailMarker> trailList,
    LiveSaber? activeSaber)
    {
        if (profile?.Customization is null) return;

        var customizationTrail = profile.Customization.TrailSettings;

        if (trailSettings is null)
        {
            var trail = activeSaber?.GetTrailLayout().Primary;
            trail?.RevertMaterialForSaberAsset(definition);
            var tm = definition.ExtractTrail(false);
            if (tm is not null)
            {
                profile.Customization.TrailSettings = tm.Clone();
            }
        }
        else
        {
            if (customizationTrail is null)
            {
                customizationTrail = new Catalog.Model.TrailSettings(
                new MaterialHandle(null), 12, 0.5f, Vector3.zero, 0f, TextureWrapMode.Clamp)
                { OriginTrails = trailList };
                customizationTrail.CloneUserSettings(trailSettings);
                customizationTrail.Material!.RefreshSnapshot(false);
                profile.Customization.TrailSettings = customizationTrail;
            }
            else
            {
                customizationTrail.CloneUserSettings(trailSettings);
                customizationTrail.OriginTrails = trailList;
            }
        }
    }
}