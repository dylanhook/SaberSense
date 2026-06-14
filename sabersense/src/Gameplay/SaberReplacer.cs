// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Configuration;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using SaberSense.Loadout;
using SaberSense.Profiles;
using SaberSense.Rendering.Materials;
using System;
using System.Linq;
using System.Threading.Tasks;
using Zenject;

namespace SaberSense.Gameplay;

internal sealed class SaberReplacer : IInitializable, IDisposable
{
    public Task ReplacementTask { get; private set; } = Task.CompletedTask;

    public Task? RestoreTask { get; private set; }

    private readonly SaberLoadout _loadout;
    private readonly SaberCatalog _catalog;
    private readonly LoadoutCoordinator _coordinator;
    private readonly ModSettings _settings;
    private readonly SelectionRandomizer _rng;
    private readonly SharedMaterialPool _materialPool;
    private readonly IModLogger _log;

    private readonly string? _snapshotLeftPath;
    private readonly string? _snapshotRightPath;

    private SaberReplacer(
    SaberLoadout loadout,
    SaberCatalog catalog,
    LoadoutCoordinator coordinator,
    ModSettings settings,
    SelectionRandomizer rng,
    SharedMaterialPool materialPool,
    IModLogger log)
    {
        _loadout = loadout;
        _catalog = catalog;
        _coordinator = coordinator;
        _settings = settings;
        _rng = rng;
        _materialPool = materialPool;
        _log = log.ForSource(nameof(SaberReplacer));

        _snapshotLeftPath = GetEquippedPath(_loadout.Left, SaberHand.Left);
        _snapshotRightPath = GetEquippedPath(_loadout.Right, SaberHand.Right);
        _log.Debug($"Snapshot: leftPath='{_snapshotLeftPath}' rightPath='{_snapshotRightPath}'");
    }

    public void Initialize()
    {
        _materialPool.Clear(MaterialPoolOwner.Gameplay);

        _log.Debug($"Initialize: randomize={_settings.RandomizeSaber} pipeline={_settings.ActivePipeline}");
        ReplacementTask = ApplyReplacementAsync();
    }

    public void Dispose()
    {
        _log.Debug($"Dispose: snapshotLeft='{_snapshotLeftPath}' snapshotRight='{_snapshotRightPath}' needsRestore={_snapshotLeftPath is not null}");
        if (_snapshotLeftPath is not null)
        {
            if (_snapshotRightPath is not null && _snapshotRightPath != _snapshotLeftPath)
            _log.Warn($"Split-saber restore: left='{_snapshotLeftPath}' right='{_snapshotRightPath}' -- restoring from left entry only");
            RestoreTask = RestoreOriginalAsync();
            ErrorBoundary.FireAndForget(RestoreTask, _log, nameof(SaberReplacer) + ".Dispose");
        }
    }

    private async Task RestoreOriginalAsync()
    {
        try
        {
            var entry = await _catalog.ResolveEntryAsync(_snapshotLeftPath!);
            if (entry is not null)
            {
                await _coordinator.EquipAsync(entry, EquipSource.ConfigRestore);

                if (entry.IsAssetStale)
                _log.Info($"Restored stale entry '{_snapshotLeftPath}' - will reload at next spawn");
            }
        }
        catch (Exception ex)
        {
            _log.Debug($"Restore skipped (container teardown): {ex.GetType().Name}");
        }
    }

    private async Task ApplyReplacementAsync()
    {
        if (_settings.RandomizeSaber)
        {
            await PickRandomAsync();
        }
    }

    private async Task PickRandomAsync()
    {
        var isCustom = _settings.ActivePipeline is SaberPipeline.SaberAsset
        or SaberPipeline.None;
        if (!isCustom) { _log.Debug("PickRandom: non-custom pipeline, skipping"); return; }

        var candidates = _catalog.EnumeratePreviews()
        .Where(p => p.RelativePath != DefaultSaberProvider.DefaultSaberPath)
        .ToList();
        if (candidates.Count is 0) { _log.Debug("PickRandom: no candidates"); return; }
        var chosen = _rng.PickRandom(candidates);
        if (chosen is null) return;
        _log.Debug($"PickRandom: {candidates.Count} candidates, chose '{chosen.RelativePath}'");
        var entry = await _catalog.ResolveEntryByPreviewAsync(chosen);
        if (entry is null) return;
        await _coordinator.EquipAsync(entry, EquipSource.Randomizer);
    }

    private static string? GetEquippedPath(SaberProfile profile, SaberHand hand)
    {
        if (profile is null) return null;
        if (profile.TryGetSaberAsset(out var cs) && cs?.OwnerEntry is not null)
        {
            var piece = hand == SaberHand.Right ? cs.OwnerEntry.RightPiece : cs.OwnerEntry.LeftPiece;
            return piece?.Asset?.RelativePath;
        }
        return null;
    }
}