// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.GUI.Framework.Core;

internal readonly struct VectorBounds
{
    public readonly float MinX, MinY, MaxX, MaxY;
    public float Width => MaxX - MinX;
    public float Height => MaxY - MinY;
    public bool IsValid => Width > 0.001f && Height > 0.001f;

    private VectorBounds(float minX, float minY, float maxX, float maxY)
    {
        MinX = minX; MinY = minY;
        MaxX = maxX; MaxY = maxY;
    }

    public static VectorBounds Compute(List<Vector2> verts)
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < verts.Count; i++)
        {
            var v = verts[i];
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x;
            if (v.y < minY) minY = v.y;
            if (v.y > maxY) maxY = v.y;
        }
        return new VectorBounds(minX, minY, maxX, maxY);
    }
}

internal static class PolygonTriangulator
{
    public static List<int> Triangulate(List<List<Vector2>> contours, out List<Vector2> allVerts)
    {
        allVerts = [];
        var allTris = new List<int>();

        if (contours.Count is 0) return allTris;

        var parentOf = new int[contours.Count];
        for (int i = 0; i < contours.Count; i++)
        parentOf[i] = -1;

        for (int i = 0; i < contours.Count; i++)
        {
            for (int j = 0; j < contours.Count; j++)
            {
                if (i == j) continue;
                if (contours[i].Count is > 0 && PointInPolygon(contours[i][0], contours[j]))
                {
                    parentOf[i] = j;
                    break;
                }
            }
        }

        for (int i = 0; i < contours.Count; i++)
        {
            if (parentOf[i] >= 0) continue;

            var outer = contours[i];
            EnsureWinding(outer, false);

            var holes = new List<List<Vector2>>();
            for (int h = 0; h < contours.Count; h++)
            {
                if (parentOf[h] == i)
                {
                    EnsureWinding(contours[h], false);
                    holes.Add(contours[h]);
                }
            }

            int offset = allVerts.Count;

            if (holes.Count is 0)
            {
                for (int v = 0; v < outer.Count; v++)
                allVerts.Add(outer[v]);

                EarClip(allVerts, allTris, offset, outer.Count);
            }
            else
            {
                int outerStart = allVerts.Count;
                for (int v = 0; v < outer.Count; v++)
                allVerts.Add(outer[v]);

                foreach (var hole in holes)
                {
                    int holeStart = allVerts.Count;
                    for (int v = 0; v < hole.Count; v++)
                    allVerts.Add(hole[v]);

                    TriangulateRing(allVerts, allTris, outerStart, outer.Count, holeStart, hole.Count);
                }
            }
        }

        return allTris;
    }

    private static void TriangulateRing(List<Vector2> verts, List<int> tris,
    int outerStart, int outerCount, int holeStart, int holeCount)
    {
        var center = Vector2.zero;
        for (int i = 0; i < holeCount; i++)
        center += verts[holeStart + i];
        center /= holeCount;

        var outerAngles = new float[outerCount];
        var holeAngles = new float[holeCount];
        for (int i = 0; i < outerCount; i++)
        outerAngles[i] = Mathf.Atan2(verts[outerStart + i].y - center.y,
        verts[outerStart + i].x - center.x);
        for (int i = 0; i < holeCount; i++)
        holeAngles[i] = Mathf.Atan2(verts[holeStart + i].y - center.y,
        verts[holeStart + i].x - center.x);

        int oStart = 0, hStart = 0;
        for (int i = 1; i < outerCount; i++)
        if (outerAngles[i] < outerAngles[oStart]) oStart = i;
        for (int i = 1; i < holeCount; i++)
        if (holeAngles[i] < holeAngles[hStart]) hStart = i;

        int oi = 0, hi = 0;
        int total = outerCount + holeCount;

        for (int step = 0; step < total; step++)
        {
            int oCurr = outerStart + (oStart + oi) % outerCount;
            int oNext = outerStart + (oStart + oi + 1) % outerCount;
            int hCurr = holeStart + (hStart + hi) % holeCount;
            int hNext = holeStart + (hStart + hi + 1) % holeCount;

            bool advanceOuter;
            if (oi >= outerCount)
            advanceOuter = false;
            else if (hi >= holeCount)
            advanceOuter = true;
            else
            {
                float oNextAngle = outerAngles[(oStart + oi + 1) % outerCount];
                float hNextAngle = holeAngles[(hStart + hi + 1) % holeCount];
                advanceOuter = oNextAngle <= hNextAngle;
            }

            if (advanceOuter)
            {
                tris.Add(oCurr);
                tris.Add(hCurr);
                tris.Add(oNext);
                oi++;
            }
            else
            {
                tris.Add(oCurr);
                tris.Add(hCurr);
                tris.Add(hNext);
                hi++;
            }
        }
    }

    private static void EarClip(List<Vector2> verts, List<int> tris, int start, int count)
    {
        var idx = new List<int>(count);
        for (int i = 0; i < count; i++)
        idx.Add(start + i);

        int safety = count * count;
        while (idx.Count is > 2 && safety-- > 0)
        {
            bool clipped = false;
            for (int i = 0; i < idx.Count; i++)
            {
                int prev = (i - 1 + idx.Count) % idx.Count;
                int next = (i + 1) % idx.Count;

                var a = verts[idx[prev]];
                var b = verts[idx[i]];
                var c = verts[idx[next]];

                if (Cross(a, b, c) <= 0) continue;

                bool isEar = true;
                for (int j = 0; j < idx.Count; j++)
                {
                    if (j == prev || j == i || j == next) continue;
                    if (PointInTriangle(verts[idx[j]], a, b, c))
                    {
                        isEar = false;
                        break;
                    }
                }

                if (!isEar) continue;

                tris.Add(idx[prev]);
                tris.Add(idx[i]);
                tris.Add(idx[next]);
                idx.RemoveAt(i);
                clipped = true;
                break;
            }

            if (!clipped) break;
        }

        if (idx.Count > 2)
        ModLogger.ForSource("EarClipTriangulator").Warn($"Incomplete triangulation: {idx.Count} vertices remain from degenerate polygon");
    }

    private static float Cross(Vector2 a, Vector2 b, Vector2 c)
    {
        return (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
    }

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(a, b, p);
        float d2 = Cross(b, c, p);
        float d3 = Cross(c, a, p);
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(hasNeg && hasPos);
    }

    private static bool PointInPolygon(Vector2 point, List<Vector2> polygon)
    {
        bool inside = false;
        int j = polygon.Count - 1;
        for (int i = 0; i < polygon.Count; i++)
        {
            if ((polygon[i].y > point.y) != (polygon[j].y > point.y) &&
            point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) /
            (polygon[j].y - polygon[i].y) + polygon[i].x)
            {
                inside = !inside;
            }
            j = i;
        }
        return inside;
    }

    private static void EnsureWinding(List<Vector2> poly, bool clockwise)
    {
        float area = 0;
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i];
            var b = poly[(i + 1) % poly.Count];
            area += (b.x - a.x) * (b.y + a.y);
        }

        bool isCW = area > 0;
        if (isCW != clockwise)
        poly.Reverse();
    }
}