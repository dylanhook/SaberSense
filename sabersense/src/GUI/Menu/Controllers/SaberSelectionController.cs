// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Catalog.Model;
using SaberSense.Configuration;
using SaberSense.Customization;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SaberSense.GUI.Menu.Controllers;

internal sealed class SaberSelectionController : IDisposable
{
    private readonly PreviewSession _previewSession;
    private readonly ModSettings _settings;

    private SaberCatalog? _catalog;
    private CancellationTokenSource? _selectionCts;

    public SaberSelectionController(
    PreviewSession previewSession,
    ModSettings settings)
    {
        _previewSession = previewSession;
        _settings = settings;
    }

    public SaberAssetEntry? SelectedEntry => _previewSession?.ActiveEntry;

    public ModSettings Config => _settings;

    public SaberCatalog? Catalog => _catalog;

    public PreviewSession Preview => _previewSession;

    public void Init(SaberCatalog catalog)
    {
        _catalog = catalog;
    }

    public async Task SelectAsync(SaberAssetEntry entry)
    {
        _selectionCts?.Cancel();
        _selectionCts = new();
        var token = _selectionCts.Token;

        if (_previewSession is not null && entry is not null)
        {
            _previewSession.SelectEntry(entry);
        }

        await Task.Yield();
        if (token.IsCancellationRequested) return;
    }

    public async Task<SaberAssetEntry?> ResolveAsync(AssetPreview preview)
    {
        if (_catalog is null) return null;
        return await _catalog[preview.RelativePath];
    }

    public void Dispose()
    {
        _selectionCts?.Cancel();
        _selectionCts?.Dispose();
    }
}