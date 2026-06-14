// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.AssetPipeline.Formats.Saber;

public sealed class SpringBoneEntry
{
    public long HostGameObjectPathId { get; init; }
    public long ChainRootPathId { get; init; }
    public float Damping { get; init; }
    public float SpringForce { get; init; }
    public float Rigidity { get; init; }
    public float Inertia { get; init; }
    public float CollisionRadius { get; init; }
    public float TailLength { get; init; }
    public Vector3 TailOffset { get; init; }
    public Vector3 GravityBias { get; init; }
    public Vector3 ExternalForce { get; init; }
    public int ConstrainedAxis { get; init; }
    public IReadOnlyList<long> ColliderPathIds { get; init; } = [];
    public IReadOnlyList<long> ExclusionPathIds { get; init; } = [];
}

public sealed class SpringColliderEntry
{
    public long ComponentPathId { get; init; }
    public long HostGameObjectPathId { get; init; }
    public int Orientation { get; init; }
    public int BoundaryMode { get; init; }
    public Vector3 CenterOffset { get; init; }
    public float SphereRadius { get; init; }
    public float CapsuleHeight { get; init; }
}