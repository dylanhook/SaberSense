// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SaberSense.Behaviors;

[DisallowMultipleComponent]
internal sealed class SaberModifierHost : MonoBehaviour
{
    [HideInInspector, SerializeField]
    internal string ModifierJson = "";

    [HideInInspector, SerializeField]
    internal List<string> TargetObjectNames = [];

    internal VisibilityRule[] VisibilityRules = [];

    internal TransformRule[] TransformRules = [];

    private bool _initialized;

    private static readonly Newtonsoft.Json.JsonSerializerSettings DeserializerSettings = new()
    {
        MissingMemberHandling = Newtonsoft.Json.MissingMemberHandling.Ignore,
        NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore
    };

    public bool Initialize()
    {
        if (string.IsNullOrEmpty(ModifierJson) || _initialized)
        return _initialized;

        DeserializeRules();
        ResolveTargets();

        _initialized = true;
        return true;
    }

    private void DeserializeRules()
    {
        try
        {
            var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<ModifierJsonSchema>(ModifierJson, DeserializerSettings);
            if (payload is null) return;

            VisibilityRules = payload.VisibilityModifiers?.Select(v => new VisibilityRule
            {
                Name = v.Name!,
                Id = v.Id,
                IsVisibleByDefault = v.DefaultValue,
                TargetIndices = v.ObjectIndecies ?? []
            }).ToArray() ?? [];

            TransformRules = payload.TransformModifiers?.Select(t => new TransformRule
            {
                Name = t.Name!,
                Id = t.Id,
                TargetIndices = t.ObjectIndecies ?? []
            }).ToArray() ?? [];
        }
        catch (System.Exception ex)
        {
            ModLogger.ForSource("SaberModifierHost").Warn($"JSON deserialization failed: {ex.Message}");
            VisibilityRules ??= [];
            TransformRules ??= [];
        }
    }

    private void ResolveTargets()
    {
        if (TargetObjectNames is null || TargetObjectNames.Count is 0) return;

        var lookup = BuildHierarchyLookup();

        var resolved = new List<GameObject?>(TargetObjectNames.Count);
        foreach (var name in TargetObjectNames)
        {
            if (!string.IsNullOrEmpty(name) && lookup.TryGetValue(name, out var go))
            resolved.Add(go);
            else
            resolved.Add(null);
        }

        foreach (var rule in VisibilityRules)
        rule.Targets = ResolveIndices(rule.TargetIndices, resolved);

        foreach (var rule in TransformRules)
        rule.Targets = ResolveIndices(rule.TargetIndices, resolved);
    }

    public IEnumerable<(int Id, object Rule)> EnumerateAllRules()
    {
        if (VisibilityRules is not null)
        foreach (var v in VisibilityRules) yield return (v.Id, v);
        if (TransformRules is not null)
        foreach (var t in TransformRules) yield return (t.Id, t);
    }

    private static List<GameObject?> ResolveIndices(List<int> indices, List<GameObject?> resolved)
    {
        if (indices is null) return [];
        return indices
        .Where(i => i >= 0 && i < resolved.Count)
        .Select(i => resolved[i])
        .ToList();
    }

    private Dictionary<string, GameObject> BuildHierarchyLookup()
    {
        var lookup = new Dictionary<string, GameObject>();
        CollectChildren(transform, lookup);
        return lookup;
    }

    private static void CollectChildren(Transform current, Dictionary<string, GameObject> lookup)
    {
        for (int i = 0; i < current.childCount; i++)
        {
            var child = current.GetChild(i);
            lookup.TryAdd(child.name, child.gameObject);
            CollectChildren(child, lookup);
        }
    }

    private sealed class ModifierJsonSchema
    {
        [Newtonsoft.Json.JsonProperty] public List<VisibilityEntry>? VisibilityModifiers = null;
        [Newtonsoft.Json.JsonProperty] public List<TransformEntry>? TransformModifiers = null;
    }

    private sealed class VisibilityEntry
    {
        [Newtonsoft.Json.JsonProperty] public string? Name = null;
        [Newtonsoft.Json.JsonProperty] public int Id = 0;
        [Newtonsoft.Json.JsonProperty] public bool DefaultValue = false;
        [Newtonsoft.Json.JsonProperty] public List<int>? ObjectIndecies = null;
    }

    private sealed class TransformEntry
    {
        [Newtonsoft.Json.JsonProperty] public string? Name = null;
        [Newtonsoft.Json.JsonProperty] public int Id = 0;
        [Newtonsoft.Json.JsonProperty] public List<int>? ObjectIndecies = null;
    }
}