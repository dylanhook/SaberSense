// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using IPA.Utilities.Async;
using SaberSense.Core.Logging;
using System;
using System.IO;
using System.Threading;

namespace SaberSense.Services;

internal sealed class ConfigFileWatcher : IDisposable
{
    private readonly string _configDir;
    private readonly string _filter;
    private readonly Func<bool> _isSelfWrite;
    private readonly IModLogger _log;

    private FileSystemWatcher? _fsw;
    private volatile bool _configListStale;
    private int _refreshPosted;

    public event Action? OnConfigsChanged;

    public ConfigFileWatcher(string configDir, string filter, Func<bool> isSelfWrite, IModLogger log)
    {
        _configDir = configDir;
        _filter = filter;
        _isSelfWrite = isSelfWrite;
        _log = log.ForSource(nameof(ConfigFileWatcher));
    }

    public void StartWatching()
    {
        if (_fsw is not null) return;
        if (!Directory.Exists(_configDir)) return;

        try
        {
            _fsw = new FileSystemWatcher(_configDir, _filter)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _fsw.Created += OnFileSystemEvent;
            _fsw.Deleted += OnFileSystemEvent;
            _fsw.Renamed += OnFileSystemEvent;
            _fsw.Changed += OnFileSystemEvent;
            _fsw.Error += (_, e) => _log.Debug($"Watcher error: {e.GetException().Message}");
        }
        catch (Exception ex)
        {
            _log.Debug($"Failed to start watcher: {ex.Message}");
            _fsw?.Dispose();
            _fsw = null;
        }
    }

    public void StopWatching()
    {
        if (_fsw is null) return;

        _fsw.Created -= OnFileSystemEvent;
        _fsw.Deleted -= OnFileSystemEvent;
        _fsw.Renamed -= OnFileSystemEvent;
        _fsw.Changed -= OnFileSystemEvent;
        _fsw.EnableRaisingEvents = false;
        _fsw.Dispose();
        _fsw = null;
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        if (_isSelfWrite()) return;

        _configListStale = true;

        if (Interlocked.CompareExchange(ref _refreshPosted, 1, 0) == 0)
        {
            UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                Interlocked.Exchange(ref _refreshPosted, 0);
                if (_configListStale)
                {
                    _configListStale = false;
                    OnConfigsChanged?.Invoke();
                }
            });
        }
    }

    public void Dispose() => StopWatching();
}