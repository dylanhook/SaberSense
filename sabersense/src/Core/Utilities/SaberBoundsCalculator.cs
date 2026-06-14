// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.Core.Utilities;

internal static class SaberBoundsCalculator
{
    public static (float minZ, float maxZ)? ComputeZBounds(
    (float minZ, float maxZ)? parsedBounds,
    GameObject prefab)
    {
        if (parsedBounds is { } p)
        return (p.minZ, p.maxZ);

        if (prefab == null) return null;

        var renderers = prefab.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length is 0) return null;

        var rootInv = prefab.transform.worldToLocalMatrix;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        foreach (var r in renderers)
        {
            var b = r.bounds;
            if (b.size.sqrMagnitude < 0.0001f) continue;

            var bMin = b.min;
            var bMax = b.max;
            for (int cx = 0; cx < 2; cx++)
            for (int cy = 0; cy < 2; cy++)
            for (int cz = 0; cz < 2; cz++)
            {
                var worldPt = new Vector3(
                cx == 0 ? bMin.x : bMax.x,
                cy == 0 ? bMin.y : bMax.y,
                cz == 0 ? bMin.z : bMax.z);
                var localPt = rootInv.MultiplyPoint3x4(worldPt);
                if (localPt.z < minZ) minZ = localPt.z;
                if (localPt.z > maxZ) maxZ = localPt.z;
            }
        }

        return minZ < maxZ ? (minZ, maxZ) : null;
    }
}