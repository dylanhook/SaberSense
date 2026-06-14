// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.AssetPipeline.Formats.Saber;
using SaberSense.Behaviors;
using SaberSense.Core.Logging;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SaberSense.AssetPipeline.Assembly;

internal static class SpringBoneInjector
{
    private static readonly IModLogger Log = ModLogger.ForSource(nameof(SpringBoneInjector));

    internal static void InjectSpringBones(GameObject root, SaberParseResult parseResult)
    {
        if (root == null || parseResult is null) return;
        if (parseResult.SpringBones.Count is 0 && parseResult.SpringColliders.Count is 0) return;

        var transformsByName = InjectionHelpers.BuildTransformLookup(root.transform);
        var pathIdToTransform = InjectionHelpers.ResolveTransformPathIds(parseResult, transformsByName);

        var collidersByPathId = new Dictionary<long, SpringBoneCollider>();

        foreach (var entry in parseResult.SpringColliders)
        {
            var hostTransform = InjectionHelpers.ResolveTransform(entry.HostGameObjectPathId, pathIdToTransform);
            if (hostTransform == null)
            {
                Log.Debug("Could not resolve collider host, skipping");
                continue;
            }

            var collider = hostTransform.gameObject.AddComponent<SpringBoneCollider>();
            collider.CenterOffset = entry.CenterOffset;
            collider.SphereRadius = entry.SphereRadius;
            collider.CapsuleHeight = entry.CapsuleHeight;
            collider.Orientation = (SpringBoneCollider.Axis)entry.Orientation;
            collider.BoundaryMode = (SpringBoneCollider.Region)entry.BoundaryMode;

            collidersByPathId[entry.ComponentPathId] = collider;
        }

        foreach (var entry in parseResult.SpringBones)
        {
            var hostTransform = InjectionHelpers.ResolveTransform(entry.HostGameObjectPathId, pathIdToTransform);
            if (hostTransform == null)
            {
                Log.Debug("Could not resolve chain host, skipping");
                continue;
            }

            var chainRoot = InjectionHelpers.ResolveTransform(entry.ChainRootPathId, pathIdToTransform);
            if (chainRoot == null)
            {
                Log.Debug("Could not resolve chain root, skipping");
                continue;
            }

            var physics = hostTransform.gameObject.AddComponent<SpringBonePhysics>();
            if (physics == null)
            {
                Log.Debug($"Failed to add SpringBonePhysics to '{hostTransform.name}', skipping");
                continue;
            }
            physics.ChainRoot = chainRoot;
            physics.Damping = entry.Damping;
            physics.SpringForce = entry.SpringForce;
            physics.Rigidity = entry.Rigidity;
            physics.Inertia = entry.Inertia;
            physics.CollisionRadius = entry.CollisionRadius;
            physics.TailLength = entry.TailLength;
            physics.TailOffset = entry.TailOffset;
            physics.GravityBias = entry.GravityBias;
            physics.ExternalForce = entry.ExternalForce;
            physics.ConstrainedAxis = entry.ConstrainedAxis;

            if (entry.ColliderPathIds is { Count: > 0 })
            {
                physics.Colliders = [.. entry.ColliderPathIds
                .Where(id => id is not 0 && collidersByPathId.ContainsKey(id))
                .Select(id => collidersByPathId[id])];
            }

            if (entry.ExclusionPathIds is { Count: > 0 })
            {
                physics.Exclusions = [.. entry.ExclusionPathIds
                .Select(id => InjectionHelpers.ResolveTransform(id, pathIdToTransform))
                .Where(t => t != null)
                .Select(t => t!)];
            }

            Log.Info($"Injected chain on '{hostTransform.name}' root='{chainRoot.name}' " +
            $"damping={entry.Damping:F2} spring={entry.SpringForce:F2} rigidity={entry.Rigidity:F2}");
        }

        MirrorSpringBones(root.transform);
    }

    private static void MirrorSpringBones(Transform root)
    {
        if (!InjectionHelpers.TryGetMirrorSourceAndTarget(root,
        t => t.GetComponentInChildren<SpringBonePhysics>() != null
        || t.GetComponentInChildren<SpringBoneCollider>() != null,
        out var source, out var target))
        return;

        var targetLookup = new Dictionary<string, Transform>();
        InjectionHelpers.CollectTransformsFlat(target, targetLookup);

        var colliderMap = new Dictionary<SpringBoneCollider, SpringBoneCollider>();
        foreach (var srcCollider in source.GetComponentsInChildren<SpringBoneCollider>())
        {
            if (!targetLookup.TryGetValue(srcCollider.transform.name, out var targetTransform))
            continue;

            var dstCollider = targetTransform.gameObject.AddComponent<SpringBoneCollider>();
            dstCollider.CenterOffset = srcCollider.CenterOffset;
            dstCollider.SphereRadius = srcCollider.SphereRadius;
            dstCollider.CapsuleHeight = srcCollider.CapsuleHeight;
            dstCollider.Orientation = srcCollider.Orientation;
            dstCollider.BoundaryMode = srcCollider.BoundaryMode;
            colliderMap[srcCollider] = dstCollider;
        }

        foreach (var srcPhysics in source.GetComponentsInChildren<SpringBonePhysics>())
        {
            if (!targetLookup.TryGetValue(srcPhysics.transform.name, out var targetHost))
            continue;

            Transform? targetChainRoot = null;
            if (srcPhysics.ChainRoot != null)
            targetLookup.TryGetValue(srcPhysics.ChainRoot.name, out targetChainRoot);

            if (targetChainRoot == null) continue;

            var dstPhysics = targetHost.gameObject.AddComponent<SpringBonePhysics>();
            if (dstPhysics == null) continue;

            dstPhysics.ChainRoot = targetChainRoot;
            dstPhysics.Damping = srcPhysics.Damping;
            dstPhysics.SpringForce = srcPhysics.SpringForce;
            dstPhysics.Rigidity = srcPhysics.Rigidity;
            dstPhysics.Inertia = srcPhysics.Inertia;
            dstPhysics.CollisionRadius = srcPhysics.CollisionRadius;
            dstPhysics.TailLength = srcPhysics.TailLength;
            dstPhysics.TailOffset = srcPhysics.TailOffset;
            dstPhysics.GravityBias = srcPhysics.GravityBias;
            dstPhysics.ExternalForce = srcPhysics.ExternalForce;
            dstPhysics.ConstrainedAxis = srcPhysics.ConstrainedAxis;

            if (srcPhysics.Colliders is { Count: > 0 })
            {
                dstPhysics.Colliders = [.. srcPhysics.Colliders
                .Where(c => c != null && colliderMap.ContainsKey(c))
                .Select(c => colliderMap[c])];
            }

            if (srcPhysics.Exclusions is { Count: > 0 })
            {
                dstPhysics.Exclusions = [.. srcPhysics.Exclusions
                .Where(ex => ex != null && targetLookup.ContainsKey(ex.name))
                .Select(ex => targetLookup[ex.name])];
            }

            Log.Info($"Mirrored chain to '{targetHost.name}' root='{targetChainRoot.name}'");
        }
    }
}