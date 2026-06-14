// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Catalog;
using SaberSense.Catalog.Model;
using SaberSense.Configuration;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Persistence;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zenject;

namespace SaberSense.Profiles;

public class SaberLoadout
{
    public SaberProfile Left { get; private set; }

    public SaberProfile Right { get; private set; }

    public bool IsEmpty => Left.IsBlank && Right.IsBlank;

    public SaberProfile this[SaberHand hand] => hand == SaberHand.Left ? Left : Right;

    private readonly SaberCatalog _catalog;
    private readonly IModLogger _log;
    private readonly Serializer _serializer;

    private readonly Dictionary<string, JObject> _saberSettings = [];

    internal void ClearSaberSettings() => _saberSettings.Clear();

    internal ModSettings Settings { get; }

    internal SaberLoadout(
    [Inject(Id = SaberHand.Left)] SaberProfile leftProfile,
    [Inject(Id = SaberHand.Right)] SaberProfile rightProfile,
    SaberCatalog catalog,
    ModSettings settings,
    Serializer serializer,
    IModLogger log)
    {
        _catalog = catalog;
        _log = log.ForSource(nameof(SaberLoadout));
        _serializer = serializer;
        Settings = settings;
        Left = leftProfile;
        Right = rightProfile;
    }

    internal async Task EquipEntryAsync(SaberAssetEntry entry)
    {
        if (entry is null) throw new ArgumentNullException(nameof(entry));
        _log.Debug($"EquipEntry: '{entry.DisplayName}' left='{entry.LeftPiece?.Asset?.RelativePath}' right='{entry.RightPiece?.Asset?.RelativePath}'");

        if (!_isLoadingConfig)
        {
            SaveCurrentSettings(Left);
            SaveCurrentSettings(Right);
        }

        Left.ApplyAssetEntry(entry);
        Right.ApplyAssetEntry(entry);

        if (!_isLoadingConfig)
        {
            await LoadSaberSettingsAsync(Left, entry.LeftPiece);
            await LoadSaberSettingsAsync(Right, entry.RightPiece);
        }
    }

    private void SaveCurrentSettings(SaberProfile profile)
    {
        if (profile.Customization is null) return;
        if (!profile.TryGetSaberAsset(out var sa)) return;
        var path = sa!.Asset?.RelativePath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var obj = new JObject();
            profile.Customization.WriteTo(obj, _serializer);
            _saberSettings[path!] = obj;
            _log.Debug($"SaveCurrentSettings: saved '{path}' ({profile.Hand})");
        }
        catch (Exception ex)
        {
            _log.Error($"SaveCurrentSettings failed for '{path}': {ex.Message}");
        }
    }

    private async Task LoadSaberSettingsAsync(SaberProfile profile, SaberAssetDefinition? piece)
    {
        var path = piece?.Asset?.RelativePath;

        profile.Customization = SaberCustomization.SeedFromDefinition(piece!);

        if (!string.IsNullOrEmpty(path) && _saberSettings.TryGetValue(path!, out var saved))
        {
            try
            {
                await profile.Customization.ReadFromAsync(saved, _serializer);
                _log.Debug($"LoadSaberSettings: loaded '{path}' ({profile.Hand})");
            }
            catch (Exception ex)
            {
                _log.Error($"LoadSaberSettings failed for '{path}': {ex.Message}");
            }
            return;
        }

        _log.Debug($"LoadSaberSettings: fresh defaults for '{path}' ({profile.Hand})");
    }

    public void SyncDimensions(SaberProfile source)
    {
        source.PropagateChanges();
        var target = source == Left ? Right : Left;

        target.Scale = new() { Length = source.Scale.Length, Width = source.Scale.Width };

        if (source.Customization?.TrailSettings is not null && target.Customization is not null)
        {
            target.Customization.TrailSettings ??= new();
            target.Customization.TrailSettings.CloneFrom(source.Customization.TrailSettings);
        }
    }

    private int _configLoadDepth;

    private bool _isLoadingConfig => _configLoadDepth > 0;

    public IDisposable ConfigLoadScope()
    {
        _configLoadDepth++;
        return new ConfigLoadGuard(this);
    }

    private readonly struct ConfigLoadGuard : IDisposable
    {
        private readonly SaberLoadout _owner;
        public ConfigLoadGuard(SaberLoadout owner) => _owner = owner;
        public void Dispose() => _owner._configLoadDepth--;
    }

    internal void ResetAllModifierBindings()
    {
        ResetModifiersForProfile(Left);
        ResetModifiersForProfile(Right);
    }

    private static void ResetModifiersForProfile(SaberProfile profile)
    {
        if (profile.Equipped?.ComponentModifiers is { } modifiers)
        {
            foreach (var b in modifiers.AllBindings()) b.Reset();
        }

        if (profile.Customization is not null)
        profile.Customization.ModifierState = null;
    }

    public async Task ReadFromAsync(JObject obj, Serializer serializer)
    {
        var snapshot = CaptureState();
        try
        {
            ResetAllModifierBindings();

            _log.Debug($"ReadFromAsync: clearing old state (Left equipped={Left.Equipped is not null}, Right equipped={Right.Equipped is not null})");
            _saberSettings.Clear();
            Left.Equipped = null;
            Right.Equipped = null;
            Left.Customization = null;
            Right.Customization = null;
            Left.Scale = SaberScale.Unit;
            Right.Scale = SaberScale.Unit;

            if (obj.TryGetValue(nameof(Left), out var leftToken))
            await SaberProfileCodec.ReadFromAsync(Left, (JObject)leftToken, serializer);

            if (obj.TryGetValue(nameof(Right), out var rightToken))
            await SaberProfileCodec.ReadFromAsync(Right, (JObject)rightToken, serializer);

            if (obj.TryGetValue("SaberSettings", out var ssToken) && ssToken is JObject ssObj)
            {
                foreach (var prop in ssObj.Properties())
                _saberSettings[prop.Name] = (JObject)prop.Value;
                _log.Debug($"ReadFromAsync: loaded {_saberSettings.Count} saber setting(s)");
            }

            if (Left.TryGetSaberAsset(out var diagSa))
            _log.Info($"ReadFromAsync: After profile read: Left piece = '{diagSa?.OwnerEntry?.DisplayName}'");
            else
            _log.Info("ReadFromAsync: After profile read: Left has no saber asset");

            if (obj.TryGetValue("Settings", out var settingsToken) && settingsToken is JObject settingsObj)
            {
                Settings.ResetToDefaults();

                var restored = settingsObj.ToObject<ModSettings>(serializer.Json)!;
                ModSettingsCopier.CopyAll(restored, Settings);

                Settings.RaisePropertyChanged(null);
                _log.Debug("ReadFromAsync: settings restored from preset");
            }
        }
        catch (Exception ex)
        {
            RestoreState(snapshot);
            _log.Error($"Failed to restore loadout (rolled back to previous state):\n{ex}");
            throw;
        }
    }

    private readonly struct LoadoutState
    {
        public readonly SaberAssetDefinition? LeftEquipped;
        public readonly SaberAssetDefinition? RightEquipped;
        public readonly SaberCustomization? LeftCustomization;
        public readonly SaberCustomization? RightCustomization;
        public readonly SaberScale LeftScale;
        public readonly SaberScale RightScale;
        public readonly Dictionary<string, JObject> SaberSettings;

        public readonly JObject? LeftModifierState;
        public readonly JObject? RightModifierState;

        public LoadoutState(
        SaberAssetDefinition? leftEquipped,
        SaberAssetDefinition? rightEquipped,
        SaberCustomization? leftCustomization,
        SaberCustomization? rightCustomization,
        SaberScale leftScale,
        SaberScale rightScale,
        Dictionary<string, JObject> saberSettings,
        JObject? leftModifierState,
        JObject? rightModifierState)
        {
            LeftEquipped = leftEquipped;
            RightEquipped = rightEquipped;
            LeftCustomization = leftCustomization;
            RightCustomization = rightCustomization;
            LeftScale = leftScale;
            RightScale = rightScale;
            SaberSettings = saberSettings;
            LeftModifierState = leftModifierState;
            RightModifierState = rightModifierState;
        }
    }

    private LoadoutState CaptureState() => new(
    Left.Equipped,
    Right.Equipped,
    Left.Customization,
    Right.Customization,
    Left.Scale,
    Right.Scale,
    new Dictionary<string, JObject>(_saberSettings),
    Left.Customization?.ModifierState,
    Right.Customization?.ModifierState);

    private void RestoreState(in LoadoutState state)
    {
        Left.Equipped = state.LeftEquipped;
        Right.Equipped = state.RightEquipped;
        Left.Customization = state.LeftCustomization;
        Right.Customization = state.RightCustomization;
        Left.Scale = state.LeftScale;
        Right.Scale = state.RightScale;

        if (Left.Customization is not null) Left.Customization.ModifierState = state.LeftModifierState;
        if (Right.Customization is not null) Right.Customization.ModifierState = state.RightModifierState;

        _saberSettings.Clear();
        foreach (var kv in state.SaberSettings)
        _saberSettings[kv.Key] = kv.Value;
    }

    public JToken WriteTo(Serializer serializer)
    {
        SaveCurrentSettings(Left);
        SaveCurrentSettings(Right);

        var ssObj = new JObject();
        foreach (var kv in _saberSettings)
        ssObj[kv.Key] = kv.Value;

        return new JObject
        {
            { nameof(Left), SaberProfileCodec.WriteTo(Left, serializer) },
            { nameof(Right), SaberProfileCodec.WriteTo(Right, serializer) },
            { "Settings", JObject.FromObject(Settings, serializer.Json) },
            { "SaberSettings", ssObj }
        };
    }
}