// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Core.Utilities;
using SaberSense.Customization;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Catalog;

internal sealed class CoverGenerationService : IDisposable
{
    private readonly SaberCatalog _catalog;
    private readonly PreviewSession _previewSession;
    private readonly IMessageBroker _broker;
    private readonly IModLogger _log;
    private readonly IDisposable _subscription;

    private Func<int, Sprite>? _captureFunc;
    private CancellationTokenSource? _cts;
    private bool _dirty;

    public CoverGenerationService(
    SaberCatalog catalog,
    PreviewSession previewSession,
    IMessageBroker broker,
    IModLogger log)
    {
        _catalog = catalog;
        _previewSession = previewSession;
        _broker = broker;
        _log = log.ForSource(nameof(CoverGenerationService));

        _subscription = broker.Subscribe<SaberPreviewInstantiatedMsg>(OnSaberPreviewInstantiated);
    }

    public void SetCaptureSource(Func<int, Sprite> captureFunc) => _captureFunc = captureFunc;

    public void ClearCaptureSource() => _captureFunc = null;

    public void Dispose()
    {
        CancelPending();
        FlushPendingSaves();
        _subscription?.Dispose();
        _captureFunc = null;
    }

    private void OnSaberPreviewInstantiated(SaberPreviewInstantiatedMsg msg)
    {
        CancelPending();

        var entry = _previewSession.ActiveEntry;
        if (entry is null) return;

        var preview = _catalog.FindPreviewForEntry(entry);
        if (preview is null || preview.CoverImage != null) return;

        _cts = new();
        ErrorBoundary.FireAndForget(GenerateAsync(preview, _cts.Token), _log);
    }

    private async Task GenerateAsync(AssetPreview preview, CancellationToken ct)
    {
        await Task.Yield();

        if (ct.IsCancellationRequested) return;
        if (_captureFunc is null) return;

        if (preview.CoverImage != null) return;

        var snapshot = _captureFunc(128);
        if (snapshot == null) return;

        preview.SetGeneratedCover(snapshot);
        _catalog.PersistPreview(preview);
        MarkDirty();

        _broker.Publish(new CoverGeneratedMsg(preview));
    }

    private void CancelPending()
    {
        if (_cts is null) return;
        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
    }

    private void MarkDirty() => _dirty = true;

    private void FlushPendingSaves()
    {
        if (!_dirty) return;
        _dirty = false;
        _catalog.FlushPreviewCache();
    }
}