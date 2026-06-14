// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;

namespace SaberSense.Behaviors;

public enum ModifierParamKind
{
    Bool,

    Float,
}

public sealed class ModifierParam
{
    public string Label { get; }

    public ModifierParamKind Kind { get; }

    public string? SectionHeader { get; }

    public float Min { get; }

    public float Max { get; }

    private readonly Func<bool>? _boolGetter;
    private readonly Action<bool>? _boolSetter;
    private readonly Func<float>? _floatGetter;
    private readonly Action<float>? _floatSetter;

    private ModifierParam(
    string label,
    ModifierParamKind kind,
    string? sectionHeader,
    float min,
    float max,
    Func<bool>? boolGetter,
    Action<bool>? boolSetter,
    Func<float>? floatGetter,
    Action<float>? floatSetter)
    {
        Label = label;
        Kind = kind;
        SectionHeader = sectionHeader;
        Min = min;
        Max = max;
        _boolGetter = boolGetter;
        _boolSetter = boolSetter;
        _floatGetter = floatGetter;
        _floatSetter = floatSetter;
    }

    public static ModifierParam Boolean(string label, Func<bool> get, Action<bool> set, string? sectionHeader = null) =>
    new(label, ModifierParamKind.Bool, sectionHeader, 0f, 0f, get, set, null, null);

    public static ModifierParam Number(string label, float min, float max, Func<float> get, Action<float> set, string? sectionHeader = null) =>
    new(label, ModifierParamKind.Float, sectionHeader, min, max, null, null, get, set);

    public bool GetBool() => _boolGetter!();

    public void SetBool(bool value) => _boolSetter!(value);

    public float GetFloat() => _floatGetter!();

    public void SetFloat(float value) => _floatSetter!(value);
}