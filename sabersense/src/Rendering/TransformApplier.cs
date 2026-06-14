// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Profiles;
using UnityEngine;

namespace SaberSense.Rendering;

internal class SaberAssetTransformHandler
{
    public readonly TransformApplier Applier;

    public SaberAssetTransformHandler(GameObject target, TransformOverrides overrides)
    {
        Applier = new(target, overrides);
    }
}

internal class TransformApplier
{
    private readonly Vector3 _originalScale;
    private readonly Transform _pivot;
    private readonly TransformOverrides _overrides;

    public TransformApplier(GameObject target, TransformOverrides overrides)
    {
        _pivot = target.transform;
        _overrides = overrides;
        _originalScale = _pivot.localScale;

        ApplyWidth(overrides.Scale);
        ApplyOffset(overrides.Offset);
        ApplyRotation(overrides.RotationDeg);
    }

    public float Width
    {
        get => _overrides.Scale;
        set => ApplyWidth(value);
    }

    public float Offset
    {
        get => _overrides.Offset;
        set => ApplyOffset(value);
    }

    public float Rotation
    {
        get => _overrides.RotationDeg;
        set => ApplyRotation(value);
    }

    private void ApplyWidth(float value)
    {
        value = SaberScale.Clamp(value);
        _pivot.localScale = new Vector3(
        _originalScale.x * value,
        _originalScale.y * value,
        _originalScale.z);
        _overrides.Scale = value;
    }

    private void ApplyOffset(float value)
    {
        var pos = _pivot.localPosition;
        pos.z = value;
        _pivot.localPosition = pos;
        _overrides.Offset = value;
    }

    private void ApplyRotation(float value)
    {
        var angles = _pivot.localEulerAngles;
        angles.z = value;
        _pivot.localEulerAngles = angles;
        _overrides.RotationDeg = value;
    }
}