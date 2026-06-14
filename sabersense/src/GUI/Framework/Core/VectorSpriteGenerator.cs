// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.GUI.Framework.Core;

internal static class VectorSpriteGenerator
{
    private static readonly Dictionary<(string path, int size), Sprite> Cache = [];

    public static void ClearCache()
    {
        foreach (var sprite in Cache.Values)
        if (sprite != null) UnityEngine.Object.Destroy(sprite);
        Cache.Clear();
    }

    public static Sprite? Generate(string svgPath, int size = 64)
    {
        var key = (svgPath, size);
        if (Cache.TryGetValue(key, out var cached))
        return cached;

        var contours = SvgPathParser.Parse(svgPath);
        var tris = PolygonTriangulator.Triangulate(contours, out var verts);

        if (verts.Count is 0) return null;

        var bounds = VectorBounds.Compute(verts);
        if (!bounds.IsValid) return null;

        float scale = (size - 2) / Mathf.Max(bounds.Width, bounds.Height);
        float offX = (size - bounds.Width * scale) * 0.5f;
        float offY = (size - bounds.Height * scale) * 0.5f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];

        for (int t = 0; t + 2 < tris.Count; t += 3)
        {
            var a = MapPoint(verts[tris[t]], bounds, scale, offX, offY, size);
            var b = MapPoint(verts[tris[t + 1]], bounds, scale, offX, offY, size);
            var c = MapPoint(verts[tris[t + 2]], bounds, scale, offX, offY, size);
            RasterizeTriangle(pixels, size, a, b, c);
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        Cache[(svgPath, size)] = sprite;
        return sprite;
    }

    private static Vector2 MapPoint(Vector2 p, VectorBounds bounds,
    float scale, float offX, float offY, int size)
    {
        float x = (p.x - bounds.MinX) * scale + offX;
        float y = size - ((p.y - bounds.MinY) * scale + offY);
        return new Vector2(x, y);
    }

    private static void RasterizeTriangle(Color32[] pixels, int size,
    Vector2 a, Vector2 b, Vector2 c)
    {
        int minPx = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
        int maxPx = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
        int minPy = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
        int maxPy = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));

        var white = new Color32(255, 255, 255, 255);

        for (int py = minPy; py <= maxPy; py++)
        for (int px = minPx; px <= maxPx; px++)
        {
            var p = new Vector2(px + 0.5f, py + 0.5f);
            if (PointInTri(p, a, b, c))
            pixels[py * size + px] = white;
        }
    }

    private static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(b - a, p - a);
        float d2 = Cross(c - b, p - b);
        float d3 = Cross(a - c, p - c);
        bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }
}