// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Catalog.Model;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Customization;
using SaberSense.Profiles;
using SaberSense.Services;
using System;
using System.Threading.Tasks;

namespace SaberSense.Loadout;

internal sealed class SaberCatalogService(
SaberCatalog catalog,
SaberLoadout loadout,
PreviewSession previewSession,
IMessageBroker broker,
IModLogger log,
PinTracker pins,
IConfigStore configManager)
{
    private readonly IModLogger _log = log.ForSource(nameof(SaberCatalogService));

    private bool _isReloading;

    public bool IsReloading => _isReloading;

    public void SetPinned(SaberAssetEntry entry, bool isOn)
    {
        if (entry is null) return;
        entry.SetPinned(isOn);

        if (catalog is not null)
        {
            var preview = catalog.FindPreviewForEntry(entry);
            preview?.SetPinned(isOn);
        }

        pins.Toggle(entry.LeftPiece!.Asset!.RelativePath);
    }

    public async Task<bool> ReloadCurrentAsync(SaberAssetEntry entry)
    {
        if (entry is null || _isReloading || loadout is null || catalog is null) return false;
        _isReloading = true;
        try
        {
            previewSession.ResetEditorReady();
            previewSession?.WipePreviews();
            await catalog.RefreshSpecificAsync(entry.LeftPiece!.Asset!.RelativePath);
            await configManager.InitializeLoadoutAsync();

            if (loadout!.Left.TryGetSaberAsset(out var sa) && sa?.OwnerEntry is not null)
            previewSession!.SelectRestoredEntry(sa!.OwnerEntry);
            previewSession!.SignalEditorReady();
            return true;
        }
        catch (Exception ex)
        {
            _log.Error($"Error during reload: {ex}");
            previewSession.SignalEditorReady();
            return false;
        }
        finally
        {
            _isReloading = false;
        }
    }

    public async Task HandleFileChangeAsync(string fullPath, AssetPipeline.FileChangeKind kind,
    Func<Task> onListChanged)
    {
        if (catalog is null) return;

        var relativePath = Core.Utilities.AssetPaths.MakeRelative(fullPath);
        var current = previewSession?.ActiveEntry;
        bool isActive = current is not null && IsPathMatch(fullPath, current.LeftPiece!.Asset!.RelativePath);

        _log.Debug($"HandleFileChange: kind={kind} path='{relativePath}' isActive={isActive}");

        switch (kind)
        {
            case AssetPipeline.FileChangeKind.Created:

            var added = await catalog.AddPreviewAsync(relativePath);
            if (added)
            {
                broker?.Publish(new SettingsChangedMsg());
                if (onListChanged is not null) await onListChanged();
            }
            break;

            case AssetPipeline.FileChangeKind.Modified:
            if (isActive)
            {
                await ReloadCurrentAsync(current!);
                if (onListChanged is not null) await onListChanged();
            }

            break;

            case AssetPipeline.FileChangeKind.Deleted:
            if (isActive)
            {
                previewSession?.WipePreviews();
            }
            catalog.UnloadSpecific(relativePath);

            if (onListChanged is not null) await onListChanged();
            break;
        }
    }

    private static bool IsPathMatch(string fullPath, string relativePath)
    {
        var normalizedFull = fullPath.Replace('\\', '/');
        var normalizedRel = relativePath.Replace('\\', '/');
        return normalizedFull.EndsWith(normalizedRel, StringComparison.OrdinalIgnoreCase);
    }

    public async Task ReconcileAsync(Func<Task> onListChanged)
    {
        if (catalog is null) return;
        await catalog.ReconcileAsync();
        if (onListChanged is not null) await onListChanged();
    }
}