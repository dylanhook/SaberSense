// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Catalog.Model;
using SaberSense.Configuration;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Core.Utilities;
using SaberSense.Customization;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Menu.Popups;
using SaberSense.GUI.Menu.Controllers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Menu.Tabs;

internal sealed partial class SaberTabView(
SaberSelectionController selectionController,
SaberCatalogController catalogController,
SaberTransformController transformController,
PreviewController previewController,
ModSettings settings,
PreviewSession previewSession,
SaberSense.GUI.TrailVisualizationRenderer trailPreviewer,
SaberSense.Customization.SaberEditor editor,
SaberCatalog catalog,
IMessageBroker broker,
IModLogger log) : IMenuTab, ISaberSelectorTab
{
    public string Title => "Sabers";

    public string IconPath => IconPaths.Saber;

    private readonly IModLogger _log = log.ForSource(nameof(SaberTabView));
    private readonly BindingScope _bindingScope = new();

    private UIScrollList _saberList = null!;
    private BaseButton _favoriteButton = null!;
    private UIMultiComboBox _grabSaberCombo = null!;
    private UIMultiComboBox _transformSabersCombo = null!;
    private UISlider _saberWidthSlider = null!;
    private UISlider _saberLengthSlider = null!;
    private UISlider _rotationSlider = null!;
    private UISlider _offsetSlider = null!;
    private float _activeSaberLength = 1f;
    private float _activeSaberOffset = 0f;

    private RectTransform _canvasRoot = null!;
    private MessagePopup _messagePopup = null!;
    private UILoadingOverlay _loadingOverlay = null!;
    private GameObject? _previewWindowGO;

    private TaskCompletionSource<bool>? _spawnComplete;

    private IDisposable? _equippedSub;
    private IDisposable? _settingsChangedSub;
    private IDisposable? _spawnedSub;
    private IDisposable? _configLoadingSub;
    private IDisposable? _configLoadedSub;
    private IDisposable? _loadProgressSub;
    private IDisposable? _loadCompletedSub;
    private IDisposable? _scanProgressSub;
    private bool _externalLoadOverlayActive;

    public GameObject Build(MenuTabContext ctx)
    {
        _canvasRoot = ctx.CanvasRoot;
        _messagePopup = ctx.MessagePopup;
        _loadingOverlay = new UILoadingOverlay(ctx.CanvasRoot);
        _previewWindowGO = ctx.PreviewWindowGO;

        var tabRoot = UILayoutFactory.TabRoot("SaberTab", ctx.Parent);

        var root = new HBox("SaberCols").SetParent(tabRoot);
        UnityEngine.Object.Destroy(root.GameObject.GetComponent<ContentSizeFitter>());
        root.SetSpacing(UITheme.ColumnGap).SetPadding(0, 0, 0, 0);
        root.AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var listCol = new VBox("ListCol").SetParent(root.RectTransform).SetAlignment(TextAnchor.UpperLeft);
        listCol.SetPadding(0, 0, 0, 0).SetSpacing(UITheme.GroupGap)
        .AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);
        UnityEngine.Object.Destroy(listCol.GameObject.GetComponent<ContentSizeFitter>());

        _saberList = new UIScrollList("SaberList", "Available sabers").SetParent(listCol.RectTransform);
        _saberList.AddLayoutElement(flexibleHeight: 1);
        _saberList.EnableSearch(_canvasRoot);
        _saberList.OnSelect((idx, data) => ErrorBoundary.FireAndForget(SaberSelectedAsync(idx, data), _log, nameof(SaberSelectedAsync)));

        LoadSabers();

        BuildRightColumn(root.RectTransform);
        BindEvents();

        return tabRoot.gameObject;
    }

    private void BindEvents()
    {
        _equippedSub = broker?.Subscribe<SaberEquippedMsg>(msg => OnEquipped(msg.Entry))!;
        _spawnedSub = broker?.Subscribe<SaberPreviewInstantiatedMsg>(_ => _spawnComplete?.TrySetResult(true))!;
        _loadProgressSub = broker?.Subscribe<SaberLoadProgressMsg>(msg => OnLoadProgress(msg.Phase, msg.Progress))!;
        _loadCompletedSub = broker?.Subscribe<SaberLoadCompletedMsg>(_ => OnLoadComplete())!;
        _scanProgressSub = broker?.Subscribe<ScanProgressMsg>(msg =>
        {
            if (msg.Discovered > 0)
            _loadingOverlay.SetPhase($"Scanning saber catalogue ({msg.Completed}/{msg.Discovered})...",
            (float)msg.Completed / msg.Discovered);
        })!;
        _configLoadingSub = broker?.Subscribe<ConfigLoadingMsg>(_ =>
        {
            ErrorBoundary.FireAndForget(ShowExternalLoadOverlayAsync(), _log, "ConfigLoadingOverlay");
        });
        _configLoadedSub = broker?.Subscribe<ConfigLoadedMsg>(_ => HideExternalLoadOverlay());
        _settingsChangedSub = broker?.Subscribe<SettingsChangedMsg>(_ =>
        {
            ErrorBoundary.FireAndForget(ShowSabersAsync(), _log, nameof(ShowSabersAsync));

            if (editor is not null && settings?.GrabSelections is not null)
            {
                HashSet<int> grab = [.. settings.GrabSelections];
                editor.SetGrab(grab.Contains(0), grab.Contains(1));
            }
            _previewWindowGO?.SetActive(settings?.Editor?.PreviewSaber ?? true);
        })!;
    }

    public void Refresh()
    {
        var entry = selectionController.SelectedEntry;

        if (_favoriteButton is not null)
        {
            bool pinned = entry?.IsPinned ?? false;
            _favoriteButton.Label.SetText(pinned ? "Unpin saber" : "Pin saber");
            _favoriteButton.NormalSprite = pinned ? UIGradient.AccentVert : UIGradient.BtnNormal;
            _favoriteButton.HoverSprite = pinned ? UIGradient.AccentVert : UIGradient.BtnHover;
            _favoriteButton.PressedSprite = pinned ? UIGradient.AccentVert : UIGradient.BtnPressed;
            _favoriteButton.GradientOverlay.SetSprite(_favoriteButton.NormalSprite);
            if (!pinned) _favoriteButton.Label.SetColor(Color.white);
        }

        if (entry is null) return;
        _saberWidthSlider?.SetRange(0, settings?.MaxGlobalWidth ?? 5);

        transformController.SyncFromActiveSaber();
    }

    public float ActiveSaberLength => _activeSaberLength;

    public float ActiveSaberOffset => _activeSaberOffset;

    public void OnSaberPreviewInstantiated(SaberSense.Rendering.LiveSaber liveSaber)
    {
        _activeSaberLength = liveSaber?.Profile?.Scale.Length ?? 1f;
        _activeSaberOffset = 0f;
    }

    private void OnEquipped(SaberAssetEntry entry)
    {
        if (entry is not null && _saberList is not null && catalog is not null)
        {
            var preview = catalog.FindPreviewForEntry(entry);
            if (preview is not null) _saberList.Select(preview.DisplayName, false);
        }
        HideExternalLoadOverlay();
        Refresh();
    }

    private async Task ShowExternalLoadOverlayAsync()
    {
        if (_loadingOverlay is null) return;
        _externalLoadOverlayActive = true;
        await _loadingOverlay.ShowAsync();
    }

    private void HideExternalLoadOverlay()
    {
        if (!_externalLoadOverlayActive) return;
        _externalLoadOverlayActive = false;
        _loadingOverlay?.Hide();
    }

    private void LoadSabers()
    {
        ErrorBoundary.FireAndForget(LoadSabersAsync(), _log, nameof(LoadSabers));
    }

    private async Task LoadSabersAsync()
    {
        if (catalog is not null)
        {
            await _loadingOverlay.ShowAsync();
            _loadingOverlay.SetPhase("Scanning saber catalogue...", 0f);
            await catalog.PreparePreviewsAsync();
            _loadingOverlay.Hide();
        }
        await ShowSabersAsync(false);

        editor?.ActivateEditor();
    }

    public void UpdateCellIcon(object userData, UnityEngine.Sprite icon) => _saberList?.UpdateCellIcon(userData, icon);

    public async Task ShowSabersAsync(bool scrollToTop = false)
    {
        if (catalog is null || catalogController.Folders is null) return;

        await previewSession.EditorReady;

        _saberList.SetItems(BuildSortedListItems());

        var current = previewSession?.ActiveEntry;

        var loadoutEntry = editor?.LoadoutEntry;
        if (loadoutEntry is not null && (current is null || !ReferenceEquals(loadoutEntry, current)))
        current = loadoutEntry;

        if (editor?.IsLoadoutEmpty == true)
        {
            previewSession?.WipePreviews();
            trailPreviewer?.Destroy();
            _saberList.Deselect();
            _grabSaberCombo?.SetSelected([]);
            _transformSabersCombo?.SetSelected([]);

            transformController.State.ResetToDefaults();
            _activeSaberLength = 1f;
            _activeSaberOffset = 0f;
            previewController?.SetTitle("");
        }
        else if (current is not null)
        {
            await selectionController.SelectAsync(current);
            var preview = catalog.FindPreviewForEntry(current);
            if (preview is not null) _saberList.Select(preview.DisplayName, !scrollToTop);
            if (previewController.TitleLabel is not null && current is ISaberListItem ci)
            previewController.SetTitle(ci.DisplayName.ToUpper());
        }
        else
        {
            trailPreviewer?.Destroy();
            _saberList.Deselect();
            _grabSaberCombo?.SetSelected([]);
            _transformSabersCombo?.SetSelected([]);
            transformController.State.ResetToDefaults();
            _activeSaberLength = 1f;
            _activeSaberOffset = 0f;
            previewController?.SetTitle("");
        }

        if (scrollToTop) _saberList.ScrollTo(0);
        Refresh();
    }

    private async Task SaberSelectedAsync(int _, UIListCellData data)
    {
        try
        {
            if (data.UserData is FolderItem dir)
            {
                catalogController.Folders!.Navigate(dir.DisplayName);
                _saberList.Deselect();
                await ShowSabersAsync(true);
                return;
            }

            if (!Plugin.MultiPassEnabled && data.UserData is AssetPreview preview && !preview.IsSPICompatible)
            {
                _messagePopup?.Show("This saber requires multi-pass\nrendering to be enabled.\n\nEnable it in Mod Settings \u2192 Asset Bundles.");
                _saberList.Deselect();
                return;
            }

            if (data.UserData is ISaberListItem listItem)
            {
                previewController.SetTitle(listItem.DisplayName.ToUpper());
            }

            SaberAssetEntry? entry = null;
            bool showedOverlay = false;

            if (data.UserData is AssetPreview assetPreview)
            {
                if (catalog?.TryGetLoaded(assetPreview.RelativePath) is null && _loadingOverlay is not null)
                {
                    await _loadingOverlay.ShowAsync();
                    showedOverlay = true;
                }
                entry = await selectionController.ResolveAsync(assetPreview);
            }
            else if (data.UserData is SaberAssetEntry assetEntry)
            entry = assetEntry;
            else return;

            if (entry is null)
            {
                if (showedOverlay) _loadingOverlay?.Hide();
                return;
            }

            if (entry == selectionController.SelectedEntry)
            {
                if (showedOverlay) _loadingOverlay?.Hide();
                return;
            }

            if (!Plugin.MultiPassEnabled && !entry.IsSPICompatible)
            {
                if (showedOverlay) _loadingOverlay?.Hide();
                _messagePopup?.Show("This saber requires multi-pass\nrendering to be enabled.\n\nEnable it in Mod Settings \u2192 Asset Bundles.");
                _saberList.Deselect();
                return;
            }

            if (!showedOverlay && _loadingOverlay is not null)
            await _loadingOverlay.ShowAsync();

            _loadingOverlay?.SetPhase("Equipping saber...", 0.7f);

            try
            {
                _spawnComplete = new();
                await selectionController.SelectAsync(entry);
                _loadingOverlay?.SetPhase("Spawning preview...", 0.85f);
                await Task.WhenAny(_spawnComplete.Task, Task.Delay(10000));
            }
            finally
            {
                _loadingOverlay?.Hide();
            }
        }
        catch (Exception ex) { _log.Error($"SaberSelected failed: {ex}"); }
    }

    private async Task TogglePinAsync(bool isOn)
    {
        try
        {
            var entry = selectionController.SelectedEntry;
            if (entry is null) return;
            catalogController.SetPinned(entry, isOn);
            Refresh();
            await ShowSabersAsync();
        }
        catch (Exception ex) { _log.Error($"ToggledFavorite failed: {ex}"); }
    }

    private void OnLoadProgress(string phase, float progress)
    {
        if (_loadingOverlay is not null && !_loadingOverlay.IsVisible && !_externalLoadOverlayActive)
        {
            _externalLoadOverlayActive = true;
            ErrorBoundary.FireAndForget(_loadingOverlay.ShowAsync(), _log, "AutoLoadOverlay");
        }
        _loadingOverlay?.SetPhase(phase, progress);
    }

    private void OnLoadComplete()
    {
        if (_externalLoadOverlayActive)
        HideExternalLoadOverlay();
    }

    public void Dispose()
    {
        _bindingScope.Dispose();
        _equippedSub?.Dispose();
        _spawnedSub?.Dispose();
        _settingsChangedSub?.Dispose();
        _configLoadingSub?.Dispose();
        _configLoadedSub?.Dispose();
        _loadProgressSub?.Dispose();
        _loadCompletedSub?.Dispose();
        _scanProgressSub?.Dispose();
        _loadingOverlay?.Dispose();
    }
}