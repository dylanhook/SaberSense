// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Core.Logging;
using SaberSense.Persistence;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Behaviors;

public sealed class ComponentModifierRegistry
{
    private readonly Dictionary<int, ModifierBinding> _bindings = [];
    private readonly IModLogger _log;

    public IReadOnlyDictionary<int, ModifierBinding> Bindings => _bindings;

    internal SaberModifierHost? ModifierHost { get; set; }

    public bool HasModifiers => ModifierHost != null;

    public ComponentModifierRegistry(GameObject prefab, IModLogger log)
    {
        _log = log.ForSource(nameof(ComponentModifierRegistry));
        ModifierHost = prefab.GetComponentInChildren<SaberModifierHost>(true);
        if (HasModifiers) DiscoverAll();
    }

    public void ReadFrom(JObject obj, IJsonProvider jsonProvider)
    {
        if (!HasModifiers || obj is null || obj["hasBindings"]?.ToObject<bool>() != true)
        return;

        if (obj["bindings"] is not JObject modsBlock) return;

        var consumed = new HashSet<string>();

        foreach (var (key, binding) in BuildStableKeyMap())
        {
            if (modsBlock[key] is JObject payload)
            {
                binding.ReadFrom(payload, jsonProvider);
                consumed.Add(key);
            }
        }

        foreach (var saved in modsBlock.Properties())
        if (!consumed.Contains(saved.Name))
        _log.Warn($"Saved modifier block '{saved.Name}' has no matching live binding -- discarding (bundle changed since this config was saved?)");
    }

    public JToken WriteTo(IJsonProvider jsonProvider)
    {
        if (!HasModifiers)
        return new JObject { { "hasBindings", false } };

        var modsBlock = new JObject();
        foreach (var (key, binding) in BuildStableKeyMap())
        modsBlock.Add(key, binding.WriteTo(jsonProvider));

        return new JObject { { "hasBindings", true }, { "bindings", modsBlock } };
    }

    public JObject? ResolveSavedBlock(JObject? bindingsBlock, ModifierBinding binding)
    {
        if (bindingsBlock is null || binding is null) return null;

        foreach (var (key, candidate) in BuildStableKeyMap())
        {
            if (!ReferenceEquals(candidate, binding)) continue;
            return bindingsBlock[key] as JObject;
        }
        return null;
    }

    private IEnumerable<(string Key, ModifierBinding Binding)> BuildStableKeyMap()
    {
        var seen = new Dictionary<string, int>();
        foreach (var binding in _bindings.Values)
        {
            var baseKey = StableKeyFor(binding);
            if (seen.TryGetValue(baseKey, out var count))
            {
                seen[baseKey] = count + 1;
                yield return ($"{baseKey}#{count}", binding);
            }
            else
            {
                seen[baseKey] = 1;
                yield return (baseKey, binding);
            }
        }
    }

    private static string StableKeyFor(ModifierBinding binding) => $"{binding.Category}/{binding.Name}";

    private void DiscoverAll()
    {
        ModifierHost!.Initialize();

        foreach (var vis in ModifierHost.VisibilityRules)
        {
            var binding = new VisibilityBinding(vis);
            if (!_bindings.TryAdd(binding.Id, binding))
            _log.Warn($"Duplicate modifier ID {binding.Id} -- skipping");
        }

        foreach (var xfm in ModifierHost.TransformRules)
        {
            var binding = new SpatialBinding(xfm);
            if (!_bindings.TryAdd(binding.Id, binding))
            _log.Warn($"Duplicate modifier ID {binding.Id} -- skipping");
        }
    }

    public void BindToInstance(GameObject saberRoot)
    {
        if (!HasModifiers) return;

        var host = saberRoot.GetComponentInChildren<SaberModifierHost>(true);
        if (host is null) return;

        if (!host.Initialize())
        {
            _log.Warn("Initialization failed");
            return;
        }

        foreach (var (id, rule) in host.EnumerateAllRules())
        if (_bindings.TryGetValue(id, out var b)) b.SetInstance(rule);
    }

    public void SyncFrom(ComponentModifierRegistry source)
    {
        if (!HasModifiers) return;

        foreach (var peer in source.Bindings.Values)
        if (Bindings.TryGetValue(peer.Id, out var local)) local.Sync(peer);
    }

    public IReadOnlyCollection<ModifierBinding> AllBindings() => _bindings.Values;

    public void Reset(int id)
    {
        if (_bindings.TryGetValue(id, out var b)) b.Reset();
    }
}