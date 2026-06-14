// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.Behaviors;

internal sealed class SpringBoneCollider : MonoBehaviour
{
    public enum Axis { X, Y, Z }
    public enum Region { Exterior, Interior }

    [SerializeField] internal Vector3 CenterOffset;
    [SerializeField] internal float SphereRadius = 0.5f;
    [SerializeField] internal float CapsuleHeight;
    [SerializeField] internal Axis Orientation;
    [SerializeField] internal Region BoundaryMode;

    private float _cachedScaledRadius;
    private int _cachedFrame = -1;

    public void ResolveParticle(ref Vector3 position, float particleRadius)
    {
        int frame = Time.frameCount;
        if (frame != _cachedFrame)
        {
            _cachedScaledRadius = SphereRadius * Mathf.Abs(transform.lossyScale.x);
            _cachedFrame = frame;
        }
        float scaledRadius = _cachedScaledRadius;
        float halfExtent = CapsuleHeight * 0.5f - SphereRadius;

        if (halfExtent <= 0f)
        {
            var center = transform.TransformPoint(CenterOffset);
            ResolveSphere(ref position, particleRadius, center, scaledRadius, BoundaryMode == Region.Exterior);
            return;
        }

        var offsetA = CenterOffset;
        var offsetB = CenterOffset;

        switch (Orientation)
        {
            case Axis.X: offsetA.x -= halfExtent; offsetB.x += halfExtent; break;
            case Axis.Y: offsetA.y -= halfExtent; offsetB.y += halfExtent; break;
            case Axis.Z: offsetA.z -= halfExtent; offsetB.z += halfExtent; break;
        }

        var endA = transform.TransformPoint(offsetA);
        var endB = transform.TransformPoint(offsetB);

        ResolveCapsule(ref position, particleRadius, endA, endB, scaledRadius, BoundaryMode == Region.Exterior);
    }

    private static void ResolveSphere(ref Vector3 pos, float pRadius, Vector3 center, float sRadius, bool pushOutside)
    {
        float combined = sRadius + pRadius;
        float combinedSq = combined * combined;
        var delta = pos - center;
        float distSq = delta.sqrMagnitude;
        bool shouldResolve = pushOutside
        ? (distSq > 0f && distSq < combinedSq)
        : (distSq > combinedSq);
        if (shouldResolve)
        pos = center + delta * (combined / Mathf.Sqrt(distSq));
    }

    private static void ResolveCapsule(ref Vector3 pos, float pRadius, Vector3 endA, Vector3 endB, float cRadius, bool pushOutside)
    {
        float combined = cRadius + pRadius;
        float combinedSq = combined * combined;
        var axis = endB - endA;
        var toPos = pos - endA;
        float projection = Vector3.Dot(toPos, axis);

        if (projection <= 0f)
        {
            ResolveSphereAt(ref pos, endA, toPos, combined, combinedSq, pushOutside);
        }
        else
        {
            float axisSq = axis.sqrMagnitude;
            if (projection >= axisSq)
            {
                ResolveSphereAt(ref pos, endB, pos - endB, combined, combinedSq, pushOutside);
            }
            else if (axisSq > 0f)
            {
                float t = projection / axisSq;
                var perpendicular = toPos - axis * t;
                float perpSq = perpendicular.sqrMagnitude;
                bool shouldResolve = pushOutside
                ? (perpSq > 0f && perpSq < combinedSq)
                : (perpSq > combinedSq);
                if (shouldResolve)
                pos += perpendicular * ((combined - Mathf.Sqrt(perpSq)) / Mathf.Sqrt(perpSq));
            }
        }
    }

    private static void ResolveSphereAt(ref Vector3 pos, Vector3 center, Vector3 delta, float combined, float combinedSq, bool pushOutside)
    {
        float distSq = delta.sqrMagnitude;
        bool shouldResolve = pushOutside
        ? (distSq > 0f && distSq < combinedSq)
        : (distSq > combinedSq);
        if (shouldResolve)
        pos = center + delta * (combined / Mathf.Sqrt(distSq));
    }
}