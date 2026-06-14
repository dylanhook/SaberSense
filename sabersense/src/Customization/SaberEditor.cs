// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.App;
using SaberSense.Catalog.Model;
using SaberSense.Configuration;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Core.Utilities;
using SaberSense.Loadout;
using SaberSense.Profiles;
using SaberSense.Rendering;
using SaberSense.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Customization;

internal sealed partial class SaberEditor : IDisposable, IEditorDeactivator
{
    public bool IsActive => _session.IsEditing;

    public bool GrabLeft { get; private set; }

    public bool GrabRight { get; private set; }

    public bool IsSaberInHand => GrabLeft || GrabRight;

    public bool IsLoadoutEmpty => _loadout.IsEmpty;

    public SaberAssetEntry? LoadoutEntry
    {
        get
        {
            if (_loadout.Left.TryGetSaberAsset(out var sa) && sa?.OwnerEntry is not null)
            return sa.OwnerEntry;
            return null;
        }
    }

    private const int PreviewCameraLayer = 12;
    private const int InvisibleLayer = 31;

    private readonly PreviewSession _previewSession;
    private readonly PlayerDataModel _playerDataModel;
    private readonly ModSettings _settings;
    private readonly GripAttachment _grip;
    private readonly SaberLoadout _loadout;
    private readonly IConfigStore _configManager;
    private readonly SessionController _session;
    private readonly IMessageBroker _broker;
    private readonly SaberSense.GUI.TrailVisualizationRenderer _trailPreviewer;
    private readonly EditScope _editScope;
    private readonly IModLogger _log;

    private IDisposable? _trailSettingsSub;
    private IDisposable? _trailMatEditSub;
    private IDisposable? _equippedSub;
    private IDisposable? _configLoadingSub;
    private IDisposable? _configLoadedSub;
    private CancellationTokenSource? _animationCts;
    private CancellationTokenSource? _spawnCts;
    private volatile bool _isResuming;

    private volatile bool _isDraggingPreview;

    private SaberEditor(
    ModSettings settings,
    PreviewSession previewSession,
    SaberLoadout loadout,
    IConfigStore configManager,
    SessionController session,
    PlayerDataModel playerDataModel,
    GripAttachment gripAttachment,
    IMessageBroker broker,
    SaberSense.GUI.TrailVisualizationRenderer trailPreviewer,
    EditScope editScope,
    IModLogger log)
    {
        _settings = settings;
        _previewSession = previewSession;
        _loadout = loadout;
        _configManager = configManager;
        _session = session;
        _playerDataModel = playerDataModel;
        _grip = gripAttachment;
        _broker = broker;
        _trailPreviewer = trailPreviewer;
        _editScope = editScope;
        _log = log.ForSource(nameof(SaberEditor));
    }

    public void SetGrab(bool left, bool right)
    {
        if (GrabLeft == left && GrabRight == right) return;
        bool wasLeft = GrabLeft;
        bool wasRight = GrabRight;
        GrabLeft = left;
        GrabRight = right;

        if (wasLeft && !left) _grip.SetGripVisible(SaberHand.Left, true);
        if (wasRight && !right) _grip.SetGripVisible(SaberHand.Right, true);

        if (!IsLoadoutEmpty)
        _previewSession.Reload();
    }

    public void Dispose()
    {
        if (_previewSession.CanResume)
        _previewSession.WipePreviews();

        if (Patches.HarmonyBridge.Editor == this)
        Patches.HarmonyBridge.Editor = null;
        CancelSpawn();
        CancelAnimation();
        _equippedSub?.Dispose();
        _trailSettingsSub?.Dispose();
        _trailMatEditSub?.Dispose();
        _configLoadingSub?.Dispose();
        _configLoadedSub?.Dispose();
    }

    private void CancelSpawn()
    {
        _spawnCts?.Cancel();
        _spawnCts?.Dispose();
        _spawnCts = null;
    }

    private void CancelAnimation()
    {
        _animationCts?.Cancel();
        _animationCts?.Dispose();
        _animationCts = null;
    }

    private void TeardownSubscriptions()
    {
        _session.TransitionTo(SessionPhase.Idle);
        CancelSpawn();
        CancelAnimation();
        _equippedSub?.Dispose(); _equippedSub = null;
        _trailSettingsSub?.Dispose(); _trailSettingsSub = null;
        _trailMatEditSub?.Dispose(); _trailMatEditSub = null;
        _configLoadingSub?.Dispose(); _configLoadingSub = null;
        _configLoadedSub?.Dispose(); _configLoadedSub = null;
        _previewSession.SynchronizeScale();
    }

    private void SubscribeToEditorMessages()
    {
        if (_equippedSub is not null) return;
        _equippedSub = _broker?.Subscribe<SaberEquippedMsg>(msg =>
        {
            if (_isResuming) return;
            if (_session.Phase == SessionPhase.LoadingConfig
            && msg.Source != EquipSource.ConfigRestore) return;
            OnEntrySelected(msg.Entry);
        });
        _trailSettingsSub = _broker?.Subscribe<TrailSettingsChangedMsg>(_ => RecreatePreviewTrails());
        _trailMatEditSub = _broker?.Subscribe<TrailMaterialEditedMsg>(msg => PushToGrabSaberTrail(msg.Material));
        _configLoadingSub = _broker?.Subscribe<ConfigLoadingMsg>(_ => OnConfigLoading());
        _configLoadedSub = _broker?.Subscribe<ConfigLoadedMsg>(_ => OnConfigLoaded());
    }

    public void SuspendEditor()
    {
        if (!IsActive) return;
        _log.Debug($"SuspendEditor: activeEntry={_previewSession.ActiveEntry?.DisplayName}");
        try
        {
            TeardownSubscriptions();

            _trailPreviewer?.Destroy();
            _previewSession.WipePreviews();
            _grip.SetGripVisible(SaberHand.Left, true);
            _grip.SetGripVisible(SaberHand.Right, true);
        }
        catch (Exception ex)
        {
            _log.Error($"SuspendEditor failed: {ex}");
        }
    }

    public void ActivateEditor()
    {
        if (IsActive) return;
        _log.Debug($"ActivateEditor: phase={_session.Phase} grabL={GrabLeft} grabR={GrabRight} activeEntry={_previewSession.ActiveEntry?.DisplayName}");
        _session.TransitionTo(SessionPhase.Editing);

        RestoreGrabStateFromConfig();

        _previewSession.ResetEditorReady();

        if (_previewSession.CanResume && LoadoutEntry == _previewSession.ActiveEntry)
        {
            _log.Info($"ActivateEditor: resuming suspended saber '{_previewSession.ActiveEntry!.DisplayName}'");
            ErrorBoundary.FireAndForget(ResumeEditorAsync(), _log, "ResumeEditor");
        }
        else
        {
            if (_previewSession.CanResume)
            {
                _log.Debug("ActivateEditor: loadout mismatch, wiping suspended state");
                _previewSession.WipePreviews();
            }
            ErrorBoundary.FireAndForget(ActivateEditorAsync(), _log, nameof(ActivateEditor));
        }
    }

    private async Task ActivateEditorAsync()
    {
        try
        {
            SubscribeToEditorMessages();

            if (_configManager.CurrentTask is not null)
            {
                _log.Debug("ActivateEditorAsync: awaiting ConfigManager.CurrentTask");
                await _configManager.CurrentTask;
            }
            if (!IsActive) return;

            _log.Debug("ActivateEditorAsync: ensuring assets valid");
            await _configManager.EnsureAssetsValidAsync();
            if (!IsActive) return;

            if (_previewSession.ActiveEntry is null)
            {
                if (_loadout.Left.TryGetSaberAsset(out var saberAsset) && saberAsset!.OwnerEntry is not null)
                {
                    _log.Debug($"ActivateEditorAsync: selecting restored entry '{saberAsset.OwnerEntry.DisplayName}'");
                    _previewSession.SelectRestoredEntry(saberAsset.OwnerEntry);
                }
                else
                {
                    _log.Debug("ActivateEditorAsync: no entry to select (loadout empty)");
                }
            }
            else
            {
                if (_loadout.Left.TryGetSaberAsset(out var sa) && sa?.OwnerEntry is not null
                && sa.OwnerEntry != _previewSession.ActiveEntry)
                {
                    _log.Debug($"ActivateEditorAsync: re-resolving refreshed entry '{sa.OwnerEntry.DisplayName}'");
                    _previewSession.SelectRestoredEntry(sa.OwnerEntry);
                }
                else
                {
                    _log.Debug($"ActivateEditorAsync: reloading existing entry '{_previewSession.ActiveEntry.DisplayName}'");
                    _previewSession.Reload();
                }
            }
        }
        finally
        {
            _previewSession.SignalEditorReady();
        }
    }

    private async Task ResumeEditorAsync()
    {
        _isResuming = true;
        try
        {
            SubscribeToEditorMessages();
            _previewSession.ResumePreviews();
            var sabers = _previewSession.Sabers;

            sabers.Left?.DestroyTrail(true);
            sabers.Right?.DestroyTrail(true);

            if (GrabLeft && sabers.Left is not null)
            {
                _grip.Attach(sabers.Left, SaberHand.Left);
                sabers.Left.CreateTrail(editorMode: true);
            }
            if (GrabRight && sabers.Right is not null)
            {
                _grip.Attach(sabers.Right, SaberHand.Right);
                sabers.Right.CreateTrail(editorMode: true);
            }

            sabers.Left?.SetColor(ColorForHand(SaberHand.Left));
            sabers.Right?.SetColor(ColorForHand(SaberHand.Right));
            UpdateSaberVisibility();

            await Task.Yield();
            if (!IsActive) return;

            _broker?.Publish(new SaberPreviewInstantiatedMsg(
            sabers[_previewSession.FocusedHand]!,
            _previewSession.FocusedHand));
        }
        finally
        {
            _isResuming = false;
            _previewSession.SignalEditorReady();
        }
    }

    public void DeactivateEditor()
    {
        if (!IsActive || _isResuming) return;
        _log.Debug($"DeactivateEditor: phase={_session.Phase} activeEntry={_previewSession.ActiveEntry?.DisplayName}");
        try
        {
            TeardownSubscriptions();

            _trailPreviewer?.Destroy();
            _previewSession.WipePreviews();
            _grip.SetGripVisible(SaberHand.Left, true);
            _grip.SetGripVisible(SaberHand.Right, true);
        }
        catch (System.Exception ex)
        {
            _log.Error($"DeactivateEditor failed: {ex}");
        }
    }

    private void OnConfigLoading()
    {
        if (!IsActive) return;
        _log.Debug($"OnConfigLoading: wiping previews, activeEntry={_previewSession.ActiveEntry?.DisplayName}");
        CancelSpawn();
        _trailPreviewer?.Destroy();
        _previewSession.WipePreviews();
    }

    private void OnConfigLoaded()
    {
        if (!IsActive) return;

        bool wasLeft = GrabLeft;
        bool wasRight = GrabRight;
        RestoreGrabStateFromConfig();

        if (_loadout.Left.TryGetSaberAsset(out var sa) && sa?.OwnerEntry is not null)
        {
            _log.Info($"OnConfigLoaded: Resolved entry='{sa.OwnerEntry.DisplayName}', GrabLeft={GrabLeft}, GrabRight={GrabRight}");
            _previewSession.SelectRestoredEntry(sa.OwnerEntry);
        }
        else
        {
            if (wasLeft && !GrabLeft) _grip.SetGripVisible(SaberHand.Left, true);
            if (wasRight && !GrabRight) _grip.SetGripVisible(SaberHand.Right, true);
        }
    }

    private void OnEntrySelected(SaberAssetEntry entry)
    {
        CancelSpawn();
        _spawnCts = new();
        ErrorBoundary.FireAndForget(OnEntrySelectedAsync(entry, _spawnCts.Token), _log, nameof(OnEntrySelected));
    }

    private async Task OnEntrySelectedAsync(SaberAssetEntry entry, CancellationToken token)
    {
        var sabers = _previewSession.Sabers;
        _log.Debug($"OnEntrySelectedAsync: entry='{entry.DisplayName}' stale={entry.IsAssetStale}");

        if (entry.IsAssetStale)
        {
            await _configManager.EnsureAssetsValidAsync();
            if (token.IsCancellationRequested) return;

            if (_loadout.Left.TryGetSaberAsset(out var sa) && sa?.OwnerEntry is not null
            && !sa.OwnerEntry.IsAssetStale)
            {
                entry = sa.OwnerEntry;
                _previewSession.SelectRestoredEntry(entry);
                return;
            }
        }

        sabers.Clear();

        await Task.Yield();
        if (!IsActive || token.IsCancellationRequested) return;

        var leftMount = GrabLeft ? _grip.GetMount(SaberHand.Left) : null;
        var rightMount = GrabRight ? _grip.GetMount(SaberHand.Right) : null;
        await _previewSession.SpawnPairAsync(_loadout, leftMount, rightMount);

        ConfigureHand(SaberHand.Left, GrabLeft, sabers.Left);
        ConfigureHand(SaberHand.Right, GrabRight, sabers.Right);

        UpdateSaberVisibility();

        await Task.Yield();
        if (!IsActive || token.IsCancellationRequested)
        {
            sabers.Clear();
            return;
        }

        _broker?.Publish(new SaberPreviewInstantiatedMsg(
        sabers[_previewSession.FocusedHand]!,
        _previewSession.FocusedHand));

        await Task.Yield();
        if (!IsActive || token.IsCancellationRequested) return;

        _animationCts?.Cancel();
        _animationCts?.Dispose();
        _animationCts = new();
        var animToken = _animationCts.Token;
        if (_settings.AnimateSelection && IsSaberInHand)
        {
            var tasks = new System.Collections.Generic.List<Task>();
            if (GrabLeft && _grip.LeftMount != null)
            {
                var mount = _grip.LeftMount;
                tasks.Add(UITransitionAnimator.ScaleTransitionAsync(0.3f, animToken, t => { if (mount) mount.localScale = new Vector3(t, t, t); }));
            }
            if (GrabRight && _grip.RightMount != null)
            {
                var mount = _grip.RightMount;
                tasks.Add(UITransitionAnimator.ScaleTransitionAsync(0.3f, animToken, t => { if (mount) mount.localScale = new Vector3(t, t, t); }));
            }
            if (tasks.Count is > 0)
            {
                await Task.WhenAll(tasks);
            }
        }
    }

    private void ConfigureHand(SaberHand hand, bool grabbing, LiveSaber? saber)
    {
        if (saber is null) return;
        _log.Debug($"ConfigureHand: {hand} grabbing={grabbing}");

        if (grabbing)
        {
            _grip.Attach(saber, hand);
            saber.CreateTrail(editorMode: true);
        }
        else
        {
            _grip.SetGripVisible(hand, true);
        }

        saber.SetColor(ColorForHand(hand));
    }

    public void UpdateSaberVisibility()
    {
        var sabers = _previewSession.Sabers;
        var focusedHand = _previewSession.FocusedHand;
        foreach (var hand in (SaberHand[])[SaberHand.Left, SaberHand.Right])
        {
            var saber = sabers[hand];
            if (saber?.GameObject == null) continue;

            bool isFocused = hand == focusedHand;
            bool isGrabbed = hand == SaberHand.Left ? GrabLeft : GrabRight;
            saber.GameObject.SetActive(isFocused || isGrabbed);
        }
    }

    private void RestoreGrabStateFromConfig()
    {
        var grabSels = _settings?.GrabSelections;
        if (grabSels is null) return;
        GrabLeft = grabSels.Contains(0);
        GrabRight = grabSels.Contains(1);
    }

    private Color ColorForHand(SaberHand hand)
    {
        var scheme = _playerDataModel.playerData.colorSchemesSettings.GetSelectedColorScheme();
        return hand == SaberHand.Left ? scheme.saberAColor : scheme.saberBColor;
    }
}