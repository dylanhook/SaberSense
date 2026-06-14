// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Catalog.Model;
using SaberSense.Core.Logging;
using SaberSense.Profiles;
using System.Threading.Tasks;

namespace SaberSense.Loadout;

internal sealed class AssetRecoveryService(
SaberLoadout loadout,
SaberCatalog catalog,
LoadoutCoordinator coordinator,
IModLogger log)
{
    private readonly IModLogger _log = log.ForSource(nameof(AssetRecoveryService));

    private Task? _ensureAssetsTask;

    public Task EnsureAssetsValidAsync()
    {
        if (!loadout.Left.TryGetSaberAsset(out var sa) || sa?.OwnerEntry is null)
        {
            _log.Debug("EnsureAssetsValid: no saber asset in loadout, skipping");
            return Task.CompletedTask;
        }
        if (!sa.OwnerEntry.IsAssetStale)
        {
            _log.Debug($"EnsureAssetsValid: asset '{sa.OwnerEntry.DisplayName}' is fresh, skipping");
            return Task.CompletedTask;
        }

        if (_ensureAssetsTask is { IsCompleted: false })
        {
            _log.Debug("EnsureAssetsValid: coalescing with in-flight reload task");
            return _ensureAssetsTask;
        }

        _log.Debug($"EnsureAssetsValid: starting reload for stale asset '{sa.OwnerEntry.DisplayName}'");
        _ensureAssetsTask = EnsureAssetsValidCore(sa);
        return _ensureAssetsTask;
    }

    private async Task EnsureAssetsValidCore(SaberAssetDefinition sa)
    {
        var path = sa.OwnerEntry!.LeftPiece?.Asset?.RelativePath;
        if (path is null) return;

        if (path == DefaultSaberProvider.DefaultSaberPath)
        {
            _log.Info("Default saber stale - clearing destroyed aux objects");
            sa.OwnerEntry.AuxObjects.Destroy();
            await coordinator.EquipAsync(sa.OwnerEntry, EquipSource.AssetRecovery);
            ValidateTrailIntegrity(sa.OwnerEntry);

            return;
        }

        _log.Info($"Assets stale for '{path}', reloading from disk...");
        await catalog.RefreshSpecificAsync(path);
        var refreshed = await catalog[path];
        _log.Debug($"EnsureAssetsValidCore: RefreshSpecific returned refreshed={(refreshed != null)} for '{path}'");
        if (refreshed is not null)
        {
            await coordinator.EquipAsync(refreshed, EquipSource.AssetRecovery);
            ValidateTrailIntegrity(refreshed);
        }
    }

    private void ValidateTrailIntegrity(SaberAssetEntry entry)
    {
        ValidateHandTrail(entry.LeftPiece!, "Left");
        ValidateHandTrail(entry.RightPiece!, "Right");
    }

    private void ValidateHandTrail(PieceDefinition piece, string hand)
    {
        if (piece is not SaberAssetDefinition sad) return;
        if (!sad.HasTrail)
        {
            _log.Warn($"{hand} piece has no trail after recovery");
            return;
        }
        var ts = sad.TrailSettings;
        if (ts?.Material?.IsValid != true)
        _log.Warn($"{hand} trail material invalid after recovery");
        else
        _log.Debug($"{hand} trail OK: length={ts.TrailLength} width={ts.TrailWidth:F2}");
    }
}