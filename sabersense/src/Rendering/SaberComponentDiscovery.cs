// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Rendering;

internal static class SaberComponentDiscovery
{
    public static List<SaberTrailMarker>? GetTrails(GameObject saberRoot)
    {
        if (saberRoot == null) return null;

        var buffer = new List<SaberTrailMarker>();
        saberRoot.GetComponentsInChildren(true, buffer);

        var valid = new List<SaberTrailMarker>(buffer.Count);
        foreach (var trail in buffer)
        {
            if (trail.PointEnd != null && trail.PointStart != null)
            valid.Add(trail);
        }

        valid.Sort((a, b) =>
        b.PointEnd!.position.z.CompareTo(a.PointEnd!.position.z));

        return valid;
    }
}