// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.App;
using SaberSense.Catalog;
using SaberSense.Configuration;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Core.Utilities;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Menu.Popups;
using SaberSense.GUI.Menu.Controllers;
using SaberSense.Services;
using System;
using System.Linq;
using UnityEngine;

namespace SaberSense.GUI.Menu.Tabs;

internal sealed class ConfigManagementSection(
ModSettings settings,
InternalConfig _internalConfig,
AppPaths _appPaths,
IMessageBroker _broker,
IConfigStore _configManager,
TrailSettingsController _trailController,
SaberTransformController _transformController,
DefaultSaberProvider _defaultSaberProvider,
RectTransform _canvasRoot,
IModLogger _log) : IDisposable
{
    private Action? _hideNormalPanels;
    private Action? _showNormalPanels;

    private GameObject _configContent = null!;
    private GameObject _controlsContent = null!;
    private UIScrollList _configList = null!;
    private UITextInput _configNameInput = null!;
    private string? _selectedConfigName;

    public void SetPanelCallbacks(Action hideNormalPanels, Action showNormalPanels)
    {
        _hideNormalPanels = hideNormalPanels;
        _showNormalPanels = showNormalPanels;
    }

    public void BuildConfigPanel(RectTransform parent)
    {
        var configGroup = new UIGroupBox("Configuration");
        configGroup.SetParent(parent).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);
        _configContent = configGroup.GameObject;
        _configContent.SetActive(false);

        _configList = new UIScrollList("ConfigList");
        _configList.SetCompact().SetParent(configGroup.Content).AddLayoutElement(preferredHeight: 55, flexibleWidth: 1);
        _configList.EnableSearch(_canvasRoot);
        _configList.OnSelect((idx, cell) => OnConfigSelected(cell));

        _configNameInput = new UITextInput("ConfigNameInput", "Config name...", _canvasRoot);
        _configNameInput.SetParent(configGroup.Content).AddLayoutElement(preferredHeight: 4.5f, flexibleWidth: 1);
    }

    public void BuildControlsPanel(RectTransform parent)
    {
        var controlsGroup = new UIGroupBox("Controls");
        controlsGroup.SetParent(parent).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);
        _controlsContent = controlsGroup.GameObject;
        _controlsContent.SetActive(false);

        var saveBtn = new BaseButton("Save").SetParent(controlsGroup.Content).AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        saveBtn.OnClick = OnSaveClicked;

        var loadBtn = new BaseButton("Load").SetParent(controlsGroup.Content).AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        loadBtn.OnClick = OnLoadClicked;

        var deleteBtn = new BaseButton("Delete").SetParent(controlsGroup.Content).AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        deleteBtn.OnClick = OnDeleteClicked;

        var resetBtn = new BaseButton("Reset all").SetParent(controlsGroup.Content).AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        resetBtn.OnClick = OnResetAllClicked;

        var exportBtn = new BaseButton("Export to clipboard").SetParent(controlsGroup.Content).AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        exportBtn.OnClick = OnExportClicked;

        var importBtn = new BaseButton("Import from clipboard").SetParent(controlsGroup.Content).AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        importBtn.OnClick = OnImportClicked;

        var openSabersBtn = new BaseButton("Open sabers folder").SetParent(controlsGroup.Content).AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        openSabersBtn.OnClick = () => { if (_appPaths?.SaberRoot is not null) OpenExternal(_appPaths.SaberRoot.FullName); };

        var openConfigsBtn = new BaseButton("Open configs folder").SetParent(controlsGroup.Content).AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        openConfigsBtn.OnClick = () => { if (_appPaths?.DataRoot is not null) OpenExternal(System.IO.Path.Combine(_appPaths.DataRoot.FullName, "Configs")); };

        var backBtn = new BaseButton("Back").SetParent(controlsGroup.Content).AddLayoutElement(preferredHeight: UITheme.ActionRowHeight, flexibleWidth: 1);
        backBtn.OnClick = Hide;
    }

    public void Show()
    {
        _hideNormalPanels?.Invoke();
        _configContent?.SetActive(true);
        _controlsContent?.SetActive(true);
        _configManager.OnConfigsChanged -= RefreshConfigList;
        _configManager.OnConfigsChanged += RefreshConfigList;
        _configManager.StartWatching();
        RefreshConfigList();
    }

    public void Hide()
    {
        _configManager.StopWatching();
        _configManager.OnConfigsChanged -= RefreshConfigList;
        _configContent?.SetActive(false);
        _controlsContent?.SetActive(false);
        _showNormalPanels?.Invoke();
    }

    private void RefreshConfigList() => ErrorBoundary.FireAndForget(RefreshConfigListAsync(), _log);

    private async System.Threading.Tasks.Task RefreshConfigListAsync()
    {
        if (_configList is null || _configManager is null) return;

        await _configManager.ValidateActiveConfigAsync();

        var configs = _configManager.GetConfigs();
        var cells = configs.Select(c => new UIListCellData(
        title: c.IsActive ? $"● {c.Name}" : $"  {c.Name}",
        subtitle: c.IsDefault ? "(default)" : "",
        userData: c.Name
        )).ToList();

        _configList.SetItems(cells);

        int selectIdx = -1;
        if (!string.IsNullOrEmpty(_selectedConfigName))
        selectIdx = configs.FindIndex(c => string.Equals(c.Name, _selectedConfigName, StringComparison.OrdinalIgnoreCase));
        if (selectIdx < 0)
        {
            selectIdx = configs.FindIndex(c => c.IsActive);
            if (selectIdx >= 0)
            _selectedConfigName = configs[selectIdx].Name;
        }
        if (selectIdx >= 0)
        _configList.Select(selectIdx);
    }

    private void OnConfigSelected(UIListCellData cell)
    {
        _selectedConfigName = cell.UserData as string;
    }

    private void OnSaveClicked()
    {
        var name = _configNameInput?.GetText();
        if (string.IsNullOrWhiteSpace(name))
        name = _selectedConfigName;
        if (string.IsNullOrWhiteSpace(name)) return;

        ErrorBoundary.FireAndForget(SaveAndRefreshAsync(name!), _log);
    }

    private async System.Threading.Tasks.Task SaveAndRefreshAsync(string name)
    {
        await _configManager.SaveAsync(name);
        _configNameInput?.SetText("");
        RefreshConfigList();
    }

    private void OnLoadClicked()
    {
        if (string.IsNullOrWhiteSpace(_selectedConfigName)) return;
        ErrorBoundary.FireAndForget(LoadAndRefreshAsync(_selectedConfigName!), _log);
    }

    private async System.Threading.Tasks.Task LoadAndRefreshAsync(string name)
    {
        await _configManager.LoadAsync(name);
        RefreshConfigList();

        SyncDefaultSaberRegistration();
        _broker?.Publish(new SettingsChangedMsg());
    }

    private void OnDeleteClicked()
    {
        if (string.IsNullOrWhiteSpace(_selectedConfigName)) return;

        if (string.Equals(_selectedConfigName, "default", StringComparison.OrdinalIgnoreCase))
        {
            var info = new MessagePopup("Cannot delete", _canvasRoot);
            info.Show("You cannot delete the default config.");
            return;
        }

        var name = _selectedConfigName!;
        var confirm = new ConfirmPopup("Delete config", _canvasRoot);
        confirm.Show($"Are you sure you want to delete \"{name}\"?", () =>
        {
            bool wasActive = string.Equals(
            name, _internalConfig.ActiveConfigName,
            StringComparison.OrdinalIgnoreCase);
            if (_configManager.Delete(name))
            {
                if (wasActive)
                ErrorBoundary.FireAndForget(LoadAndRefreshAsync("default"), _log);
                else
                RefreshConfigList();
            }
        });
    }

    private void OnResetAllClicked() => ErrorBoundary.FireAndForget(ResetAllAsync(), _log);

    private async System.Threading.Tasks.Task ResetAllAsync()
    {
        await _configManager.ResetToDefaultsAsync();

        _trailController?.SyncFromActiveSaber();
        _transformController?.SyncFromActiveSaber();

        SyncDefaultSaberRegistration();
        if (settings is not null)
        SaberSense.Patches.HarmonyBridge.SwingExtrapolation = settings.SwingExtrapolation;

        _configNameInput?.SetText("");
        RefreshConfigList();
        _broker?.Publish(new SettingsChangedMsg());
    }

    private void OnExportClicked() => ErrorBoundary.FireAndForget(ExportToClipboardAsync(), _log);

    private async System.Threading.Tasks.Task ExportToClipboardAsync()
    {
        var data = await _configManager.ExportAsync();
        if (!string.IsNullOrEmpty(data))
        ClipboardHelper.SetText(data!);
    }

    private void OnImportClicked() => ErrorBoundary.FireAndForget(ImportFromClipboardAsync(), _log);

    private async System.Threading.Tasks.Task ImportFromClipboardAsync()
    {
        var clipboard = ClipboardHelper.GetText();
        var ok = await _configManager.ImportAsync(clipboard);
        RefreshConfigList();
        if (ok) _broker?.Publish(new SettingsChangedMsg());
    }

    private void SyncDefaultSaberRegistration()
    {
        if (settings is null) return;
        if (settings.ShowDefaultSaber) _defaultSaberProvider?.Register();
        else _defaultSaberProvider?.Unregister();
    }

    private void OpenExternal(string path)
    {
        try { System.Diagnostics.Process.Start(path); }
        catch (Exception ex) { _log.Debug($"Open external failed: {ex.Message}"); }
    }

    public void Dispose()
    {
        _configManager.StopWatching();
        _configManager.OnConfigsChanged -= RefreshConfigList;
    }
}