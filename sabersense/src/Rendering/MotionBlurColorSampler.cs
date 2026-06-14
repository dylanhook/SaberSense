// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SaberSense.Rendering;

internal static class MotionBlurColorSampler
{
    public const int StripResolution = 16;

    public static Color[] Sample(
    Renderer[] renderers, Matrix4x4 rootInverse, float minZ, float maxZ,
    Dictionary<int, Color[]?> texStripCache)
    {
        if (renderers is null || renderers.Length is 0 || maxZ - minZ <= 0f)
        return new Color[StripResolution];

        var strip = new Color[StripResolution];
        for (int i = 0; i < StripResolution; i++)
        strip[i] = new Color(-1f, -1f, -1f, -1f);
        float range = maxZ - minZ;

        foreach (var r in renderers)
        {
            if (r == null) continue;

            var mats = r.sharedMaterials;
            if (mats is null || mats.Length is 0) continue;

            GetLocalZRange(r, rootInverse, out float rMinZ, out float rMaxZ);

            foreach (var mat in mats)
            {
                if (mat == null) continue;

                Color matColor = GetMaterialColor(mat);
                Color[]? texStrip = ResolveTexStrip(mat, texStripCache);

                if (texStrip != null)
                {
                    CompositeTexStrip(texStrip, matColor, strip, r, rootInverse,
                    minZ, range, rMinZ, rMaxZ);
                }
                else if (matColor.a > 0.01f && matColor.maxColorComponent > 0.05f)
                {
                    AddFlatColor(strip, r, rootInverse, matColor, minZ, range);
                }
            }
        }

        FillGaps(strip);

        for (int i = 0; i < StripResolution; i++)
        {
            if (strip[i].r < 0) strip[i] = Color.black;
        }

        return strip;
    }

    private static Color[]? ResolveTexStrip(Material mat, Dictionary<int, Color[]?> texStripCache)
    {
        if (!mat.HasProperty("_MainTex")) return null;
        if (mat.GetTexture("_MainTex") is not Texture2D sourceTex) return null;

        int key = sourceTex.GetInstanceID();
        if (texStripCache.TryGetValue(key, out var cached)) return cached;

        var readableTex = TryGetReadableTexture(sourceTex, out var tempRT, out bool createdTex);
        Color[]? texStrip = readableTex != null ? BuildTexStrip(readableTex) : null;

        if (createdTex && readableTex != null) Object.Destroy(readableTex);
        if (tempRT != null) RenderTexture.ReleaseTemporary(tempRT);

        texStripCache[key] = texStrip;
        return texStrip;
    }

    private static Color[] BuildTexStrip(Texture2D tex)
    {
        int texH = tex.height;
        int texW = tex.width;

        var texStrip = new Color[StripResolution];
        for (int i = 0; i < StripResolution; i++)
        {
            float v = (float)i / (StripResolution - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt(v * (texH - 1)), 0, texH - 1);

            Color rowAvg = Color.clear;
            int sampleCount = Mathf.Min(texW, 16);
            for (int s = 0; s < sampleCount; s++)
            {
                int px = (texW > 1) ? Mathf.RoundToInt((float)s / (sampleCount - 1) * (texW - 1)) : 0;
                rowAvg += tex.GetPixel(px, py);
            }
            texStrip[i] = rowAvg / sampleCount;
        }
        return texStrip;
    }

    private static void CompositeTexStrip(
    Color[] texStrip, Color matColor, Color[] strip, Renderer r,
    Matrix4x4 rootInverse, float minZ, float range,
    float rMinZ, float rMaxZ)
    {
        float rRange = rMaxZ - rMinZ;
        if (rRange > 0.001f)
        {
            for (int i = 0; i < StripResolution; i++)
            {
                float z = minZ + ((float)i / (StripResolution - 1)) * range;
                if (z < rMinZ || z > rMaxZ) continue;

                float tInRenderer = (z - rMinZ) / rRange;
                int texIdx = Mathf.Clamp(Mathf.RoundToInt(tInRenderer * (StripResolution - 1)), 0, StripResolution - 1);
                var texColor = texStrip[texIdx];

                Color blended;
                if (texColor.a > 0.01f)
                {
                    if (matColor.maxColorComponent > 0.1f)
                    {
                        blended = new Color(
                        texColor.r * matColor.r,
                        texColor.g * matColor.g,
                        texColor.b * matColor.b, 1f);
                    }
                    else
                    {
                        blended = texColor;
                    }
                }
                else
                {
                    blended = new Color(matColor.r, matColor.g, matColor.b, 1f);
                }

                if (strip[i].r < 0)
                strip[i] = blended;
                else if (blended.maxColorComponent > strip[i].maxColorComponent)
                strip[i] = blended;
            }
        }
        else
        {
            AddFlatColor(strip, r, rootInverse, matColor, minZ, range);
        }
    }

    private static Texture2D? TryGetReadableTexture(Texture2D tex, out RenderTexture? tempRT, out bool createdTex)
    {
        tempRT = null;
        createdTex = false;

        if (tex.isReadable) return tex;

        var prev = RenderTexture.active;
        try
        {
            tempRT = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, tempRT);
            RenderTexture.active = tempRT;
            var readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
            readable.Apply();
            createdTex = true;
            return readable;
        }
        catch
        {
            return null;
        }
        finally
        {
            RenderTexture.active = prev;
        }
    }

    private static void GetLocalZRange(Renderer r, Matrix4x4 rootInverse, out float minZ, out float maxZ)
    {
        var b = r.bounds;
        var bMin = b.min;
        var bMax = b.max;
        float localMin = float.MaxValue;
        float localMax = float.MinValue;
        MotionBlurBounds.ForEachAABBCorner(bMin, bMax, rootInverse, lc =>
        {
            if (lc.z < localMin) localMin = lc.z;
            if (lc.z > localMax) localMax = lc.z;
        });
        minZ = localMin;
        maxZ = localMax;
    }

    private static void AddFlatColor(Color[] strip, Renderer r, Matrix4x4 rootInverse, Color color, float minZ, float range)
    {
        GetLocalZRange(r, rootInverse, out float localMinZ, out float localMaxZ);

        for (int i = 0; i < StripResolution; i++)
        {
            float z = minZ + ((float)i / (StripResolution - 1)) * range;
            if (z >= localMinZ && z <= localMaxZ)
            {
                if (strip[i].r < 0)
                strip[i] = color;
                else if (color.maxColorComponent > strip[i].maxColorComponent)
                strip[i] = color;
            }
        }
    }

    private static void FillGaps(Color[] strip)
    {
        for (int i = 0; i < strip.Length; i++)
        {
            if (strip[i].r >= 0) continue;

            float bestDist = float.MaxValue;
            Color bestColor = Color.black;
            for (int j = 0; j < strip.Length; j++)
            {
                if (j == i || strip[j].r < 0) continue;
                float dist = Mathf.Abs(j - i);
                if (dist < bestDist) { bestDist = dist; bestColor = strip[j]; }
            }
            strip[i] = bestColor;
        }
    }

    private static readonly string[] ColorProperties =
    ["_EmissionColor", "_Color", "_TintColor", "_BaseColor", "_Glow", "_SimpleColor"];

    private static Color GetMaterialColor(Material mat)
    {
        var shader = mat.shader;

        foreach (var prop in ColorProperties)
        {
            if (!mat.HasProperty(prop)) continue;

            int idx = shader.FindPropertyIndex(prop);
            if (idx < 0) continue;
            if (shader.GetPropertyType(idx) != ShaderPropertyType.Color) continue;

            var c = mat.GetColor(prop);
            if (prop == "_EmissionColor" && c.maxColorComponent <= 0.1f) continue;
            return c;
        }
        return new Color(1f, 1f, 1f, 0f);
    }
}