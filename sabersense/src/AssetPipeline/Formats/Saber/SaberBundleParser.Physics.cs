// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.BundleFormat;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.AssetPipeline.Formats.Saber;

public sealed partial class SaberBundleParser
{
    private static SpringBoneEntry ReadDynamicBone(SerializedObject obj, ParseDiagnostics diagnostics)
    {
        var goRef = obj.GetChild("m_GameObject");
        var rootRef = obj.GetChild("m_Root");
        var endOffset = obj.GetChild("m_EndOffset");
        var gravity = obj.GetChild("m_Gravity");
        var force = obj.GetChild("m_Force");

        var colliderIds = ReadPPtrList(obj, "m_Colliders");
        var exclusionIds = ReadPPtrList(obj, "m_Exclusions");

        return new()
        {
            HostGameObjectPathId = goRef?.GetLong("m_PathID") ?? 0,
            ChainRootPathId = rootRef?.GetLong("m_PathID") ?? 0,

            Damping = diagnostics.RequireFloat(obj, "DynamicBone", "m_Damping", 0.1f),
            SpringForce = obj.GetFloat("m_Elasticity", 0.1f),
            Rigidity = obj.GetFloat("m_Stiffness", 0.1f),
            Inertia = obj.GetFloat("m_Inert", 0f),
            CollisionRadius = obj.GetFloat("m_Radius", 0f),
            TailLength = obj.GetFloat("m_EndLength", 0f),
            TailOffset = ReadVector3(endOffset),
            GravityBias = ReadVector3(gravity),
            ExternalForce = ReadVector3(force),
            ConstrainedAxis = obj.GetInt("m_FreezeAxis", 0),
            ColliderPathIds = colliderIds,
            ExclusionPathIds = exclusionIds
        };
    }

    private static SpringColliderEntry ReadDynamicBoneCollider(SerializedObject obj, long componentPathId)
    {
        var goRef = obj.GetChild("m_GameObject");
        var center = obj.GetChild("m_Center");

        return new()
        {
            ComponentPathId = componentPathId,
            HostGameObjectPathId = goRef?.GetLong("m_PathID") ?? 0,
            Orientation = obj.GetInt("m_Direction", 0),
            BoundaryMode = obj.GetInt("m_Bound", 0),
            CenterOffset = ReadVector3(center),
            SphereRadius = obj.GetFloat("m_Radius", 0.5f),
            CapsuleHeight = obj.GetFloat("m_Height", 0f)
        };
    }

    private static List<long> ReadPPtrList(SerializedObject obj, string fieldName)
    {
        var result = new List<long>();
        if (obj[fieldName] is List<object> list)
        {
            foreach (var element in list)
            {
                if (element is SerializedObject pptr)
                result.Add(pptr.GetLong("m_PathID"));
            }
        }
        return result;
    }

    private static Vector3 ReadVector3(SerializedObject? obj)
    {
        if (obj is null) return Vector3.zero;
        return new Vector3(
        obj.GetFloat("x", 0f),
        obj.GetFloat("y", 0f),
        obj.GetFloat("z", 0f));
    }

    private static (float minZ, float maxZ)? ComputeParsedBounds(
    Dictionary<long, ParsedTransform> transforms,
    Dictionary<long, ParsedAABB> meshAABBs,
    Dictionary<long, long> goToMesh,
    Dictionary<long, long> goToTransform)
    {
        if (meshAABBs.Count is 0 || transforms.Count is 0) return null;

        float globalMinZ = float.MaxValue, globalMaxZ = float.MinValue;
        bool found = false;

        foreach (var (goPathId, meshPathId) in goToMesh)
        {
            if (!meshAABBs.TryGetValue(meshPathId, out var aabb)) continue;
            if (!goToTransform.TryGetValue(goPathId, out var xfPathId)) continue;

            var rootMatrix = BuildLocalToRootMatrix(xfPathId, transforms);

            var min = aabb.Center - aabb.Extent;
            var max = aabb.Center + aabb.Extent;
            var corners = new[]
            {
                new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z)
            };

            foreach (var corner in corners)
            {
                var world = rootMatrix.MultiplyPoint3x4(corner);
                if (world.z < globalMinZ) globalMinZ = world.z;
                if (world.z > globalMaxZ) globalMaxZ = world.z;
                found = true;
            }
        }

        return found ? (globalMinZ, globalMaxZ) : null;
    }

    private static Matrix4x4 BuildLocalToRootMatrix(
    long transformPathId, Dictionary<long, ParsedTransform> transforms)
    {
        var chain = new List<ParsedTransform>();
        long current = transformPathId;
        int safety = 100;
        while (current is not 0 && transforms.TryGetValue(current, out var xf) && safety-- > 0)
        {
            chain.Add(xf);
            current = xf.ParentPathId;
        }

        var result = Matrix4x4.identity;
        for (int i = chain.Count - 1; i >= 0; i--)
        {
            var xf = chain[i];
            result *= Matrix4x4.TRS(xf.Position, xf.Rotation, xf.Scale);
        }
        return result;
    }
}