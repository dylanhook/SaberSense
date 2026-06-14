// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Rendering;

internal static class MotionBlurBounds
{
    private const float MinBoundsSqMagnitude = 0.0001f;
    private const float MinXYWidth = 0.001f;
    private const float DefaultBladeRadius = 0.01f;
    private const float MinBladeRadius = 0.005f;
    private const float MaxBladeRadius = 0.015f;
    private const float TaperStartT = 0.8f;
    private const float TaperEndScale = 0.3f;
    private const float MinBoundsRange = 0.01f;
    private const float TrailMarkerPadding = 0.05f;
    private const float MinScaleZ = 0.001f;
    private const float MinWorldRange = 0.5f;
    private const float MaxWorldRange = 1.2f;
    private const float HiltExtensionFraction = 0.15f;

    public static (float minZ, float maxZ) Compute(
    Renderer[] renderers, Transform root, (float minZ, float maxZ)? parsed)
    {
        float minZ, maxZ;

        if (parsed.HasValue)
        {
            minZ = parsed.Value.minZ;
            maxZ = parsed.Value.maxZ;
        }
        else if (renderers is not null && renderers.Length is > 0 && root != null)
        {
            ComputeFromMeshBounds(renderers, root, out minZ, out maxZ);
        }
        else
        {
            minZ = 0f;
            maxZ = 1f;
        }

        if (root != null)
        {
            bool hadMarkers = ApplyTrailMarkerOverride(root, ref minZ, ref maxZ);
            ApplyScaleEnforcement(root, hadMarkers, ref minZ, ref maxZ);
        }

        return (minZ, maxZ);
    }

    public static float[] BuildProfile(
    Renderer[] renderers, Transform root, float minZ, float maxZ, int resolution)
    {
        var profile = new float[resolution];
        float range = maxZ - minZ;
        if (range <= 0f) return profile;

        var rootInv = root.worldToLocalMatrix;

        var widths = new List<float>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var b = r.bounds;
            if (b.size.sqrMagnitude < MinBoundsSqMagnitude) continue;

            var bMin = b.min;
            var bMax = b.max;
            float maxXY = 0f;
            ForEachAABBCorner(bMin, bMax, rootInv, lc =>
            {
                float xy = Mathf.Sqrt(lc.x * lc.x + lc.y * lc.y);
                if (xy > maxXY) maxXY = xy;
            });
            if (maxXY > MinXYWidth) widths.Add(maxXY);
        }

        float bladeRadius = DefaultBladeRadius;
        if (widths.Count is > 0)
        {
            widths.Sort();
            bladeRadius = Mathf.Clamp(widths[0], MinBladeRadius, MaxBladeRadius);
        }

        for (int i = 0; i < resolution; i++)
        {
            float t = (float)i / (resolution - 1);
            float taper = t > TaperStartT ? Mathf.Lerp(1f, TaperEndScale, (t - TaperStartT) / (1f - TaperStartT)) : 1f;
            profile[i] = bladeRadius * taper;
        }

        return profile;
    }

    internal static void ForEachAABBCorner(Vector3 bMin, Vector3 bMax, Matrix4x4 matrix, System.Action<Vector3> action)
    {
        for (int cx = 0; cx < 2; cx++)
        for (int cy = 0; cy < 2; cy++)
        for (int cz = 0; cz < 2; cz++)
        {
            var corner = new Vector3(
            cx == 0 ? bMin.x : bMax.x,
            cy == 0 ? bMin.y : bMax.y,
            cz == 0 ? bMin.z : bMax.z);
            action(matrix.MultiplyPoint3x4(corner));
        }
    }

    private static void ComputeFromMeshBounds(Renderer[] renderers, Transform root, out float minZ, out float maxZ)
    {
        var rootInv = root.worldToLocalMatrix;
        var perMin = new List<float>();
        var perMax = new List<float>();

        foreach (var r in renderers)
        {
            if (r == null) continue;

            Bounds lb;
            if (r is MeshRenderer mr)
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                lb = mf.sharedMesh.bounds;
            }
            else if (r is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh == null) continue;
                lb = smr.sharedMesh.bounds;
            }
            else continue;

            if (lb.size.sqrMagnitude < MinBoundsSqMagnitude) continue;

            var meshToRootLocal = rootInv * r.transform.localToWorldMatrix;
            var lbMin = lb.min;
            var lbMax = lb.max;
            float rMin = float.MaxValue, rMax = float.MinValue;

            ForEachAABBCorner(lbMin, lbMax, meshToRootLocal, localPt =>
            {
                if (localPt.z < rMin) rMin = localPt.z;
                if (localPt.z > rMax) rMax = localPt.z;
            });

            perMin.Add(rMin);
            perMax.Add(rMax);
        }

        if (perMin.Count is > 0)
        {
            perMin.Sort();
            perMax.Sort();
            int mid = perMax.Count / 2;

            minZ = perMin[0];
            maxZ = perMax[mid];

            if (maxZ - minZ < MinBoundsRange)
            maxZ = perMax[perMax.Count - 1];
        }
        else
        {
            minZ = 0f;
            maxZ = 1f;
        }
    }

    private static bool ApplyTrailMarkerOverride(Transform root, ref float minZ, ref float maxZ)
    {
        var rootInv = root.worldToLocalMatrix;
        float trailTipZ = float.MinValue;
        var markers = root.gameObject.GetComponentsInChildren<SaberTrailMarker>(true);
        bool hasTrailTip = false;

        foreach (var m in markers)
        {
            if (m.PointEnd != null)
            {
                hasTrailTip = true;
                float tipZ = rootInv.MultiplyPoint3x4(m.PointEnd.position).z;
                if (tipZ > trailTipZ) trailTipZ = tipZ;
            }
        }

        if (trailTipZ > float.MinValue && trailTipZ > minZ)
        maxZ = trailTipZ + Mathf.Abs(trailTipZ - minZ) * TrailMarkerPadding;

        return hasTrailTip;
    }

    private static void ApplyScaleEnforcement(Transform root, bool hadTrailMarkers, ref float minZ, ref float maxZ)
    {
        float scaleZ = Mathf.Max(root.lossyScale.z, MinScaleZ);
        float worldRange = (maxZ - minZ) * scaleZ;

        if (worldRange < MinWorldRange)
        {
            float targetLocalRange = 1f / scaleZ;
            float hiltExt = targetLocalRange * HiltExtensionFraction;
            minZ = Mathf.Min(minZ, -hiltExt);
            maxZ = minZ + targetLocalRange;
        }
        else if (worldRange > MaxWorldRange && !hadTrailMarkers)
        {
            maxZ = minZ + MaxWorldRange / scaleZ;
        }
    }
}