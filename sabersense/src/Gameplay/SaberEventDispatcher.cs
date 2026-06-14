// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SaberSense.Gameplay;

internal enum SaberEventType
{
    OnSlice,
    OnComboBreak,
    MultiplierUp,
    SaberStartColliding,
    SaberStopColliding,
    OnLevelStart,
    OnLevelFail,
    OnLevelEnded,
    OnBlueLightOn,
    OnRedLightOn,
}

internal readonly struct ResolvedCall
{
    public readonly Object Target;
    public readonly string Description;
    private readonly Action _compiled;

    public ResolvedCall(Object target, MethodInfo method, object?[] args)
    {
        Target = target;
        Description = $"{target.name}.{method.Name}({string.Join(", ", args)})";

        _compiled = args.Length switch
        {
            0 => () => method.Invoke(target, null),
            _ => () => method.Invoke(target, args),
        };

        if (target is GameObject go && method.Name == "SetActive" && args.Length is 1 && args[0] is bool boolVal)
        _compiled = () => go.SetActive(boolVal);
    }

    public void Invoke()
    {
        if (Target == null) return;
        try { _compiled(); }
        catch (Exception ex) { ModLogger.ForSource("EventDispatcher").Warn($"Event call failed: {Target.name}: {ex.Message}"); }
    }
}

internal readonly struct ResolvedParamCall<T>
{
    public readonly Object Target;
    public readonly string Description;
    private readonly Action<T> _compiled;

    public ResolvedParamCall(Object target, MethodInfo method)
    {
        Target = target;
        Description = $"{target.name}.{method.Name}(<{typeof(T).Name}>)";

        var args = new object[1];
        _compiled = val =>
        {
            args[0] = val!;
            method.Invoke(target, args);
        };
    }

    public void Invoke(T value)
    {
        if (Target == null) return;
        try { _compiled(value); }
        catch (Exception ex) { ModLogger.ForSource("EventDispatcher").Warn($"Event call failed: {Target.name}: {ex.Message}"); }
    }
}

internal sealed class ResolvedComboFilter
{
    public int ComboTarget { get; init; }
    public List<ResolvedCall> Calls { get; init; } = [];
}

internal sealed class ResolvedNthComboFilter
{
    public int ComboStep { get; init; }
    public List<ResolvedCall> Calls { get; init; } = [];
}

internal sealed class ResolvedAccuracyFilter
{
    public float Target { get; init; }
    public List<ResolvedCall> OnReachTarget { get; init; } = [];
    public List<ResolvedCall> OnHigherThanTarget { get; init; } = [];
    public List<ResolvedCall> OnLowerThanTarget { get; init; } = [];
    internal float PreviousAccuracy = 1f;
}

internal sealed class SaberEventDispatcher : MonoBehaviour
{
    private readonly Dictionary<SaberEventType, List<ResolvedCall>> _calls = [];
    private readonly List<ResolvedParamCall<int>> _onComboChangedCalls = [];
    private readonly List<ResolvedParamCall<float>> _onAccuracyChangedCalls = [];
    private readonly List<ResolvedComboFilter> _comboFilters = [];
    private readonly List<ResolvedNthComboFilter> _nthComboFilters = [];
    private readonly List<ResolvedAccuracyFilter> _accuracyFilters = [];

    internal bool HasAnyCalls { get; private set; }

    internal void RegisterCalls(SaberEventType eventType, List<ResolvedCall> calls)
    {
        if (calls.Count is 0) return;
        if (!_calls.TryGetValue(eventType, out var list))
        {
            list = [];
            _calls[eventType] = list;
        }
        list.AddRange(calls);
        HasAnyCalls = true;
    }

    internal void RegisterComboChangedCalls(List<ResolvedParamCall<int>> calls)
    {
        if (calls.Count is 0) return;
        _onComboChangedCalls.AddRange(calls);
        HasAnyCalls = true;
    }

    internal void RegisterAccuracyChangedCalls(List<ResolvedParamCall<float>> calls)
    {
        if (calls.Count is 0) return;
        _onAccuracyChangedCalls.AddRange(calls);
        HasAnyCalls = true;
    }

    internal void RegisterComboFilter(ResolvedComboFilter filter) { _comboFilters.Add(filter); HasAnyCalls = true; }
    internal void RegisterNthComboFilter(ResolvedNthComboFilter filter) { _nthComboFilters.Add(filter); HasAnyCalls = true; }
    internal void RegisterAccuracyFilter(ResolvedAccuracyFilter filter) { _accuracyFilters.Add(filter); HasAnyCalls = true; }

    internal void Fire(SaberEventType eventType)
    {
        if (!_calls.TryGetValue(eventType, out var calls)) return;
        foreach (var call in calls) call.Invoke();
    }

    internal void FireComboChanged(int combo)
    {
        foreach (var call in _onComboChangedCalls) call.Invoke(combo);

        foreach (var filter in _comboFilters)
        {
            if (combo == filter.ComboTarget)
            foreach (var call in filter.Calls) call.Invoke();
        }

        foreach (var filter in _nthComboFilters)
        {
            if (filter.ComboStep > 0 && combo % filter.ComboStep == 0 && combo != 0)
            foreach (var call in filter.Calls) call.Invoke();
        }
    }

    internal void FireAccuracyChanged(float accuracy)
    {
        foreach (var call in _onAccuracyChangedCalls) call.Invoke(accuracy);

        foreach (var filter in _accuracyFilters)
        {
            float prev = filter.PreviousAccuracy;
            float target = filter.Target;

            if ((prev > target && accuracy < target) || (prev < target && accuracy > target))
            foreach (var call in filter.OnReachTarget) call.Invoke();

            if (prev < target && accuracy > target)
            foreach (var call in filter.OnHigherThanTarget) call.Invoke();

            if (prev > target && accuracy < target)
            foreach (var call in filter.OnLowerThanTarget) call.Invoke();

            filter.PreviousAccuracy = accuracy;
        }
    }
}