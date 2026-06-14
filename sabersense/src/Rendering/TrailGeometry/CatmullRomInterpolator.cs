// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.Rendering.TrailGeometry;

internal static class CatmullRomInterpolator
{
    public static Vector3 Interpolate(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
        (2f * p1) +
        (-p0 + p2) * t +
        (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
        (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    public static void Sample(SnapshotRingBuffer buffer, float t, out Vector3 pos, out Vector3 up)
    {
        if (buffer.Count is < 2)
        {
            var snap = buffer.Get(0);
            pos = snap.Pos;
            up = snap.Normal;
            return;
        }

        t = Mathf.Clamp01(t);
        int idx = buffer.FindIndexByDistance(t, out float localT);

        var s0 = buffer.Get(idx - 1);
        var s1 = buffer.Get(idx);
        var s2 = buffer.Get(idx + 1);
        var s3 = buffer.Get(idx + 2);

        pos = Interpolate(s0.Pos, s1.Pos, s2.Pos, s3.Pos, localT);
        up = Interpolate(s0.Normal, s1.Normal, s2.Normal, s3.Normal, localT);
    }
}