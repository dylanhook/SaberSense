// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Catalog.Model;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SaberSense.GUI.Menu.Controllers;

internal enum SortMode { Name, Date, Size, Author }

internal sealed class SaberCatalogController
{
    private readonly Loadout.SaberCatalogService _catalogService;
    private readonly SaberSelectionController _selectionController;

    private FolderNavigator? _dirManager;

    private SortMode _sortMode = SortMode.Name;

    public SaberCatalogController(
    Loadout.SaberCatalogService catalogService,
    SaberSelectionController selectionController)
    {
        _catalogService = catalogService;
        _selectionController = selectionController;
    }

    public SortMode SortMode
    {
        get => _sortMode;
        set => _sortMode = value;
    }

    public static IOrderedEnumerable<AssetPreview> ApplySort(IEnumerable<AssetPreview> source, SortMode mode)
    {
        var ordered = source.OrderByDescending(m => m.IsPinned);
        return mode switch
        {
            SortMode.Name => ordered.ThenBy(x => x.DisplayName),
            SortMode.Date => ordered.ThenByDescending(x => x.FileLastModifiedTicks),
            SortMode.Size => ordered.ThenByDescending(x => x.FileSize),
            SortMode.Author => ordered.ThenBy(x => x.CreatorName),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
        };
    }

    public FolderNavigator? Folders => _dirManager;

    public void Init(FolderNavigator dirManager)
    {
        _dirManager = dirManager;
    }

    public void SetPinned(SaberAssetEntry entry, bool isOn)
    {
        _catalogService.SetPinned(entry, isOn);
    }
}