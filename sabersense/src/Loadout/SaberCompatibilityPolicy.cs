// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.App;
using SaberSense.Core.Logging;
using SaberSense.Profiles;

namespace SaberSense.Loadout;

internal sealed class SaberCompatibilityPolicy
{
    private readonly SaberLoadout _loadout;
    private readonly IMultiPassProvider _multiPass;
    private readonly IModLogger _log;

    public SaberCompatibilityPolicy(SaberLoadout loadout, IMultiPassProvider multiPass, IModLogger log)
    {
        _loadout = loadout;
        _multiPass = multiPass;
        _log = log.ForSource(nameof(SaberCompatibilityPolicy));
    }

    public void DeselectIncompatibleSabers()
    {
        if (_multiPass.IsMultiPassEnabled) return;

        bool incompatible =
        (_loadout.Left.TryGetSaberAsset(out var leftCs) && leftCs?.OwnerEntry is not null && !leftCs.OwnerEntry.IsSPICompatible) ||
        (_loadout.Right.TryGetSaberAsset(out var rightCs) && rightCs?.OwnerEntry is not null && !rightCs.OwnerEntry.IsSPICompatible);

        if (incompatible)
        {
            _log.Info("Saved saber requires multi-pass rendering -- deselecting and using default saber.");
            _loadout.Left.Equipped = null;
            _loadout.Right.Equipped = null;
        }
    }
}