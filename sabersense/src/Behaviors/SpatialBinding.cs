// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Persistence;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SaberSense.Behaviors;

internal sealed class SpatialBinding : ModifierBinding
{
    public override string Name { get; }
    public override string Category => "Transform";

    private Vector3 _posOff;
    public Vector3 PositionOffset
    {
        get => _posOff;
        set { _posOff = value; ApplyPosition(value); }
    }

    private Vector3 _scaleOff;
    public Vector3 ScaleOffset
    {
        get => _scaleOff;
        set { _scaleOff = value; ApplyScale(value); }
    }

    private float _rotOff;
    public float RotationOffset
    {
        get => _rotOff;
        set { _rotOff = value; ApplyRotation(value); }
    }

    public string PositionText => $"Position: {_posOff.x:F} {_posOff.y:F} {_posOff.z:F}";
    public string RotationText => $"Rotation: {_rotOff}";
    public string ScaleText => $"Scale: {_scaleOff.x:F} {_scaleOff.y:F} {_scaleOff.z:F}";

    private readonly List<List<(Transform xf, Vector3 basePos, Quaternion baseRot, Vector3 baseScale)>> _allTargets = [];

    public SpatialBinding(TransformRule def) : base(def.Id)
    {
        Name = def.Name;
    }

    public override void SetInstance(object instance)
    {
        if (instance is not TransformRule definition) return;

        _allTargets.RemoveAll(t => t.Count is 0 || !t[0].xf);

        var targets = definition.Targets?
        .Where(o => o != null)
        .Select(o => (o!.transform!, o.transform!.localPosition, o.transform!.localRotation, o.transform!.localScale))
        .ToList() ?? new();

        _allTargets.Add(targets);

        ApplyPositionSingle(targets, _posOff);
        ApplyScaleSingle(targets, _scaleOff);
        ApplyRotationSingle(targets, _rotOff);
    }

    public override void Reset()
    {
        PositionOffset = Vector3.zero;
        RotationOffset = 0f;
        ScaleOffset = Vector3.zero;
    }

    public override void ReadFrom(JObject obj, IJsonProvider jsonProvider)
    {
        if (obj is null) return;
        if (obj.TryGetValue(nameof(PositionOffset), out var p))
        PositionOffset = p.ToObject<Vector3>(jsonProvider.Json);
        if (obj.TryGetValue(nameof(ScaleOffset), out var s))
        ScaleOffset = s.ToObject<Vector3>(jsonProvider.Json);
        if (obj.TryGetValue(nameof(RotationOffset), out var r))
        RotationOffset = r.ToObject<float>(jsonProvider.Json);
    }

    public override JToken WriteTo(IJsonProvider jsonProvider)
    {
        return new JObject
        {
            { nameof(PositionOffset), JToken.FromObject(PositionOffset, jsonProvider.Json) },
            { nameof(ScaleOffset), JToken.FromObject(ScaleOffset, jsonProvider.Json) },
            { nameof(RotationOffset), JToken.FromObject(RotationOffset, jsonProvider.Json) }
        };
    }

    public override void Update() { }

    public override void Sync(object otherMod)
    {
        if (otherMod is not SpatialBinding peer) return;
        PositionOffset = peer.PositionOffset;
        ScaleOffset = peer.ScaleOffset;
        RotationOffset = peer.RotationOffset;
    }

    public override void OnSelected(params object[] args) { }

    public override IEnumerable<ModifierParam> DescribeEditor(JObject? modJson, IJsonProvider jsonProvider)
    {
        yield return ModifierParam.Number(
        "  Pos X", -0.5f, 0.5f,
        () => ReadPosition(modJson).x,
        value => { var p = PositionOffset; p.x = value; PositionOffset = p; },
        sectionHeader: Name);
        yield return ModifierParam.Number(
        "  Pos Y", -0.5f, 0.5f,
        () => ReadPosition(modJson).y,
        value => { var p = PositionOffset; p.y = value; PositionOffset = p; });
        yield return ModifierParam.Number(
        "  Pos Z", -0.5f, 0.5f,
        () => ReadPosition(modJson).z,
        value => { var p = PositionOffset; p.z = value; PositionOffset = p; });
        yield return ModifierParam.Number(
        "  Rotation", -180f, 180f,
        () => modJson?[nameof(RotationOffset)]?.ToObject<float>() ?? RotationOffset,
        value => RotationOffset = value);
        yield return ModifierParam.Number(
        "  Scale X", -1f, 1f,
        () => ReadScale(modJson).x,
        value => { var s = ScaleOffset; s.x = value; ScaleOffset = s; });
        yield return ModifierParam.Number(
        "  Scale Y", -1f, 1f,
        () => ReadScale(modJson).y,
        value => { var s = ScaleOffset; s.y = value; ScaleOffset = s; });
        yield return ModifierParam.Number(
        "  Scale Z", -1f, 1f,
        () => ReadScale(modJson).z,
        value => { var s = ScaleOffset; s.z = value; ScaleOffset = s; });
    }

    private Vector3 ReadPosition(JObject? modJson) =>
    modJson?[nameof(PositionOffset)]?.ToObject<Vector3>() ?? PositionOffset;

    private Vector3 ReadScale(JObject? modJson) =>
    modJson?[nameof(ScaleOffset)]?.ToObject<Vector3>() ?? ScaleOffset;

    private void ApplyPosition(Vector3 off)
    {
        foreach (var targets in _allTargets)
        ApplyPositionSingle(targets, off);
    }

    private void ApplyScale(Vector3 off)
    {
        foreach (var targets in _allTargets)
        ApplyScaleSingle(targets, off);
    }

    private void ApplyRotation(float off)
    {
        foreach (var targets in _allTargets)
        ApplyRotationSingle(targets, off);
    }

    private static void ApplyPositionSingle(List<(Transform xf, Vector3 basePos, Quaternion baseRot, Vector3 baseScale)> targets, Vector3 off)
    {
        if (targets is null) return;
        foreach (var (xf, basePos, _, _) in targets)
        if (xf) xf.localPosition = basePos + off;
    }

    private static void ApplyScaleSingle(List<(Transform xf, Vector3 basePos, Quaternion baseRot, Vector3 baseScale)> targets, Vector3 off)
    {
        if (targets is null) return;
        foreach (var (xf, _, _, baseScale) in targets)
        if (xf) xf.localScale = baseScale + off;
    }

    private static void ApplyRotationSingle(List<(Transform xf, Vector3 basePos, Quaternion baseRot, Vector3 baseScale)> targets, float off)
    {
        if (targets is null) return;
        foreach (var (xf, _, baseRot, _) in targets)
        if (xf) xf.localRotation = baseRot * Quaternion.Euler(Vector3.forward * off);
    }
}