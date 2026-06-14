// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.App;
using SaberSense.Catalog.Model;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Profiles;
using System;
using System.Threading.Tasks;

namespace SaberSense.Loadout;

internal enum EquipSource
{
    UserSelection,

    ConfigRestore,

    Randomizer,

    AssetRecovery,
}

internal sealed class LoadoutCoordinator
{
    private readonly SaberLoadout _loadout;
    private readonly IMessageBroker _broker;
    private readonly SessionController _session;
    private readonly IModLogger _log;

    public LoadoutCoordinator(SaberLoadout loadout, IMessageBroker broker, SessionController session, IModLogger log)
    {
        _loadout = loadout;
        _broker = broker;
        _session = session;
        _log = log.ForSource(nameof(LoadoutCoordinator));
    }

    public async Task EquipAsync(SaberAssetEntry entry, EquipSource source)
    {
        if (entry is null) throw new ArgumentNullException(nameof(entry));

        if (_session.Phase == SessionPhase.LoadingConfig && source != EquipSource.ConfigRestore)
        {
            _log.Debug($"Equip('{entry.DisplayName}', {source}) blocked - config load in progress");
            return;
        }

        await _loadout.EquipEntryAsync(entry);

        _broker.Publish(new SaberEquippedMsg(entry, source));
        _log.Info($"Equipped '{entry.DisplayName}' (source: {source})");
    }
}