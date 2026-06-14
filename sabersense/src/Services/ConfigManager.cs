// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.App;
using SaberSense.Configuration;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Loadout;
using SaberSense.Persistence;
using SaberSense.Profiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SaberSense.Services;

internal readonly record struct ConfigInfo(string Name, bool IsDefault, bool IsActive);

internal sealed class ConfigManager : IConfigStore, IDisposable
{
    private const string FileExtension = ".sabersense";
    private const string DefaultConfigName = "default";

    private readonly SaberLoadout _loadout;
    private readonly Serializer _serializer;
    private readonly InternalConfig _internalConfig;
    private readonly SessionController _session;
    private readonly string _configDir;
    private readonly IMessageBroker _broker;
    private readonly AssetRecoveryService _recovery;
    private readonly ConfigFileWatcher _watcher;
    private readonly SaberCompatibilityPolicy _compatibilityPolicy;
    private readonly IModLogger _log;

    private volatile int _isLoading;
    private volatile int _isSaving;

    private readonly SemaphoreSlim _io = new(1, 1);

    public Task? CurrentTask { get; private set; }

    public event Action? OnConfigsChanged
    {
        add => _watcher.OnConfigsChanged += value;
        remove => _watcher.OnConfigsChanged -= value;
    }

    public string ConfigDirectory => _configDir;

    private string ActiveConfigPath => GetFilePath(_internalConfig.ActiveConfigName ?? DefaultConfigName);

    public ConfigManager(
    SaberLoadout loadout,
    Serializer serializer,
    InternalConfig internalConfig,
    AssetRecoveryService recovery,
    SaberCompatibilityPolicy compatibilityPolicy,
    SessionController session,
    AppPaths paths,
    IMessageBroker broker,
    IModLogger log)
    {
        _loadout = loadout;
        _serializer = serializer;
        _internalConfig = internalConfig;
        _session = session;
        _configDir = paths.ConfigsRoot.FullName;
        _broker = broker;
        _recovery = recovery;
        _compatibilityPolicy = compatibilityPolicy;
        _log = log.ForSource(nameof(ConfigManager));

        _watcher = new ConfigFileWatcher(
        _configDir, $"*{FileExtension}",
        () => _isLoading != 0 || _isSaving != 0,
        log);
    }

    private async Task RunExclusiveAsync(Func<Task> body)
    {
        await _io.WaitAsync();
        try
        {
            await body();
        }
        finally
        {
            _io.Release();
        }
    }

    private void WriteEnvelope(string path, JObject payload)
    {
        Interlocked.Exchange(ref _isSaving, 1);
        try
        {
            ConfigEnvelope.WriteToDisk(path, payload);
        }
        finally
        {
            Interlocked.Exchange(ref _isSaving, 0);
        }
    }

    public Task InitializeLoadoutAsync() => RunExclusiveAsync(InitializeLoadoutCore);

    private async Task InitializeLoadoutCore()
    {
        var cfgPath = ActiveConfigPath;
        _log.Debug($"InitializeLoadout: activeConfig='{_internalConfig.ActiveConfigName}' path='{cfgPath}'");

        var restorePhase = _session.Phase is SessionPhase.Editing
        ? SessionPhase.Editing : SessionPhase.Idle;

        if (!File.Exists(cfgPath))
        {
            _log.Info($"Config '{_internalConfig.ActiveConfigName}' not found -- creating with defaults.");
            await ForcePersistCore();
            _session.TransitionTo(restorePhase);
            return;
        }

        try
        {
            var payload = ConfigEnvelope.ReadFromDisk(cfgPath);
            using (_loadout.ConfigLoadScope())
            {
                CurrentTask = _loadout.ReadFromAsync(payload, _serializer);
                await CurrentTask;
            }

            _compatibilityPolicy.DeselectIncompatibleSabers();
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to restore loadout from config file:\n{ex}");
        }
        finally
        {
            CurrentTask = null;
            _session.TransitionTo(restorePhase);
        }
    }

    public Task ForcePersistAsync() => RunExclusiveAsync(ForcePersistCore);

    private Task ForcePersistCore()
    {
        _log.Debug($"ForcePersist: activeConfig='{_internalConfig.ActiveConfigName}' saber={GetEquippedSaberName()}");
        var token = _loadout.WriteTo(_serializer);
        if (token is JObject obj)
        {
            var path = ActiveConfigPath;
            WriteEnvelope(path, obj);
            _log.Info($"ForcePersist written to '{Path.GetFileName(path)}'");
        }
        else
        {
            _log.Warn("ForcePersist: WriteTo returned null or non-object");
        }
        return Task.CompletedTask;
    }

    public Task EnsureAssetsValidAsync() => _recovery.EnsureAssetsValidAsync();

    public void StartWatching() => _watcher.StartWatching();

    public void StopWatching() => _watcher.StopWatching();

    public Task ValidateActiveConfigAsync() => RunExclusiveAsync(ValidateActiveConfigCore);

    private async Task ValidateActiveConfigCore()
    {
        var active = _internalConfig.ActiveConfigName ?? DefaultConfigName;
        var path = GetFilePath(active);
        _log.Debug($"ValidateActiveConfig: checking '{active}' at '{path}'");

        if (!File.Exists(path))
        {
            _log.Warn($"Active config '{active}' no longer exists -- reverting to '{DefaultConfigName}'");
            _internalConfig.ActiveConfigName = DefaultConfigName;
            _internalConfig.Save();

            var defaultPath = GetFilePath(DefaultConfigName);
            if (!File.Exists(defaultPath))
            {
                _log.Info($"Recreating missing default config at '{defaultPath}'");
                await ForcePersistCore();
            }
        }
    }

    public List<ConfigInfo> GetConfigs()
    {
        var active = _internalConfig.ActiveConfigName ?? DefaultConfigName;
        var list = new List<ConfigInfo>();

        if (Directory.Exists(_configDir))
        {
            var files = Directory.GetFiles(_configDir, $"*{FileExtension}")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

            foreach (var name in files)
            {
                bool isDefault = string.Equals(name, DefaultConfigName, StringComparison.OrdinalIgnoreCase);
                list.Add(new ConfigInfo(name, IsDefault: isDefault, IsActive: string.Equals(active, name, StringComparison.OrdinalIgnoreCase)));
            }
        }

        return list;
    }

    public Task SaveAsync(string name) => RunExclusiveAsync(() => SaveCore(name));

    private Task SaveCore(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Task.CompletedTask;
        name = SanitizeName(name);

        var payload = _loadout.WriteTo(_serializer);
        if (payload is not JObject obj) return Task.CompletedTask;

        var path = GetFilePath(name);
        WriteEnvelope(path, obj);

        _internalConfig.ActiveConfigName = name;
        _internalConfig.Save();
        _log.Info($"Saved config '{name}'");
        return Task.CompletedTask;
    }

    public async Task LoadAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        if (System.Threading.Interlocked.CompareExchange(ref _isLoading, 1, 0) != 0)
        {
            _log.Warn($"LoadAsync('{name}') skipped -- already loading");
            return;
        }
        try
        {
            var active = _internalConfig.ActiveConfigName ?? DefaultConfigName;
            _log.Debug($"LoadAsync: target='{name}' current='{active}' phase={_session.Phase}");

            var path = GetFilePath(name);
            if (!File.Exists(path))
            {
                _log.Warn($"Config file not found: {path}");
                return;
            }

            JObject payload;
            try
            {
                payload = ConfigEnvelope.ReadFromDisk(path);
                _log.Debug($"LoadAsync: read '{name}' from disk, keys=[{string.Join(", ", payload.Properties().Select(p => p.Name))}]");
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to read config '{name}': {ex.Message}");
                return;
            }

            _broker?.Publish(new ConfigLoadingMsg());

            var prevPhase = _session.Phase;
            _session.TransitionTo(SessionPhase.LoadingConfig);

            await _io.WaitAsync();
            try
            {
                using (_loadout.ConfigLoadScope())
                {
                    await _loadout.ReadFromAsync(payload, _serializer);
                    _internalConfig.ActiveConfigName = name;
                    _internalConfig.Save();
                    _log.Info($"Loaded config '{name}'");
                    _log.Debug($"LoadAsync: post-load saber={GetEquippedSaberName()}");
                }
            }
            catch (Exception ex)
            {
                _log.Error($"Failed to apply config '{name}': {ex.Message}");
            }
            finally
            {
                _io.Release();
                _session.TransitionTo(prevPhase is SessionPhase.Editing ? SessionPhase.Editing : SessionPhase.Idle);
            }

            using (_loadout.ConfigLoadScope())
            {
                _broker?.Publish(new ConfigLoadedMsg());
            }
        }
        finally
        {
            System.Threading.Interlocked.Exchange(ref _isLoading, 0);
        }
    }

    public bool Delete(string name)
    {
        if (string.IsNullOrWhiteSpace(name) ||
        string.Equals(name, DefaultConfigName, StringComparison.OrdinalIgnoreCase)) return false;

        var path = GetFilePath(name);
        if (!File.Exists(path)) return false;

        try
        {
            File.Delete(path);

            if (string.Equals(_internalConfig.ActiveConfigName, name, StringComparison.OrdinalIgnoreCase))
            {
                _internalConfig.ActiveConfigName = DefaultConfigName;
                _internalConfig.Save();
            }

            _log.Info($"Deleted config '{name}'");
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to delete config '{name}': {ex.Message}");
            return false;
        }
    }

    public async Task ResetToDefaultsAsync()
    {
        await _io.WaitAsync();
        try
        {
            await _loadout.ReadFromAsync(new JObject(), _serializer);

            _loadout.Settings.ResetToDefaults();
        }
        finally
        {
            _io.Release();
        }

        _log.Info("Reset loadout + settings to defaults (in memory -- not saved)");
        _broker?.Publish(new ConfigLoadedMsg());
    }

    public Task<string?> ExportAsync()
    {
        try
        {
            var payload = _loadout.WriteTo(_serializer);
            if (payload is not JObject obj) return Task.FromResult<string?>(null);

            var result = ConfigEnvelope.ToClipboardString(obj);
            _log.Info($"Exported config ({result.Length} chars)");
            return Task.FromResult<string?>(result);
        }
        catch (Exception ex)
        {
            _log.Error($"Export failed: {ex.Message}");
            return Task.FromResult<string?>(null);
        }
    }

    public async Task<bool> ImportAsync(string clipboardData)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(clipboardData))
            {
                _log.Warn("Import data is empty.");
                return false;
            }

            var payload = ConfigEnvelope.FromClipboardString(clipboardData);
            _broker?.Publish(new ConfigLoadingMsg());
            using (_loadout.ConfigLoadScope())
            {
                await _io.WaitAsync();
                try
                {
                    await _loadout.ReadFromAsync(payload, _serializer);
                }
                finally
                {
                    _io.Release();
                }
                _broker?.Publish(new ConfigLoadedMsg());
            }
            _log.Info("Imported config (not saved -- use Save to persist)");
            return true;
        }
        catch (FormatException ex)
        {
            _log.Warn($"Invalid data: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            _log.Error($"Import failed: {ex.Message}");
            return false;
        }
    }

    private string GetFilePath(string name) => Path.Combine(_configDir, name + FileExtension);

    private static string SanitizeName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        name = name.Replace(c, '_');
        return name.Trim();
    }

    private string GetEquippedSaberName()
    {
        if (_loadout.Left.TryGetSaberAsset(out var sa) && sa?.OwnerEntry is not null)
        return sa.OwnerEntry.DisplayName;
        return "(none)";
    }

    public void Dispose()
    {
        _watcher.Dispose();
    }
}