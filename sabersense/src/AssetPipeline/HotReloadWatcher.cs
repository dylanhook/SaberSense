// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using IPA.Utilities.Async;
using SaberSense.App;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SaberSense.AssetPipeline;

public enum FileChangeKind { Created, Modified, Deleted }

public sealed class HotReloadWatcher : IDisposable
{
    private static readonly string[] WatchedExtensions = { ".saber", ".whacker" };
    private static readonly TimeSpan FileReadyTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(300);
    private const int PruneThreshold = 50;
    private const int MaxConcurrentTasks = 4;

    public bool Monitoring { get; private set; }

    public event Action<string, FileChangeKind>? OnSaberFileChanged;

    public event Action? OnFolderChanged;

    public event Action? OnWatcherReset;

    private readonly DirectoryInfo _watchRoot;
    private readonly IModLogger _log;
    private FileSystemWatcher? _fsw;
    private CancellationTokenSource? _cts;

    private readonly ConcurrentDictionary<string, DateTime> _lastEventTimes = new(StringComparer.OrdinalIgnoreCase);

    private readonly SemaphoreSlim _concurrencyLimiter = new(MaxConcurrentTasks, MaxConcurrentTasks);

    public HotReloadWatcher(AppPaths dirs, IModLogger log)
    {
        _watchRoot = dirs.SaberRoot;
        _log = log.ForSource(nameof(HotReloadWatcher));
    }

    public void StartMonitoring()
    {
        if (_fsw is not null) StopMonitoring();

        _cts = new();
        _fsw = new FileSystemWatcher(_watchRoot.FullName, "*.*")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.DirectoryName,
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        _fsw.Changed += OnFileSystemEvent;
        _fsw.Created += OnFileSystemEvent;
        _fsw.Renamed += OnFileSystemEvent;
        _fsw.Deleted += OnFileSystemEvent;
        _fsw.Error += OnWatcherError;
        Monitoring = true;
    }

    public void StopMonitoring()
    {
        if (_fsw is null) return;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        _fsw.Changed -= OnFileSystemEvent;
        _fsw.Created -= OnFileSystemEvent;
        _fsw.Renamed -= OnFileSystemEvent;
        _fsw.Deleted -= OnFileSystemEvent;
        _fsw.Error -= OnWatcherError;
        _fsw.EnableRaisingEvents = false;
        _fsw.Dispose();
        _fsw = null;
        Monitoring = false;
        _lastEventTimes.Clear();
    }

    private static bool IsWatchedExtension(string path)
    {
        var ext = Path.GetExtension(path);
        foreach (var watched in WatchedExtensions)
        if (string.Equals(ext, watched, StringComparison.OrdinalIgnoreCase))
        return true;
        return false;
    }

    private static FileChangeKind ClassifyEvent(WatcherChangeTypes type) => type switch
    {
        WatcherChangeTypes.Created => FileChangeKind.Created,
        WatcherChangeTypes.Deleted => FileChangeKind.Deleted,
        _ => FileChangeKind.Modified,
    };

    private static async Task<bool> WaitForFileReadableAsync(string path, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (IOException)
            {
                await Task.Delay(100, token).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }
        return false;
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs args)
    {
        if (string.IsNullOrEmpty(Path.GetExtension(args.FullPath)))
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() => OnFolderChanged?.Invoke());
            return;
        }

        if (!IsWatchedExtension(args.FullPath)) return;

        var kind = ClassifyEvent(args.ChangeType);

        var now = DateTime.UtcNow;
        if (kind != FileChangeKind.Deleted)
        {
            if (_lastEventTimes.TryGetValue(args.FullPath, out var lastTime) &&
            (now - lastTime) < DebounceWindow)
            {
                return;
            }
        }
        _lastEventTimes[args.FullPath] = now;

        if (_lastEventTimes.Count > PruneThreshold)
        {
            var cutoff = now.AddSeconds(-30);
            foreach (var kvp in _lastEventTimes)
            if (kvp.Value < cutoff)
            _lastEventTimes.TryRemove(kvp.Key, out _);
        }

        var token = _cts?.Token ?? CancellationToken.None;
        var fullPath = args.FullPath;

        ErrorBoundary.FireAndForget(
        Task.Run(() => ProcessFileEventAsync(fullPath, kind, token), token), _log, nameof(OnFileSystemEvent));
    }

    private async Task ProcessFileEventAsync(string fullPath, FileChangeKind kind, CancellationToken token)
    {
        bool acquired = false;
        try
        {
            await _concurrencyLimiter.WaitAsync(token);
            acquired = true;
            if (token.IsCancellationRequested) return;

            if (kind != FileChangeKind.Deleted)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeoutCts.CancelAfter(FileReadyTimeout);
                if (!await WaitForFileReadableAsync(fullPath, timeoutCts.Token))
                return;
            }

            await UnityMainThreadTaskScheduler.Factory.StartNew(
            () => OnSaberFileChanged?.Invoke(fullPath, kind));
        }
        finally
        {
            if (acquired) _concurrencyLimiter.Release();
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs args)
    {
        _log.Error($"FileSystemWatcher error: {args.GetException()}");

        UnityMainThreadTaskScheduler.Factory.StartNew(() => OnWatcherReset?.Invoke());
    }

    public void Dispose()
    {
        StopMonitoring();
    }
}