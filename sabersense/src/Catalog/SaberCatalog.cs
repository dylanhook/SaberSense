// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.App;
using SaberSense.AssetPipeline;
using SaberSense.Catalog.Model;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Core.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SaberSense.Catalog;

public sealed partial class SaberCatalog : IDisposable, IAsyncLoadable
{
    private readonly List<string> _externalSearchPaths = [];
    public IReadOnlyList<string> ExternalSearchPaths => _externalSearchPaths;

    public Task<SaberAssetEntry?> this[string relativePath] => ResolveEntryAsync(relativePath);
    public Task<SaberAssetEntry?> this[AssetPreview preview] => ResolveEntryAsync(preview.RelativePath);

    public Task? CurrentTask => _scanGuard.IsCompleted ? null : _scanTask;

    private readonly IModLogger _log;
    private readonly IEnumerable<ISaberLoader> _loaders;
    private readonly SaberAssetBuilder _saberParser;
    private readonly AppPaths _dirs;
    private readonly IMessageBroker _broker;
    private readonly PinTracker _pins;
    private readonly string _dbPath;
    private PreviewDatabase? _db;

    private readonly ConcurrentDictionary<string, AssetPreview> _previews = new();
    private readonly ConcurrentDictionary<string, SaberAssetEntry> _loadedEntries = new();
    private readonly AsyncOnce _scanGuard = new();

    private readonly ConcurrentDictionary<string, Task<SaberAssetEntry?>> _inflight = new();
    private readonly SemaphoreSlim _scanPause = new(1, 1);
    private Task? _scanTask;

    private int _reconciling;

    private SaberCatalog(
    IModLogger log,
    SaberAssetBuilder saberParser,
    AppPaths dirs,
    List<ISaberLoader> loaders,
    IMessageBroker broker,
    PinTracker pins)
    {
        _log = log.ForSource(nameof(SaberCatalog));
        _dirs = dirs;
        _loaders = loaders;
        _saberParser = saberParser;
        _broker = broker;
        _pins = pins;
        _dbPath = Path.Combine(_dirs.DataRoot.FullName, "saber_cache.bin");
    }

    public void Dispose()
    {
        PurgeAll();
        _db?.Dispose();
        _scanPause.Dispose();
    }

    public void DiscoverExternalFolders()
    {
        _externalSearchPaths.Clear();
        foreach (var dir in _dirs.SaberRoot.GetDirectories("*", SearchOption.AllDirectories))
        {
            var cleanPath = AssetPaths.RemoveRootPrefix(AssetPaths.MakeRelative(dir.FullName));
            var sepIndex = cleanPath.IndexOf(Path.DirectorySeparatorChar);
            if (sepIndex is < 0) continue;
            _externalSearchPaths.Add(cleanPath[(sepIndex + 1)..]);
        }
    }

    public Task<SaberAssetEntry?> ResolveEntryAsync(string relativePath)
    {
        if (_loadedEntries.TryGetValue(relativePath, out var entry)) return Task.FromResult<SaberAssetEntry?>(entry);

        return LoadDedupedAsync(relativePath);
    }

    private Task<SaberAssetEntry?> LoadDedupedAsync(string relativePath)
    => _inflight.GetOrAdd(relativePath, p => DedupedInflateAsync(p));

    private async Task<SaberAssetEntry?> DedupedInflateAsync(string relativePath)
    {
        try
        {
            return await InflateEntryAsync(relativePath);
        }
        finally
        {
            _inflight.TryRemove(relativePath, out _);
        }
    }

    public SaberAssetEntry? TryGetLoaded(string relativePath)
    => _loadedEntries.TryGetValue(relativePath, out var entry) ? entry : null;

    public Task<SaberAssetEntry?> ResolveEntryByPreviewAsync(AssetPreview preview) =>
    this[preview.RelativePath];

    internal Task PreparePreviewsAsync() => ScanAllPreviewsAsync();

    public async Task<bool> AddPreviewAsync(string relativePath)
    {
        if (_previews.ContainsKey(relativePath)) return false;

        var ext = Path.GetExtension(relativePath);
        ISaberLoader? loader = null;
        foreach (var l in _loaders)
        if (string.Equals(l.HandledExtension, ext, StringComparison.OrdinalIgnoreCase))
        { loader = l; break; }
        if (loader is null) return false;

        await ExtractAndStorePreviewAsync(loader, relativePath);
        return _previews.ContainsKey(relativePath);
    }

    public async Task ScanAllPreviewsAsync()
    {
        await _scanGuard.RunOnceAsync(async () =>
        {
            _db = new(_dbPath, _log);
            try { _db.Open(); }
            catch (Exception ex) { _log.Error($"Failed to open preview database: {ex}"); }
            DiscoverExternalFolders();

            foreach (var loader in _loaders)
            {
                _scanTask = ExecuteScanAsync(loader, true);
                await _scanTask;
            }
            _db.Save();
        });
    }

    public AssetPreview? FindPreviewForEntry(SaberAssetEntry entry)
    {
        var key = entry.LeftPiece!.Asset.RelativePath;
        return _previews.TryGetValue(key, out var preview) ? preview : null;
    }

    public IEnumerable<AssetPreview> EnumeratePreviews() => _previews.Values;

    public void PersistPreview(AssetPreview preview)
    {
        try { _db?.UpsertPreview(preview.ToRow()); }
        catch (Exception ex) { _log.Warn($"Failed to persist preview: {ex.Message}"); }
    }

    public void FlushPreviewCache()
    {
        try { _db?.Save(); }
        catch (Exception ex) { _log.Warn($"Failed to flush preview cache: {ex.Message}"); }
    }

    public void RegisterDefaultSaberEntry(SaberAssetEntry entry)
    {
        if (entry is null) return;
        _loadedEntries.TryAdd(DefaultSaberProvider.DefaultSaberPath, entry);
    }

    public void ShowDefaultSaberPreview(AssetPreview preview)
    {
        if (preview is null) return;
        _previews.TryAdd(DefaultSaberProvider.DefaultSaberPath, preview);
    }

    public void HideDefaultSaberPreview()
    {
        _previews.TryRemove(DefaultSaberProvider.DefaultSaberPath, out _);
    }

    private static bool IsDefaultSaber(string path) =>
    string.Equals(path, DefaultSaberProvider.DefaultSaberPath, StringComparison.Ordinal);

    public void PurgeAll()
    {
        foreach (var kvp in _loadedEntries)
        {
            if (!IsDefaultSaber(kvp.Key)) kvp.Value.Dispose();
        }
        _loadedEntries.Clear();
        foreach (var kvp in _previews)
        {
            if (!IsDefaultSaber(kvp.Key)) kvp.Value.Dispose();
        }
        _previews.Clear();
    }

    public void UnloadSpecific(string path)
    {
        if (IsDefaultSaber(path)) return;
        bool changed = false;
        if (_loadedEntries.TryRemove(path, out var entry))
        {
            entry.Dispose();
            changed = true;
        }
        if (_previews.TryRemove(path, out var preview))
        {
            preview.Dispose();
            changed = true;
        }
        if (changed)
        _broker?.Publish(new SettingsChangedMsg());
    }

    public async Task RefreshSpecificAsync(string path)
    {
        if (IsDefaultSaber(path)) return;

        if (_loadedEntries.TryGetValue(path, out var existing) && !existing.IsAssetStale)
        return;

        byte[]? rescuedCoverBytes = null;
        try { rescuedCoverBytes = _db!.GetPreview(path)?.CoverBytes; }
        catch (Exception ex) { _log.Debug($"Cover rescue failed for '{path}' (will regenerate): {ex.Message}"); }

        UnloadSpecific(path);

        await LoadDedupedAsync(path);
        await GenerateAndStorePreviewAsync(path, rescuedCoverBytes);
    }

    public async Task ReconcileAsync()
    {
        if (_db is null) return;

        if (Interlocked.CompareExchange(ref _reconciling, 1, 0) is not 0) return;
        try
        {
            var onDisk = new HashSet<string>(StringComparer.Ordinal);
            bool changed = false;

            foreach (var loader in _loaders)
            {
                await foreach (var route in loader.DiscoverAsync(_dirs))
                {
                    onDisk.Add(route.RelativePath);

                    if (!_previews.ContainsKey(route.RelativePath))
                    {
                        if (await AddPreviewAsync(route.RelativePath))
                        changed = true;
                    }
                }
            }

            foreach (var key in _previews.Keys)
            {
                if (IsDefaultSaber(key) || onDisk.Contains(key)) continue;
                UnloadSpecific(key);
                _db.DeletePreview(key);
                changed = true;
            }

            if (changed)
            {
                _db.Save();
                _broker?.Publish(new SettingsChangedMsg());
            }
        }
        catch (Exception ex)
        {
            _log.Warn($"Catalog reconcile failed: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _reconciling, 0);
        }
    }
}