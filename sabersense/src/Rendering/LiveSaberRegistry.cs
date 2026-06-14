// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using Zenject;

namespace SaberSense.Rendering;

internal sealed class LiveSaberRegistry
{
    private readonly List<WeakReference<LiveSaber>> _entries = new(4);

    [InjectOptional]
    public PlayerTransforms? PlayerTransforms { get; set; }
    public int Count => _entries.Count;

    public void Register(LiveSaber saber)
    {
        Prune();
        _entries.Add(new(saber));
        saber.PlayerTransforms = PlayerTransforms;
    }

    public void Unregister(LiveSaber saber)
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        {
            if (_entries[i].TryGetTarget(out var target) && ReferenceEquals(target, saber))
            {
                _entries.RemoveAt(i);
                return;
            }
        }
    }

    public void Clear() => _entries.Clear();

    public List<LiveSaber> CollectAlive()
    {
        var alive = new List<LiveSaber>(_entries.Count);
        foreach (var weak in _entries)
        if (weak.TryGetTarget(out var saber))
        alive.Add(saber);
        return alive;
    }

    private void Prune()
    {
        for (int i = _entries.Count - 1; i >= 0; i--)
        if (!_entries[i].TryGetTarget(out _))
        _entries.RemoveAt(i);
    }
}