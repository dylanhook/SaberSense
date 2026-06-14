// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.AssetPipeline;
using SaberSense.Catalog;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Core.Utilities;
using SaberSense.Customization;
using SaberSense.GUI.Menu.Controllers;
using SaberSense.GUI.Menu.Tabs;
using SaberSense.Loadout;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.GUI.Menu;

internal sealed class MenuEventWiring(
IMessageBroker broker,
HotReloadWatcher saberFileWatcher,
SaberEditor editor,
SaberCatalogService catalogService,
IModLogger log) : IDisposable
{
    private readonly IMessageBroker _broker = broker;
    private readonly HotReloadWatcher _saberFileWatcher = saberFileWatcher;
    private readonly SaberEditor _editor = editor;
    private readonly SaberCatalogService _catalogService = catalogService;
    private readonly IModLogger _log = log.ForSource(nameof(MenuEventWiring));

    private IDisposable? _previewInstantiatedSub;
    private IDisposable? _coverGeneratedSub;
    private IDisposable? _previewsWipedSub;

    private PreviewController? _previewController;
    private ISaberSelectorTab? _saberTab;
    private ITrailTab? _trailTab;
    private IModifierTab? _modifierTab;
    private SaberCatalogController? _catalogController;
    private SaberCatalog? _catalog;
    private MonoBehaviour? _host;

    public void Wire(
    PreviewController previewController,
    SaberCatalogController catalogController,
    IReadOnlyList<IMenuTab> tabs,
    SaberCatalog catalog,
    MonoBehaviour host)
    {
        _previewController = previewController;
        _catalogController = catalogController;
        _saberTab = tabs.OfType<ISaberSelectorTab>().FirstOrDefault();
        _trailTab = tabs.OfType<ITrailTab>().FirstOrDefault();
        _modifierTab = tabs.OfType<IModifierTab>().FirstOrDefault();
        _catalog = catalog;
        _host = host;

        _previewInstantiatedSub = _broker.Subscribe<SaberPreviewInstantiatedMsg>(
        msg => OnSaberPreviewInstantiated(msg.Saber, msg.Hand));
        _coverGeneratedSub = _broker.Subscribe<CoverGeneratedMsg>(OnCoverGenerated);
        _previewsWipedSub = _broker.Subscribe<PreviewsWipedMsg>(_ => OnPreviewsWiped());

        if (_saberFileWatcher is not null)
        {
            _saberFileWatcher.OnSaberFileChanged += OnSaberFileUpdate;
            _saberFileWatcher.OnFolderChanged += OnFolderStructureChanged;
            _saberFileWatcher.OnWatcherReset += OnWatcherReset;
            _saberFileWatcher.StartMonitoring();
        }

        _editor?.ActivateEditor();

        if (_previewController?.SaberPreview is not null && _editor is not null)
        {
            _previewController.SaberPreview.OnDragStarted = () => _editor.OnPreviewDragStarted();
            _previewController.SaberPreview.OnDragEnded = () => _editor.OnPreviewDragEnded();
        }

        if (_previewController is not null)
        _previewController.OnFocusedSaberChanged += OnFocusedSaberChanged;
    }

    public void Dispose()
    {
        if (_previewController is not null)
        _previewController.OnFocusedSaberChanged -= OnFocusedSaberChanged;

        if (_saberFileWatcher is not null)
        {
            _saberFileWatcher.OnSaberFileChanged -= OnSaberFileUpdate;
            _saberFileWatcher.OnFolderChanged -= OnFolderStructureChanged;
            _saberFileWatcher.OnWatcherReset -= OnWatcherReset;
            _saberFileWatcher.StopMonitoring();
        }

        _previewInstantiatedSub?.Dispose();
        _coverGeneratedSub?.Dispose();
        _previewsWipedSub?.Dispose();

        _editor?.SuspendEditor();
    }

    private void OnSaberPreviewInstantiated(SaberSense.Rendering.LiveSaber liveSaber, SaberSense.Core.SaberHand hand)
    {
        _saberTab?.OnSaberPreviewInstantiated(liveSaber);
        _previewController?.OnSaberPreviewInstantiated(liveSaber, hand);
        _saberTab?.Refresh();
        _trailTab?.Refresh();
        _modifierTab?.RefreshModifiers();

        ErrorBoundary.FireAndForget(DeferredMaterialRefreshAsync(), _log);
    }

    private async Task DeferredMaterialRefreshAsync()
    {
        await Task.Yield();
        if (_host == null) return;
        _modifierTab?.RefreshMaterials();
    }

    private void OnPreviewsWiped()
    {
        _modifierTab?.RefreshModifiers();
        _modifierTab?.RefreshMaterials();
    }

    private void OnCoverGenerated(CoverGeneratedMsg msg)
    {
        _saberTab?.UpdateCellIcon(msg.Preview, msg.Preview.CoverImage!);
    }

    private void OnSaberFileUpdate(string filename, FileChangeKind kind)
    {
        if (_host == null) return;
        ErrorBoundary.FireAndForget(
        _catalogService.HandleFileChangeAsync(filename, kind,
        async () => { if (_host != null) await _saberTab!.ShowSabersAsync(); }),
        _log, nameof(OnSaberFileUpdate));
    }

    private void OnWatcherReset()
    {
        if (_host == null) return;
        ErrorBoundary.FireAndForget(
        _catalogService.ReconcileAsync(
        async () => { if (_host != null) await _saberTab!.ShowSabersAsync(); }),
        _log, nameof(OnWatcherReset));
    }

    private void OnFolderStructureChanged()
    {
        if (_host == null || _catalog is null) return;
        _catalog.DiscoverExternalFolders();
        var dirManager = new FolderNavigator(_catalog.ExternalSearchPaths);
        _catalogController!.Init(dirManager);
        ErrorBoundary.FireAndForget(
        _saberTab!.ShowSabersAsync(),
        _log, nameof(OnFolderStructureChanged));
    }

    private void OnFocusedSaberChanged()
    {
        _modifierTab?.RefreshModifiers();
        _trailTab?.Refresh();
    }
}