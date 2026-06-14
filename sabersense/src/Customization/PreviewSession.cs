// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog.Model;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Loadout;
using SaberSense.Profiles;
using SaberSense.Rendering;
using SaberSense.Rendering.Materials;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Customization;
internal sealed class PreviewSession : IDisposable
{
    public SaberPair Sabers { get; } = new();

    private bool _suspended;

    public SaberHand FocusedHand { get; set; } = SaberHand.Left;

    public LiveSaber? FocusedSaber => Sabers[FocusedHand];

    public PieceRenderer? ActiveRenderer { get; private set; }
    public SaberAssetEntry? ActiveEntry { get; private set; }

    public Task EditorReady => _editorReadyTcs.Task;
    private TaskCompletionSource<bool> _editorReadyTcs = new();

    private readonly IModLogger _logger;
    private readonly LiveSaber.Factory _liveSaberCreator;
    private readonly SaberLoadout _loadout;
    private readonly LoadoutCoordinator _coordinator;
    private readonly SharedMaterialPool _materialPool;

    internal SharedMaterialPool MaterialPool => _materialPool;
    private readonly IMessageBroker _broker;

    public PreviewSession(
    IModLogger logger,
    SaberLoadout loadout,
    LiveSaber.Factory liveSaberCreator,
    LoadoutCoordinator coordinator,
    IMessageBroker broker,
    SharedMaterialPool materialPool)
    {
        _logger = logger.ForSource(nameof(PreviewSession));
        _loadout = loadout;
        _liveSaberCreator = liveSaberCreator;
        _coordinator = coordinator;
        _broker = broker;
        _materialPool = materialPool;

        _onPreviewSaberChanged = entry =>
        {
            if (entry == ActiveEntry)
            Reload();
            else
            SelectEntry(entry);
        };

        _previewChangedSub = broker?.Subscribe<PreviewSaberChangedMsg>(msg => _onPreviewSaberChanged(msg.Entry));
    }

    private readonly Action<SaberAssetEntry> _onPreviewSaberChanged;
    private readonly IDisposable? _previewChangedSub;

    public void Dispose()
    {
        _previewChangedSub?.Dispose();
    }

    public void SelectEntry(SaberAssetEntry entry)
    => SelectEntryInternal(entry, EquipSource.UserSelection);

    public void SelectRestoredEntry(SaberAssetEntry entry)
    => SelectEntryInternal(entry, EquipSource.ConfigRestore);

    private void SelectEntryInternal(SaberAssetEntry entry, EquipSource source)
    {
        if (entry == ActiveEntry) return;
        _logger.Debug($"{source}: '{ActiveEntry?.DisplayName}' -> '{entry?.DisplayName}'");
        TeardownCurrent();
        ActiveEntry = entry;
        if (entry is not null)
        Core.Utilities.ErrorBoundary.FireAndForget(
        ActivateNewAsync(entry, source), _logger, $"{nameof(PreviewSession)}.{source}");
    }

    private void TeardownCurrent()
    {
        if (ActiveEntry is null) return;
        _logger.Debug($"TeardownCurrent: '{ActiveEntry.DisplayName}'");
        ActiveEntry.DestroyAuxObjects();
    }

    private async Task ActivateNewAsync(SaberAssetEntry entry, EquipSource source)
    {
        await _coordinator.EquipAsync(entry, source);
        _logger.Info($"Equipped [{entry.DisplayName}] for preview (source: {source})");
    }

    public void SignalEditorReady()
    {
        _editorReadyTcs.TrySetResult(true);
    }

    public void ResetEditorReady()
    {
        if (_editorReadyTcs.Task.IsCompleted)
        _editorReadyTcs = new();
    }

    public void Reload()
    {
        if (ActiveEntry is null) return;
        _logger.Debug($"Reload: re-selecting '{ActiveEntry.DisplayName}'");
        var entry = ActiveEntry;
        TeardownCurrent();
        ActiveEntry = null;
        SelectEntry(entry);
    }

    public void SynchronizeScale()
    {
        if (FocusedSaber is null) return;
        _loadout.SyncDimensions(FocusedSaber.Profile);
    }

    internal static readonly Vector3 StashPosition = new(0, -2000, 0);

    public async Task SpawnPairAsync(SaberLoadout loadout, Transform? leftParent, Transform? rightParent)
    {
        _logger.Debug($"SpawnPair: leftParent={(leftParent != null ? leftParent.name : "null")} rightParent={(rightParent != null ? rightParent.name : "null")}");
        _materialPool.Clear(MaterialPoolOwner.Menu);
        Sabers.Left = _liveSaberCreator.Create(loadout.Left, MaterialPoolOwner.Menu);
        if (leftParent != null) Sabers.Left.SetParent(leftParent);
        else Sabers.Left.CachedTransform.position = StashPosition;

        await Task.Yield();

        Sabers.Right = _liveSaberCreator.Create(loadout.Right, MaterialPoolOwner.Menu);
        if (rightParent != null) Sabers.Right.SetParent(rightParent);
        else Sabers.Right.CachedTransform.position = StashPosition;

        ActiveRenderer = FocusedSaber?.Renderer;
    }

    public void WipePreviews()
    {
        _suspended = false;
        _logger.Debug($"WipePreviews: active='{ActiveEntry?.DisplayName}' leftSaber={(Sabers.Left is not null)} rightSaber={(Sabers.Right is not null)}");
        TeardownCurrent();
        Sabers.Clear();
        ActiveRenderer = null;
        ActiveEntry = null;
        _broker?.Publish(new PreviewsWipedMsg());
    }

    public void SuspendPreviews()
    {
        TeardownCurrent();

        foreach (var saber in new[] { Sabers.Left, Sabers.Right })
        {
            if (saber?.GameObject == null) continue;
            saber.CachedTransform.SetParent(null, false);
            saber.CachedTransform.position = StashPosition;
            saber.GameObject.SetActive(false);
        }
        _suspended = true;
    }

    public bool CanResume =>
    _suspended
    && ActiveEntry is not null
    && !ActiveEntry.IsAssetStale
    && Sabers.Left is not null && Sabers.Left.GameObject
    && Sabers.Right is not null && Sabers.Right.GameObject;

    public void ResumePreviews()
    {
        if (!_suspended) return;
        Sabers.Left?.GameObject?.SetActive(true);
        Sabers.Right?.GameObject?.SetActive(true);
        ActiveRenderer = FocusedSaber?.Renderer;
        _suspended = false;
    }

    internal void RefreshActiveRenderer() => ActiveRenderer = FocusedSaber?.Renderer;
}