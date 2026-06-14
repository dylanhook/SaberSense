// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Behaviors;

internal sealed class SpringBonePhysics : MonoBehaviour
{
    [SerializeField] internal Transform? ChainRoot;
    [SerializeField] internal float Damping = 0.1f;
    [SerializeField] internal float SpringForce = 0.1f;
    [SerializeField] internal float Rigidity = 0.1f;
    [SerializeField] internal float Inertia;
    [SerializeField] internal float CollisionRadius;
    [SerializeField] internal float TailLength;
    [SerializeField] internal Vector3 TailOffset;
    [SerializeField] internal Vector3 GravityBias;
    [SerializeField] internal Vector3 ExternalForce;
    [SerializeField] internal int ConstrainedAxis;
    [SerializeField] internal List<SpringBoneCollider>? Colliders;
    [SerializeField] internal List<Transform>? Exclusions;

    private sealed class Node
    {
        public Transform? Bone;
        public int Parent = -1;
        public float DepthAlongChain;
        public Vector3 WorldPos;
        public Vector3 PriorWorldPos;
        public Vector3 TailOffset;
        public Vector3 BindLocalPos;
        public Quaternion BindLocalRot;
    }

    private readonly List<Node> _nodes = [];
    private float _chainSpan;
    private Vector3 _gravityInRootSpace;
    private Vector3 _ownerVelocity;
    private Vector3 _ownerLastPos;
    private float _scale = 1f;
    private float _stepBudget;

    private void Start() => Initialize();

    private void OnEnable() => ResetSimulation();

    private void OnDisable() => RestoreBindPose();

    private void Update() => RestoreBindPose();

    private void LateUpdate()
    {
        if (ChainRoot == null || _nodes.Count is 0) return;
        Step(Time.deltaTime);
    }

    private void Initialize()
    {
        _nodes.Clear();
        if (ChainRoot == null) return;

        _gravityInRootSpace = ChainRoot.InverseTransformDirection(GravityBias);
        _scale = Mathf.Abs(transform.lossyScale.x);
        _ownerLastPos = transform.position;
        _ownerVelocity = Vector3.zero;
        _chainSpan = 0f;

        BuildHierarchy(ChainRoot, -1, 0f);
    }

    private void BuildHierarchy(Transform? bone, int parentIndex, float depth)
    {
        var node = new Node
        {
            Bone = bone,
            Parent = parentIndex,
            BindLocalPos = bone != null ? bone.localPosition : Vector3.zero,
            BindLocalRot = bone != null ? bone.localRotation : Quaternion.identity,
        };

        if (bone != null)
        {
            node.WorldPos = node.PriorWorldPos = bone.position;
        }
        else
        {
            var anchor = _nodes[parentIndex].Bone!;
            node.TailOffset = ComputeTailOffset(anchor);
            node.WorldPos = node.PriorWorldPos = anchor.TransformPoint(node.TailOffset);
        }

        if (parentIndex >= 0)
        {
            var parentWorldPos = _nodes[parentIndex].Bone != null
            ? _nodes[parentIndex].Bone!.position
            : node.WorldPos;
            depth += Vector3.Distance(parentWorldPos, node.WorldPos);
            node.DepthAlongChain = depth;
            _chainSpan = Mathf.Max(_chainSpan, depth);
        }

        int selfIndex = _nodes.Count;
        _nodes.Add(node);

        if (bone == null) return;

        for (int i = 0; i < bone.childCount; i++)
        {
            var child = bone.GetChild(i);
            if (!ShouldExclude(child))
            BuildHierarchy(child, selfIndex, depth);
        }

        if (bone.childCount == 0 && (TailLength > 0f || TailOffset != Vector3.zero))
        BuildHierarchy(null, selfIndex, depth);
    }

    private Vector3 ComputeTailOffset(Transform leafBone)
    {
        if (TailLength > 0f)
        {
            var grandParent = leafBone.parent;
            if (grandParent != null)
            {
                return leafBone.InverseTransformPoint(
                leafBone.position * 2f - grandParent.position) * TailLength;
            }
            return new Vector3(TailLength, 0f, 0f);
        }

        return leafBone.InverseTransformPoint(
        transform.TransformDirection(TailOffset) + leafBone.position);
    }

    private bool ShouldExclude(Transform t)
    {
        if (Exclusions == null) return false;
        for (int i = 0; i < Exclusions.Count; i++)
        if (Exclusions[i] == t) return true;
        return false;
    }

    private void Step(float dt)
    {
        _scale = Mathf.Abs(transform.lossyScale.x);
        _ownerVelocity = transform.position - _ownerLastPos;
        _ownerLastPos = transform.position;

        const float tickRate = 60f;
        float tickInterval = 1f / tickRate;
        _stepBudget += dt;

        int ticks = 0;
        while (_stepBudget >= tickInterval)
        {
            _stepBudget -= tickInterval;
            if (++ticks >= 3) { _stepBudget = 0f; break; }
        }

        if (ticks > 0)
        {
            for (int t = 0; t < ticks; t++)
            {
                IntegrateVelocities();
                SolveConstraints();
                _ownerVelocity = Vector3.zero;
            }
        }

        CommitToTransforms();
    }

    private void IntegrateVelocities()
    {
        var gravNorm = GravityBias.normalized;
        var restGravWorld = ChainRoot!.TransformDirection(_gravityInRootSpace);
        var alignedPart = gravNorm * Mathf.Max(Vector3.Dot(restGravWorld, gravNorm), 0f);
        var netGravity = (GravityBias - alignedPart + ExternalForce) * _scale;

        for (int i = 0; i < _nodes.Count; i++)
        {
            var n = _nodes[i];
            if (n.Parent < 0)
            {
                n.PriorWorldPos = n.WorldPos;
                n.WorldPos = n.Bone!.position;
            }
            else
            {
                var drift = n.WorldPos - n.PriorWorldPos;
                var inertiaDelta = _ownerVelocity * Inertia;
                n.PriorWorldPos = n.WorldPos + inertiaDelta;
                n.WorldPos += drift * (1f - Damping) + netGravity + inertiaDelta;
            }
        }
    }

    private void SolveConstraints()
    {
        for (int i = 1; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            var parent = _nodes[node.Parent];

            float segmentLength = MeasureSegmentLength(node, parent);

            ApplyElasticityAndStiffness(node, parent, segmentLength);
            ApplyCollisions(node);
            ApplyAxisLock(node, parent);
            ApplyDistanceConstraint(node, parent, segmentLength);
        }
    }

    private void ApplyElasticityAndStiffness(Node node, Node parent, float segmentLength)
    {
        if (SpringForce <= 0f && Rigidity <= 0f) return;

        var refMatrix = parent.Bone!.localToWorldMatrix;
        refMatrix.SetColumn(3, (Vector4)parent.WorldPos);

        var localOffset = node.Bone != null
        ? node.Bone.localPosition
        : node.TailOffset;

        var idealPos = refMatrix.MultiplyPoint3x4(localOffset);

        if (SpringForce > 0f)
        node.WorldPos += (idealPos - node.WorldPos) * SpringForce;

        if (Rigidity > 0f)
        {
            var gap = idealPos - node.WorldPos;
            float gapMag = gap.magnitude;
            float maxGap = segmentLength * (1f - Rigidity) * 2f;
            if (gapMag > maxGap)
            node.WorldPos += gap * ((gapMag - maxGap) / gapMag);
        }
    }

    private void ApplyCollisions(Node node)
    {
        if (Colliders == null) return;
        float radius = CollisionRadius * _scale;
        for (int c = 0; c < Colliders.Count; c++)
        {
            if (Colliders[c] != null && Colliders[c].enabled)
            Colliders[c].ResolveParticle(ref node.WorldPos, radius);
        }
    }

    private void ApplyAxisLock(Node node, Node parent)
    {
        if (ConstrainedAxis <= 0) return;

        var axisDir = ConstrainedAxis switch
        {
            1 => parent.Bone!.right,
            2 => parent.Bone!.up,
            3 => parent.Bone!.forward,
            _ => Vector3.zero
        };

        if (axisDir != Vector3.zero)
        {
            var plane = new Plane(axisDir, parent.WorldPos);
            node.WorldPos -= axisDir * plane.GetDistanceToPoint(node.WorldPos);
        }
    }

    private static void ApplyDistanceConstraint(Node node, Node parent, float segmentLength)
    {
        var separation = parent.WorldPos - node.WorldPos;
        float currentDist = separation.magnitude;
        if (currentDist > 0f)
        node.WorldPos += separation * ((currentDist - segmentLength) / currentDist);
    }

    private void CommitToTransforms()
    {
        for (int i = 1; i < _nodes.Count; i++)
        {
            var node = _nodes[i];
            var parent = _nodes[node.Parent];

            if (parent.Bone!.childCount <= 1)
            {
                var bindDir = node.Bone != null
                ? node.Bone.localPosition
                : node.TailOffset;

                var fromDir = parent.Bone!.TransformDirection(bindDir);
                var toDir = node.WorldPos - parent.WorldPos;

                if (fromDir.sqrMagnitude > 0f && toDir.sqrMagnitude > 0f)
                parent.Bone.rotation = Quaternion.FromToRotation(fromDir, toDir) * parent.Bone.rotation;
            }

            if (node.Bone != null)
            node.Bone.position = node.WorldPos;
        }
    }

    private static float MeasureSegmentLength(Node node, Node parent)
    {
        if (node.Bone != null)
        return Vector3.Distance(parent.Bone!.position, node.Bone!.position);

        return parent.Bone!.localToWorldMatrix.MultiplyVector(node.TailOffset).magnitude;
    }

    private void ResetSimulation()
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            var n = _nodes[i];
            if (n.Bone != null)
            {
                n.WorldPos = n.PriorWorldPos = n.Bone!.position;
            }
            else if (n.Parent >= 0)
            {
                var parentBone = _nodes[n.Parent].Bone!;
                n.WorldPos = n.PriorWorldPos = parentBone.TransformPoint(n.TailOffset);
            }
        }
        _ownerLastPos = transform.position;
    }

    private void RestoreBindPose()
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            var n = _nodes[i];
            if (n.Bone != null)
            {
                n.Bone.localPosition = n.BindLocalPos;
                n.Bone.localRotation = n.BindLocalRot;
            }
        }
    }
}