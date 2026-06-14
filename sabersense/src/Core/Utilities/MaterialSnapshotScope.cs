// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Core.Utilities;

internal readonly struct MaterialSnapshotScope
{
    private readonly HashSet<int> _preLoadIds;

    public MaterialSnapshotScope()
    {
        _preLoadIds = [];
        foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
        _preLoadIds.Add(m.GetInstanceID());
    }

    public List<Material> GetNewMaterials()
    {
        var result = new List<Material>();
        foreach (var m in Resources.FindObjectsOfTypeAll<Material>())
        {
            if (m != null && !_preLoadIds.Contains(m.GetInstanceID()))
            result.Add(m);
        }
        return result;
    }
}