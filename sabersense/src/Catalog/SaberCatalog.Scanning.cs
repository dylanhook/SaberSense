// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.AssetPipeline;
using SaberSense.Catalog.Model;
using SaberSense.Core.Messaging;
using SaberSense.Core.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SaberSense.Catalog;

public sealed partial class SaberCatalog
{
    private async Task ExecuteScanAsync(ISaberLoader loader, bool forceGeneration)
    {
        var timer = new PerfTimer("Scanning Saber Catalog");
        var pendingPaths = new List<string>();
        int discovered = 0;

        await foreach (var discovery in loader.DiscoverAsync(_dirs))
        {
            discovered++;

            if (_previews.ContainsKey(discovery.RelativePath)) continue;

            bool cachedRowIsCurrent = false;
            if (TryStatFile(discovery.FullPath, out var fileSize, out var fileTicks))
            cachedRowIsCurrent = _db!.HasCurrentPreview(discovery.RelativePath, fileSize, fileTicks);

            if (cachedRowIsCurrent)
            {
                var row = _db!.GetPreview(discovery.RelativePath);
                if (row is not null)
                {
                    var preview = new AssetPreview(row);
                    ApplyPinState(preview, discovery.RelativePath);
                    if (InjectSiblingCoverIfNeeded(preview))
                    _db.UpsertPreview(preview.ToRow());

                    _previews.TryAdd(discovery.RelativePath, preview);
                    continue;
                }
            }

            if (forceGeneration)
            pendingPaths.Add(discovery.RelativePath);
        }

        int completed = discovered - pendingPaths.Count;
        _broker.Publish(new ScanProgressMsg(completed, discovered));

        const int batchSize = 8;
        for (int i = 0; i < pendingPaths.Count; i += batchSize)
        {
            var batch = pendingPaths.GetRange(i, Math.Min(batchSize, pendingPaths.Count - i));
            await Task.WhenAll(batch.Select(path => ExtractAndStorePreviewAsync(loader, path)));
            completed += batch.Count;
            _broker.Publish(new ScanProgressMsg(completed, discovered));
        }

        if (pendingPaths.Count > 0)
        _db!.Save();

        timer.Print(_log);
    }

    private async Task ExtractAndStorePreviewAsync(ISaberLoader loader, string relativePath)
    {
        if (_previews.ContainsKey(relativePath)) return;

        await _scanPause.WaitAsync();
        _scanPause.Release();

        try
        {
            var data = await loader.ExtractPreviewAsync(relativePath);
            if (data is null) return;

            var preview = new AssetPreview(relativePath, data.Value);
            ApplyPinState(preview, relativePath);
            InjectSiblingCoverIfNeeded(preview);

            _db!.UpsertPreview(preview.ToRow());
            _previews.TryAdd(relativePath, preview);
        }
        catch (Exception ex)
        {
            _log.Warn($"Preview extraction failed for {relativePath}: {ex.Message}");
        }
    }

    internal async Task<SaberAssetEntry?> GenerateAndStorePreviewAsync(
    string relativePath, byte[]? rescuedCoverBytes = null)
    {
        if (_previews.ContainsKey(relativePath)) return null;

        var entry = await this[relativePath];
        if (entry is null) return null;

        var preview = new AssetPreview(relativePath, entry);
        ApplyPinState(preview, relativePath);

        if (preview.CoverImage == null && rescuedCoverBytes is { Length: > 0 })
        preview.InjectCoverBytes(rescuedCoverBytes);

        try
        {
            _db!.UpsertPreview(preview.ToRow());
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to write preview to database: {ex}");
        }

        _previews.TryAdd(relativePath, preview);
        return entry;
    }

    private async Task<SaberAssetEntry?> InflateEntryAsync(string relativePath)
    {
        await _scanPause.WaitAsync();
        try
        {
            return await InflateEntryCore(relativePath);
        }
        finally
        {
            _scanPause.Release();
        }
    }

    private async Task<SaberAssetEntry?> InflateEntryCore(string relativePath)
    {
        if (_loadedEntries.TryGetValue(relativePath, out var cached)) return cached;

        var ext = Path.GetExtension(relativePath);
        var loader = _loaders.FirstOrDefault(l => string.Equals(l.HandledExtension, ext, StringComparison.OrdinalIgnoreCase));
        if (loader is null) return null;

        var rawAsset = await loader.LoadAsync(relativePath);
        if (rawAsset is null) return null;

        var entry = _saberParser.ParseAsset(rawAsset);
        if (entry is not null)
        {
            if (!_loadedEntries.TryAdd(relativePath, entry))
            {
                if (_loadedEntries.TryGetValue(relativePath, out var winner) && !ReferenceEquals(winner, entry))
                {
                    entry.Dispose();
                    return winner;
                }
            }
        }
        return entry;
    }

    private static bool TryStatFile(string fullPath, out long fileSize, out long lastWriteTicks)
    {
        try
        {
            var info = new FileInfo(fullPath);
            if (info.Exists)
            {
                fileSize = info.Length;
                lastWriteTicks = info.LastWriteTimeUtc.Ticks;
                return true;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        fileSize = 0;
        lastWriteTicks = 0;
        return false;
    }

    private void ApplyPinState(AssetPreview preview, string relativePath)
    => preview.IsPinned = _pins.Contains(relativePath);

    private bool InjectSiblingCoverIfNeeded(AssetPreview preview)
    {
        if (preview.CoverImage != null || string.IsNullOrEmpty(preview.ContentHash))
        return false;
        var siblingCover = _db!.FindCoverByContentHash(preview.ContentHash);
        if (siblingCover is null) return false;
        preview.InjectCoverBytes(siblingCover);
        return true;
    }
}