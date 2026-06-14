// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.AssetPipeline.Formats.Saber;
using SaberSense.Core.Logging;
using SaberSense.Gameplay;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.AssetPipeline.Assembly;

internal static class EventInjector
{
    private static readonly IModLogger Log = ModLogger.ForSource(nameof(EventInjector));

    internal static SaberEventDispatcher? InjectEvents(
    GameObject root, SaberParseResult parseResult, GameObject? eventTargetContainer = null)
    {
        if (root == null || parseResult?.Events is null) return null;
        if (!parseResult.HasEvents) return null;

        var events = parseResult.Events;

        var goByName = new Dictionary<string, GameObject>();
        InjectionHelpers.CollectGameObjectsByName(root.transform, goByName);

        if (eventTargetContainer != null)
        InjectionHelpers.CollectGameObjectsByName(eventTargetContainer.transform, goByName);

        var dispatcher = root.AddComponent<SaberEventDispatcher>();
        int totalCalls = 0;

        foreach (var entry in events.EventManagers)
        {
            totalCalls += InjectEventList(dispatcher, SaberEventType.OnSlice, entry.OnSlice, parseResult, goByName);
            totalCalls += InjectEventList(dispatcher, SaberEventType.OnComboBreak, entry.OnComboBreak, parseResult, goByName);
            totalCalls += InjectEventList(dispatcher, SaberEventType.MultiplierUp, entry.MultiplierUp, parseResult, goByName);
            totalCalls += InjectEventList(dispatcher, SaberEventType.SaberStartColliding, entry.SaberStartColliding, parseResult, goByName);
            totalCalls += InjectEventList(dispatcher, SaberEventType.SaberStopColliding, entry.SaberStopColliding, parseResult, goByName);
            totalCalls += InjectEventList(dispatcher, SaberEventType.OnLevelStart, entry.OnLevelStart, parseResult, goByName);
            totalCalls += InjectEventList(dispatcher, SaberEventType.OnLevelFail, entry.OnLevelFail, parseResult, goByName);
            totalCalls += InjectEventList(dispatcher, SaberEventType.OnLevelEnded, entry.OnLevelEnded, parseResult, goByName);
            totalCalls += InjectEventList(dispatcher, SaberEventType.OnBlueLightOn, entry.OnBlueLightOn, parseResult, goByName);
            totalCalls += InjectEventList(dispatcher, SaberEventType.OnRedLightOn, entry.OnRedLightOn, parseResult, goByName);

            var comboCalls = ResolveParamCallList<int>(entry.OnComboChanged, parseResult, goByName, typeof(int));
            if (comboCalls.Count is > 0)
            {
                dispatcher.RegisterComboChangedCalls(comboCalls);
                foreach (var r in comboCalls) Log.Info($"  OnComboChanged -> {r.Description}");
                totalCalls += comboCalls.Count;
            }

            var accCalls = ResolveParamCallList<float>(entry.OnAccuracyChanged, parseResult, goByName, typeof(float));
            if (accCalls.Count is > 0)
            {
                dispatcher.RegisterAccuracyChangedCalls(accCalls);
                foreach (var r in accCalls) Log.Info($"  OnAccuracyChanged -> {r.Description}");
                totalCalls += accCalls.Count;
            }
        }

        foreach (var entry in events.ComboFilters)
        {
            var resolved = ResolveCallList(entry.NthComboReached, parseResult, goByName);
            if (resolved.Count is > 0)
            {
                dispatcher.RegisterComboFilter(new ResolvedComboFilter
                {
                    ComboTarget = entry.ComboTarget,
                    Calls = resolved
                });
                totalCalls += resolved.Count;
            }
        }

        foreach (var entry in events.NthComboFilters)
        {
            var resolved = ResolveCallList(entry.NthComboReached, parseResult, goByName);
            if (resolved.Count is > 0)
            {
                dispatcher.RegisterNthComboFilter(new ResolvedNthComboFilter
                {
                    ComboStep = entry.ComboStep,
                    Calls = resolved
                });
                totalCalls += resolved.Count;
            }
        }

        foreach (var entry in events.AccuracyFilters)
        {
            var onReach = ResolveCallList(entry.OnAccuracyReachTarget, parseResult, goByName);
            var onHigher = ResolveCallList(entry.OnAccuracyHigherThanTarget, parseResult, goByName);
            var onLower = ResolveCallList(entry.OnAccuracyLowerThanTarget, parseResult, goByName);

            if (onReach.Count is > 0 || onHigher.Count is > 0 || onLower.Count is > 0)
            {
                dispatcher.RegisterAccuracyFilter(new ResolvedAccuracyFilter
                {
                    Target = entry.Target,
                    OnReachTarget = onReach,
                    OnHigherThanTarget = onHigher,
                    OnLowerThanTarget = onLower
                });
                totalCalls += onReach.Count + onHigher.Count + onLower.Count;
            }
        }

        Log.Info($"Injected SaberEventDispatcher with {totalCalls} resolved call(s)");

        return dispatcher;
    }

    private static int InjectEventList(
    SaberEventDispatcher dispatcher,
    SaberEventType eventType,
    IReadOnlyList<EventCallEntry> calls,
    SaberParseResult parseResult,
    Dictionary<string, GameObject> goByName)
    {
        var resolved = ResolveCallList(calls, parseResult, goByName);
        if (resolved.Count is > 0)
        {
            dispatcher.RegisterCalls(eventType, resolved);
            foreach (var r in resolved)
            Log.Info($"  {eventType} -> {r.Description}");
        }
        return resolved.Count;
    }

    private static List<ResolvedCall> ResolveCallList(
    IReadOnlyList<EventCallEntry> calls,
    SaberParseResult parseResult,
    Dictionary<string, GameObject> goByName)
    {
        var resolved = new List<ResolvedCall>();
        if (calls is null || calls.Count is 0) return resolved;

        foreach (var call in calls)
        {
            var target = ResolveEventTarget(call.TargetPathId, parseResult, goByName);
            if (target == null)
            {
                Log.Debug(
                $"Could not resolve target pathId={call.TargetPathId} for {call.MethodName}");
                continue;
            }

            var method = target.GetType().GetMethod(
            call.MethodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
            null,
            GetParameterTypes(call.Mode),
            null);

            if (method is null)
            {
                Log.Debug(
                $"Method '{call.MethodName}' not found on {target.GetType().Name}");
                continue;
            }

            var args = BuildArguments(call, parseResult, goByName);
            resolved.Add(new ResolvedCall(target, method, args));
        }

        return resolved;
    }

    private static List<ResolvedParamCall<T>> ResolveParamCallList<T>(
    IReadOnlyList<EventCallEntry> calls,
    SaberParseResult parseResult,
    Dictionary<string, GameObject> goByName,
    Type paramType)
    {
        var resolved = new List<ResolvedParamCall<T>>();
        if (calls is null || calls.Count is 0) return resolved;

        var paramTypes = new[] { paramType };

        foreach (var call in calls)
        {
            var target = ResolveEventTarget(call.TargetPathId, parseResult, goByName);
            if (target == null)
            {
                Log.Debug(
                $"Could not resolve target pathId={call.TargetPathId} for {call.MethodName}");
                continue;
            }

            var resolvedParamTypes = call.Mode is 0 ? paramTypes : GetParameterTypes(call.Mode);

            var method = target.GetType().GetMethod(
            call.MethodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public,
            null,
            resolvedParamTypes,
            null);

            if (method is null)
            {
                Log.Debug(
                $"Method '{call.MethodName}' not found on {target.GetType().Name}");
                continue;
            }

            resolved.Add(new ResolvedParamCall<T>(target, method));
        }

        return resolved;
    }

    private static UnityEngine.Object? ResolveEventTarget(
    long pathId,
    SaberParseResult parseResult,
    Dictionary<string, GameObject> goByName)
    {
        if (pathId is 0) return null;

        if (!parseResult.PathIdToGameObjectName.TryGetValue(pathId, out var targetName))
        return null;

        if (!goByName.TryGetValue(targetName, out var go))
        {
            Log.Debug($"Target '{targetName}' not found in hierarchy");
            return null;
        }

        int typeId = 1;
        parseResult.Events?.PathIdToTypeId?.TryGetValue(pathId, out typeId);

        if (typeId is 1) return go;

        var componentType = UnityTypeIdToType(typeId);
        if (componentType is not null)
        return go.GetComponent(componentType);

        return go;
    }

    private static Type? UnityTypeIdToType(int typeId) => typeId switch
    {
        198 => typeof(ParticleSystem),
        95 => typeof(Animator),
        82 => typeof(AudioSource),
        108 => typeof(Light),
        212 => typeof(SpriteRenderer),
        23 => typeof(MeshRenderer),
        _ => null
    };

    private static Type[] GetParameterTypes(int mode) => mode switch
    {
        1 => Type.EmptyTypes,
        2 => new[] { typeof(UnityEngine.Object) },
        3 => new[] { typeof(int) },
        4 => new[] { typeof(float) },
        5 => new[] { typeof(string) },
        6 => new[] { typeof(bool) },
        _ => Type.EmptyTypes,
    };

    private static object?[] BuildArguments(
    EventCallEntry call, SaberParseResult parseResult, Dictionary<string, GameObject> goByName) => call.Mode switch
    {
        1 => [],
        2 => new object?[] { ResolveEventTarget(
            call.ObjectArgumentPathId, parseResult, goByName) },
        3 => new object[] { call.IntArgument },
        4 => new object[] { call.FloatArgument },
        5 => new object[] { call.StringArgument },
        6 => new object[] { call.BoolArgument },
        _ => [],
    };
}