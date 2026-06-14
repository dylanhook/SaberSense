// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.Gameplay;

internal sealed class SaberSmoother : MonoBehaviour
{
    private const float MaxLerpRate = 50f;

    private const float MinLerpRate = 15f;

    public float Strength { get; set; }

    private Transform? _target;
    private Vector3 _smoothedPos;
    private Quaternion _smoothedRot;

    private void Start()
    {
        _target = transform.parent;
        if (_target != null)
        {
            _smoothedPos = _target.position;
            _smoothedRot = _target.rotation;
        }
    }

    private void Update()
    {
        if (_target == null || Strength <= 0f)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            return;
        }

        var dt = Time.unscaledDeltaTime;

        var clampedStrength = Mathf.Clamp(Strength, 0.1f, 100f);
        var normalised = clampedStrength / 100f;
        var lerpRate = Mathf.Lerp(MaxLerpRate, MinLerpRate, normalised * normalised);

        var t = Mathf.Clamp01(lerpRate * dt);

        _smoothedPos = Vector3.Lerp(_smoothedPos, _target.position, t);
        _smoothedRot = Quaternion.Slerp(_smoothedRot, _target.rotation, t);

        transform.SetPositionAndRotation(_smoothedPos, _smoothedRot);
    }

    public static SaberSmoother InsertAbove(Transform target, Transform parent, float strength)
    {
        var go = new GameObject("SaberSmoother");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(parent.position, parent.rotation);

        var smoother = go.AddComponent<SaberSmoother>();
        smoother.Strength = strength;

        target.SetParent(go.transform, true);
        return smoother;
    }
}