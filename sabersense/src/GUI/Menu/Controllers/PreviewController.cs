// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog.Model;
using SaberSense.Configuration;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Customization;
using SaberSense.GUI.Framework.Core;
using SaberSense.Rendering;
using System;
using UnityEngine;

namespace SaberSense.GUI.Menu.Controllers;

internal sealed partial class PreviewController : IDisposable
{
    private readonly PreviewSession _previewSession;
    private readonly SaberSense.GUI.TrailVisualizationRenderer _trailPreviewer;
    private readonly SaberSense.Customization.SaberEditor _editor;
    private readonly ModSettings _settings;
    private readonly PlayerDataModel _playerDataModel;
    private readonly SaberSense.Catalog.CoverGenerationService _coverService;
    private readonly LiveSaber.Factory _liveSaberCreator;
    private readonly SaberSense.Customization.EditScope _editScope;
    private IDisposable? _settingsChangedSub;
    private IDisposable? _configLoadedSub;

    private UISaberPreview? _saberPreview;
    private UILabel? _previewTitleLabel;
    private GameObject? _previewWindowGO;
    private LiveSaber? _mirrorLeft;
    private LiveSaber? _mirrorRight;
    private SaberAssetEntry? _mirrorSourceEntry;
    private readonly GUI.Framework.Core.BindingScope _bindingScope = new();
    private readonly SaberSense.Rendering.Materials.SharedMaterialPool _materialPool;

    private const int PreviewCameraLayer = 12;
    private const int InvisibleLayer = 31;

    private const float AutoSwitchInterval = 15f;
    private RectTransform? _timerFillRect;
    private float _autoSwitchTimer;
    private bool _isAutoMode;
    private UIComboBox? _previewModeCombo;

    public event Action? OnFocusedSaberChanged;

    public PreviewController(
    PreviewSession previewSession,
    SaberSense.GUI.TrailVisualizationRenderer trailVisualizationRenderer,
    SaberSense.Customization.SaberEditor editor,
    ModSettings settings,
    PlayerDataModel playerDataModel,
    SaberSense.Catalog.CoverGenerationService coverService,
    LiveSaber.Factory liveSaberCreator,
    SaberSense.Customization.EditScope editScope,
    SaberSense.Core.Messaging.IMessageBroker broker,
    SaberSense.Rendering.Materials.SharedMaterialPool materialPool)
    {
        _previewSession = previewSession;
        _trailPreviewer = trailVisualizationRenderer;
        _editor = editor;
        _settings = settings;
        _playerDataModel = playerDataModel;
        _coverService = coverService;
        _liveSaberCreator = liveSaberCreator;
        _editScope = editScope;
        _materialPool = materialPool;
        _settingsChangedSub = broker?.Subscribe<SaberSense.Core.Messaging.SettingsChangedMsg>(_ => SyncPreviewMode());
        _configLoadedSub = broker?.Subscribe<SaberSense.Core.Messaging.ConfigLoadedMsg>(_ => OnConfigLoaded());
    }

    public UISaberPreview? SaberPreview => _saberPreview;

    public UILabel? TitleLabel => _previewTitleLabel;

    public GameObject? WindowGO => _previewWindowGO;

    public void CreateTrailPreview(LiveSaber? displaySaber = null, LiveSaber? trailDataSource = null)
    {
        _trailPreviewer?.Destroy();
        var hand = _previewSession.FocusedHand;
        var liveSaber = displaySaber ?? _previewSession.FocusedSaber;
        var dataSource = trailDataSource ?? liveSaber;
        var trailData = dataSource?.GetTrailLayout().Primary;
        if (trailData == null) return;

        if (liveSaber!.TrailHandler == null)
        {
            _trailPreviewer!.Create(
            liveSaber.GameObject.transform.parent,
            trailData,
            _settings?.Trail?.VertexColorOnly ?? true
            );

            _trailPreviewer.SetLayer(PreviewCameraLayer);

            try
            {
                var scheme = _playerDataModel.playerData.colorSchemesSettings.GetSelectedColorScheme();
                _trailPreviewer.SetColor(hand == SaberHand.Left ? scheme.saberAColor : scheme.saberBColor);
            }
            catch (Exception ex) { ModLogger.ForSource("PreviewController").Debug($"PlayerDataModel unavailable: {ex.Message}"); }
        }

        SetTrailVisualizerVisible(_settings?.Editor?.DisplayTrails ?? true);
    }

    public void OnSaberPreviewInstantiated(LiveSaber saber, SaberHand hand)
    {
        bool anyGrabbed = (_editor is not null && (_editor.GrabLeft || _editor.GrabRight));

        if (anyGrabbed)
        {
            ClearMirror();

            var sabers = _previewSession?.Sabers;
            if (sabers?.Left != null)
            {
                _mirrorLeft = _liveSaberCreator.Create(sabers.Left.Profile, SaberSense.Rendering.Materials.MaterialPoolOwner.Menu);
                _mirrorLeft.CachedTransform.position = new Vector3(0, -1000, 0);
                _mirrorLeft.SetColor(GetHandColor(SaberHand.Left));
            }
            if (sabers?.Right != null)
            {
                _mirrorRight = _liveSaberCreator.Create(sabers.Right.Profile, SaberSense.Rendering.Materials.MaterialPoolOwner.Menu);
                _mirrorRight.CachedTransform.position = new Vector3(0, -1000, 0);
                _mirrorRight.SetColor(GetHandColor(SaberHand.Right));
            }
            _mirrorSourceEntry = _previewSession?.ActiveEntry;

            var activeMirror = MirrorFor(hand);
            var inactiveMirror = InactiveMirrorFor(hand);
            inactiveMirror?.GameObject?.SetActive(false);

            _editScope.PreviewMirror = activeMirror;
            _saberPreview?.SetSaber(activeMirror!);
        }
        else
        {
            ClearMirror();

            _saberPreview?.SetSaber(saber);
        }

        CreateTrailPreview(MirrorFor(hand));
    }

    private void ShowFocusedSaber()
    {
        var sabers = _previewSession?.Sabers;
        if (sabers is null) return;

        var hand = _previewSession!.FocusedHand;
        var targetSaber = sabers[hand];
        if (targetSaber == null) return;

        bool isGrabbed = hand == SaberHand.Left
        ? (_editor is not null && _editor.GrabLeft)
        : (_editor is not null && _editor.GrabRight);

        if (isGrabbed)
        {
            var activeMirror = MirrorFor(hand);
            var inactiveMirror = InactiveMirrorFor(hand);

            if (activeMirror?.GameObject != null)
            {
                activeMirror.GameObject.SetActive(true);
                inactiveMirror?.GameObject?.SetActive(false);

                _editScope.PreviewMirror = activeMirror;
                _saberPreview?.SetSaber(activeMirror);
                _editor?.UpdateSaberVisibility();
                _previewSession.RefreshActiveRenderer();

                activeMirror.DestroyTrail(true);

                CreateTrailPreview(activeMirror);
                OnFocusedSaberChanged?.Invoke();
                return;
            }

            ClearMirror();
            var newMirror = _liveSaberCreator.Create(targetSaber.Profile, SaberSense.Rendering.Materials.MaterialPoolOwner.Menu);
            newMirror.CachedTransform.position = new Vector3(0, -1000, 0);
            newMirror.SetColor(GetHandColor(hand));
            if (hand == SaberHand.Left) _mirrorLeft = newMirror;
            else _mirrorRight = newMirror;
            _editScope.PreviewMirror = newMirror;
            _saberPreview?.SetSaber(newMirror);
        }
        else
        {
            ClearMirror();
            _saberPreview?.SetSaber(targetSaber);
        }

        CreateTrailPreview(MirrorFor(hand));

        _editor?.UpdateSaberVisibility();

        _previewSession.RefreshActiveRenderer();
        OnFocusedSaberChanged?.Invoke();
    }

    public void Tick()
    {
        if (!_isAutoMode || _previewSession?.Sabers == null) return;

        if (_previewSession.FocusedSaber == null) return;

        if (_saberPreview != null && _saberPreview.IsDragging)
        {
            UpdateTimerBar();
            return;
        }

        _autoSwitchTimer += Time.deltaTime;
        UpdateTimerBar();

        if (_autoSwitchTimer >= AutoSwitchInterval)
        {
            _autoSwitchTimer = 0f;
            _previewSession.FocusedHand = _previewSession.FocusedHand == SaberHand.Left
            ? SaberHand.Right
            : SaberHand.Left;
            ShowFocusedSaber();
        }
    }

    private void SyncPreviewMode()
    {
        if (_settings?.Editor is null) return;
        var ed = _settings.Editor;

        _saberPreview?.SetBloom(ed.Bloom);
        _saberPreview?.SetDisplayTrails(ed.DisplayTrails);
        SetTrailVisualizerVisible(ed.DisplayTrails);
        _saberPreview?.SetRotation(ed.Rotation, ed.RotationSpeed);

        int mode = ed.SaberPreviewMode;
        _isAutoMode = mode == 0;
        _autoSwitchTimer = 0f;
        _previewModeCombo?.SetSelected(mode);
        if (mode == 1)
        _previewSession.FocusedHand = SaberHand.Left;
        else if (mode == 2)
        _previewSession.FocusedHand = SaberHand.Right;
        UpdateTimerBar();
    }

    private void UpdateTimerBar()
    {
        if (_timerFillRect == null) return;

        if (_isAutoMode && _previewSession?.FocusedSaber != null)
        {
            float fill = Mathf.Clamp01(_autoSwitchTimer / AutoSwitchInterval);
            _timerFillRect.anchorMax = new Vector2(fill, 1f);
        }
        else
        {
            _timerFillRect.anchorMax = new Vector2(0f, 1f);
        }
    }

    public void SetTitle(string text)
    {
        _previewTitleLabel?.SetText(text);
    }

    public UnityEngine.Sprite? CaptureSnapshot(int size = 128) => _saberPreview?.CaptureSnapshot(size);

    public void Cleanup()
    {
        _coverService?.ClearCaptureSource();
        ClearMirror();

        _saberPreview?.Dispose();
        _saberPreview = null;

        if (_previewWindowGO != null)
        UnityEngine.Object.Destroy(_previewWindowGO);
        _previewWindowGO = null;
    }

    private void SetTrailVisualizerVisible(bool val)
    {
        if (_trailPreviewer is null) return;

        _trailPreviewer.SetLayer(val ? PreviewCameraLayer : InvisibleLayer);
    }

    private void OnConfigLoaded()
    {
        ClearMirror();
    }

    private void ClearMirror()
    {
        _mirrorLeft?.Destroy();
        _mirrorLeft = null;
        _mirrorRight?.Destroy();
        _mirrorRight = null;
        _mirrorSourceEntry = null;
        _editScope.PreviewMirror = null;
    }

    private LiveSaber? MirrorFor(SaberHand hand) => hand == SaberHand.Left ? _mirrorLeft : _mirrorRight;
    private LiveSaber? InactiveMirrorFor(SaberHand hand) => hand == SaberHand.Left ? _mirrorRight : _mirrorLeft;

    private Color GetHandColor(SaberHand hand)
    {
        try
        {
            var scheme = _playerDataModel.playerData.colorSchemesSettings.GetSelectedColorScheme();
            return hand == SaberHand.Left ? scheme.saberAColor : scheme.saberBColor;
        }
        catch { return Color.white; }
    }

    public void Dispose()
    {
        _settingsChangedSub?.Dispose();
        _settingsChangedSub = null;
        _configLoadedSub?.Dispose();
        _configLoadedSub = null;
        _bindingScope.Dispose();
        Cleanup();
    }
}