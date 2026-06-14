// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Catalog.Model;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Menu.Controllers;
using SaberSense.Rendering;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.GUI.Menu.Popups;

internal sealed class ChooseTrailPopup
{
    private const float ModalWidth = 85f;
    private const float ModalHeight = 75f;
    private const float CellHeight = 8.5f;

    private readonly UIModal _modal;
    private readonly UIScrollList _list;
    private readonly SaberCatalog _catalog;
    private readonly MessagePopup? _messagePopup;

    private Action<TrailSettings?, List<SaberSense.Rendering.SaberTrailMarker>?>? _onSelectionChanged;

    private IEnumerable<AssetPreview> _allPreviews = [];
    private SortMode _sortMode;
    private FolderNavigator _folders = null!;
    private IReadOnlyList<string>? _folderPaths;
    private string? _currentTrailOriginPath;

    public ChooseTrailPopup(RectTransform canvasRoot, SaberCatalog catalog, MessagePopup? messagePopup = null)
    {
        _catalog = catalog;
        _messagePopup = messagePopup;
        _modal = new UIModal("Choose trail", canvasRoot, ModalWidth, ModalHeight);

        _list = new UIScrollList("TrailList");
        _list.SetCellHeight(CellHeight);
        _list.RectTransform.SetParent(_modal.ContentArea.RectTransform, false);
        _list.AddLayoutElement(flexibleHeight: 1);
        _list.EnableSearch(canvasRoot);

        _list.OnSelect(async (index, data) =>
        {
            if (data.UserData is FolderItem dir)
            {
                _folders.Navigate(dir.DisplayName);
                _list.Deselect();
                RebuildList();
                return;
            }

            if (data.UserData is AssetPreview preview)
            {
                if (!SaberSense.Plugin.MultiPassEnabled && !preview.IsSPICompatible)
                {
                    _messagePopup?.Show("This saber requires multi-pass\nrendering to be enabled.\n\nEnable it in Mod Settings \u2192 Asset Bundles.");
                    return;
                }

                var entry = await _catalog[preview];
                if (entry?.LeftPiece is SaberSense.Catalog.Model.SaberAssetDefinition definition)
                {
                    var tm = definition.ExtractTrail(true);
                    var tl = SaberComponentDiscovery.GetTrails(definition.Prefab);
                    _onSelectionChanged?.Invoke(tm, tl);
                    _modal.Hide();
                }
            }
        });

        _modal.AddButtons("Original", () =>
        {
            _onSelectionChanged?.Invoke(null, null);
            _list.Deselect();
            _modal.Hide();
        }, "Close", () => _modal.Hide());
        _modal.ButtonsRow!.LayoutGroup!.childForceExpandWidth = true;
    }

    public void Show(IEnumerable<AssetPreview> previews, SortMode sortMode,
    IReadOnlyList<string> folderPaths,
    Action<TrailSettings?, List<SaberSense.Rendering.SaberTrailMarker>?> onSelectionChanged,
    string? currentTrailOriginPath = null)
    {
        _onSelectionChanged = onSelectionChanged;
        _allPreviews = previews;
        _sortMode = sortMode;
        _folderPaths = folderPaths;
        _folders = new FolderNavigator(folderPaths ?? []);

        _currentTrailOriginPath = currentTrailOriginPath;
        RebuildList();
        _modal.Show();
    }

    public void Exit()
    {
        _modal.Hide();
    }

    private void RebuildList()
    {
        var sortedPreviews = SaberCatalogController.ApplySort(_allPreviews, _sortMode);

        List<ISaberListItem> items = [.. sortedPreviews];
        var processed = _folders.Process(items);

        List<UIListCellData> uiItems = [];
        var folderSprite = VectorSpriteGenerator.Generate(IconPaths.Folder, 64);
        var returnSprite = VectorSpriteGenerator.Generate(IconPaths.Return, 64);
        foreach (var item in processed)
        {
            if (item is FolderItem dir)
            {
                var isUp = dir.DisplayName == "<";
                uiItems.Add(new UIListCellData(isUp ? "Back" : dir.DisplayName, isUp ? "Return to parent" : "Directory", isUp ? returnSprite : folderSprite, dir));
            }
            else if (!string.IsNullOrEmpty(item.DisplayName))
            uiItems.Add(new UIListCellData(item.DisplayName, item.CreatorName ?? "", item.CoverImage, item, item.IsPinned));
        }

        _list.SetItems(uiItems);

        int matchIdx = -1;
        if (!string.IsNullOrEmpty(_currentTrailOriginPath))
        {
            for (int i = 0; i < uiItems.Count; i++)
            {
                if (uiItems[i].UserData is AssetPreview preview && preview.RelativePath == _currentTrailOriginPath)
                {
                    matchIdx = i;
                    break;
                }
            }
        }

        if (matchIdx >= 0)
        _list.Select(matchIdx);
        else
        _list.Deselect();
    }
}