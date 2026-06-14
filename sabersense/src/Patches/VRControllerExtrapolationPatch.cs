// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace SaberSense.Patches;

internal sealed class ControllerSwingState
{
    public Quaternion LastRotation = Quaternion.identity;
    public Vector3 AngularVelocity = Vector3.zero;
    public bool Initialized;

    public Vector3 LocalPivot;
    public bool PivotCached;
}

[HarmonyPatch(typeof(VRController), "Update")]
internal static class VRControllerSwingPatch
{
    private const float MaxPredFactor = 3f;
    private const float VelocitySmoothing = 0.3f;
    private const float AngularVelocityThreshold = 0.001f;
    private const float PredictionFrameTime = 0.016f;
    private const float MinDeltaTime = 0.0001f;
    private const float FallbackDeltaTime = 0.011f;
    private const float PivotSqrEpsilon = 0.0001f;

    private static readonly Dictionary<XRNode, ControllerSwingState> States = [];

    public static void Postfix(VRController __instance)
    {
        var extrapConfig = HarmonyBridge.SwingExtrapolation;
        if (extrapConfig is not { Enabled: true, Strength: > 0f }) return;

        if (extrapConfig.GameOnly && !HarmonyBridge.IsInGameCore) return;

        if (!States.TryGetValue(__instance.node, out var state))
        {
            state = new ControllerSwingState();
            States[__instance.node] = state;
        }

        var currentRot = __instance.transform.localRotation;
        float dt = Time.deltaTime;
        if (dt <= MinDeltaTime) dt = FallbackDeltaTime;

        if (!state.Initialized)
        {
            state.LastRotation = currentRot;
            state.AngularVelocity = Vector3.zero;
            state.Initialized = true;
            return;
        }

        var deltaRot = currentRot * Quaternion.Inverse(state.LastRotation);
        deltaRot.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (axis.sqrMagnitude < AngularVelocityThreshold) { axis = Vector3.up; angle = 0f; }
        axis.Normalize();

        var instantAngularVel = axis * (angle / dt);
        state.AngularVelocity = Vector3.Lerp(state.AngularVelocity, instantAngularVel, VelocitySmoothing);

        float predFactor = (extrapConfig.Strength / 100f) * MaxPredFactor;
        float predictedAngle = state.AngularVelocity.magnitude * predFactor * PredictionFrameTime;
        var predictedAxis = state.AngularVelocity.sqrMagnitude > AngularVelocityThreshold
        ? state.AngularVelocity.normalized
        : Vector3.up;

        if (!state.PivotCached)
        {
            var anchor = __instance.viewAnchorTransform;
            if (anchor != null)
            {
                float anchorZ = anchor.localPosition.z;
                state.LocalPivot = new Vector3(0f, 0f, -anchorZ);
            }

            state.PivotCached = true;
        }

        var prediction = Quaternion.AngleAxis(predictedAngle, predictedAxis);
        var newRot = prediction * currentRot;
        __instance.transform.localRotation = newRot;

        if (state.LocalPivot.sqrMagnitude > PivotSqrEpsilon)
        {
            var shift = currentRot * state.LocalPivot - newRot * state.LocalPivot;
            __instance.transform.localPosition += shift;
        }

        state.LastRotation = currentRot;
    }
}