// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using IPA.Utilities.Async;
using SaberSense.App;
using SaberSense.AssetPipeline.Assembly;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Catalog;

internal sealed class TextureCache : IAsyncLoadable, IDisposable
{
    public Task<CachedTexture?> this[string path] => RetrieveTexture(path);
    public Task? CurrentTask => _populateGuard.IsCompleted ? null : _populateTask;

    public event Action? OnCacheChanged;

    private readonly ConcurrentDictionary<string, CachedTexture> _cache = new();
    private readonly DirectoryInfo _texturesFolder;
    private readonly AsyncOnce _populateGuard = new();
    private Task? _populateTask;
    private FileSystemWatcher? _fsw;
    private volatile bool _disposed;

    private static readonly SemaphoreSlim _ioThrottle = new(4, 4);

    private const int DebounceMs = 300;

    public DirectoryInfo TexturesFolder => _texturesFolder;

    internal TextureCache(AppPaths dirs, string subfolder, IModLogger log)
    {
        _log = log.ForSource(nameof(TextureCache));
        _texturesFolder = dirs.DataRoot.CreateSubdirectory("Textures").CreateSubdirectory(subfolder);
        StartWatching();
    }

    private readonly IModLogger _log;

    public Task<CachedTexture?> RetrieveTexture(string path) => ResolveTextureAsync(path);

    public bool Contains(string path) => _cache.ContainsKey(path);
    public IEnumerable<CachedTexture> EnumerateAll() => _cache.Values;

    public Texture2D? FindByName(string textureName)
    {
        if (string.IsNullOrEmpty(textureName)) return null;

        if (_cache.TryGetValue(textureName, out var cached)) return cached.Texture;

        foreach (var entry in _cache.Values)
        {
            if (entry.Texture != null && entry.Texture.name == textureName)
            return entry.Texture;
        }
        return null;
    }

    public void Purge()
    {
        var snapshot = _cache.Values.ToArray();
        _cache.Clear();
        if (snapshot.Length is 0) return;

        UnityMainThreadTaskScheduler.Factory.StartNew(() =>
        {
            foreach (var tex in snapshot)
            tex.Dispose();
        });
    }

    public void Dispose()
    {
        _disposed = true;
        if (_fsw is not null)
        {
            _fsw.EnableRaisingEvents = false;
            _fsw.Dispose();
            _fsw = null;
        }
        Purge();
    }

    public async Task PopulateCacheAsync()
    {
        await _populateGuard.RunOnceAsync(async () =>
        {
            _populateTask = ExecutePopulationAsync();
            await _populateTask;
        });
    }

    private void StartWatching()
    {
        try
        {
            _fsw = new(_texturesFolder.FullName, "*.*")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };
            _fsw.Created += (_, e) => OnFileCreated(e.FullPath);
            _fsw.Changed += (_, e) => OnFileCreated(e.FullPath);
            _fsw.Deleted += (_, e) => OnFileDeleted(e.FullPath);
            _fsw.Renamed += (_, e) => { OnFileDeleted(e.OldFullPath); OnFileCreated(e.FullPath); };
            _fsw.Error += (_, e) =>
            _log.Warn($"Watcher error: {e.GetException().Message}");
        }
        catch (Exception ex)
        {
            _log.Warn($"Could not start file watcher: {ex.Message}");
        }
    }

    private void OnFileCreated(string fullPath)
    {
        if (_disposed) return;
        if (!IsImageFile(Path.GetExtension(fullPath))) return;

        ErrorBoundary.FireAndForget(
        Task.Run(() => ProcessFileCreatedAsync(fullPath)), _log, nameof(OnFileCreated));
    }

    private async Task ProcessFileCreatedAsync(string fullPath)
    {
        await Task.Delay(DebounceMs);
        if (_disposed || !File.Exists(fullPath)) return;

        var relativePath = AssetPaths.MakeRelative(fullPath);
        if (_cache.ContainsKey(relativePath)) return;

        await _ioThrottle.WaitAsync();
        try
        {
            byte[] bytes;
            try { bytes = await FileIO.SlurpAsync(fullPath); }
            catch (IOException ex) { _log.Debug($"File read failed (may still be locked): {ex.Message}"); return; }
            if (_disposed || bytes is null || bytes.Length is 0) return;

            await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                if (_disposed) return;

                var tex = SpriteFactory.LoadTexture(bytes);
                if (tex == null) return;

                tex.name = relativePath;
                var asset = new CachedTexture(
                Path.GetFileName(relativePath), relativePath, tex, AssetSource.FileSystem);

                if (!_cache.TryAdd(relativePath, asset))
                {
                    asset.Dispose();
                    return;
                }

                OnCacheChanged?.Invoke();
            });
        }
        finally { _ioThrottle.Release(); }
    }

    private void OnFileDeleted(string fullPath)
    {
        if (_disposed) return;
        if (!IsImageFile(Path.GetExtension(fullPath))) return;

        var relativePath = AssetPaths.MakeRelative(fullPath);
        if (!_cache.TryRemove(relativePath, out var removed)) return;

        UnityMainThreadTaskScheduler.Factory.StartNew(() =>
        {
            removed.Dispose();
            OnCacheChanged?.Invoke();
        });
    }

    private static bool IsImageFile(string ext)
    {
        ext = ext.ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg";
    }

    private async Task<CachedTexture?> ResolveTextureAsync(string relativePath)
    {
        if (_cache.TryGetValue(relativePath, out var cached)) return cached;

        var fullPath = AssetPaths.ResolveFull(relativePath);
        if (!File.Exists(fullPath)) return null;

        var bytes = await FileIO.SlurpAsync(fullPath);
        if (bytes is null || bytes.Length is 0) return null;

        CachedTexture? result = null;
        await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
        {
            var tex = SpriteFactory.LoadTexture(bytes);
            if (!tex) return;

            tex!.name = relativePath;

            var asset = new CachedTexture(Path.GetFileName(relativePath), relativePath, tex, AssetSource.FileSystem);
            if (!_cache.TryAdd(relativePath, asset))
            {
                asset.Dispose();
                _cache.TryGetValue(relativePath, out result);
                return;
            }
            result = asset;
        });
        return result;
    }

    private async Task ExecutePopulationAsync()
    {
        var scanTasks = _texturesFolder.EnumerateFiles("*.*", SearchOption.AllDirectories)
        .Where(f => IsImageFile(f.Extension))
        .Select(f => ThrottledResolveAsync(AssetPaths.MakeRelative(f.FullName)));

        await Task.WhenAll(scanTasks);
    }

    private async Task<CachedTexture?> ThrottledResolveAsync(string relativePath)
    {
        await _ioThrottle.WaitAsync();
        try { return await ResolveTextureAsync(relativePath); }
        finally { _ioThrottle.Release(); }
    }
}