// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Rendering.Materials;

internal enum MaterialPoolOwner
{
    Menu,

    Gameplay,
}

internal sealed class SharedMaterialPool : IDisposable
{
    private readonly Dictionary<string, Material> _pool = [];
    private readonly Dictionary<MaterialPoolOwner, List<Material>> _owned = [];

    private static string Key(MaterialPoolOwner owner, string resolvedName, SaberHand hand)
    => $"{owner}/{resolvedName}_{hand}";

    public Material GetOrClone(MaterialPoolOwner owner, string resolvedName, Material original, SaberHand hand)
    {
        string key = Key(owner, resolvedName, hand);
        if (_pool.TryGetValue(key, out var existing))
        return existing;

        var clone = new Material(original);
        _pool[key] = clone;
        OwnedFor(owner).Add(clone);
        return clone;
    }

    public Material? Get(MaterialPoolOwner owner, string resolvedName, SaberHand hand)
    => _pool.TryGetValue(Key(owner, resolvedName, hand), out var mat) ? mat : null;

    public bool Contains(MaterialPoolOwner owner, string resolvedName, SaberHand hand)
    => _pool.ContainsKey(Key(owner, resolvedName, hand));

    public void Clear(MaterialPoolOwner owner)
    {
        if (_owned.TryGetValue(owner, out var owned))
        {
            foreach (var mat in owned)
            mat.TryDestroy();
            owned.Clear();
        }

        string prefix = $"{owner}/";

        var stale = new List<string>();
        foreach (var key in _pool.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            stale.Add(key);
        }
        foreach (var key in stale)
        _pool.Remove(key);
    }

    private List<Material> OwnedFor(MaterialPoolOwner owner)
    {
        if (!_owned.TryGetValue(owner, out var list))
        {
            list = [];
            _owned[owner] = list;
        }
        return list;
    }

    private void ClearAll()
    {
        foreach (var owned in _owned.Values)
        {
            foreach (var mat in owned)
            mat.TryDestroy();
            owned.Clear();
        }
        _pool.Clear();
    }

    public void Dispose() => ClearAll();
}