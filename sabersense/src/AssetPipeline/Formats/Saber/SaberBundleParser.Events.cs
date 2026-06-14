// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.BundleFormat;
using System.Collections.Generic;

namespace SaberSense.AssetPipeline.Formats.Saber;

public sealed partial class SaberBundleParser
{
    private static EventManagerEntry ReadEventManager(SerializedObject obj)
    {
        var goRef = obj.GetChild("m_GameObject");
        return new()
        {
            HostGameObjectPathId = goRef?.GetLong("m_PathID") ?? 0,
            OnSlice = ReadUnityEvent(obj.GetChild("OnSlice")),
            OnComboBreak = ReadUnityEvent(obj.GetChild("OnComboBreak")),
            MultiplierUp = ReadUnityEvent(obj.GetChild("MultiplierUp")),
            SaberStartColliding = ReadUnityEvent(obj.GetChild("SaberStartColliding")),
            SaberStopColliding = ReadUnityEvent(obj.GetChild("SaberStopColliding")),
            OnLevelStart = ReadUnityEvent(obj.GetChild("OnLevelStart")),
            OnLevelFail = ReadUnityEvent(obj.GetChild("OnLevelFail")),
            OnLevelEnded = ReadUnityEvent(obj.GetChild("OnLevelEnded")),
            OnBlueLightOn = ReadUnityEvent(obj.GetChild("OnBlueLightOn")),
            OnRedLightOn = ReadUnityEvent(obj.GetChild("OnRedLightOn")),
            OnComboChanged = ReadUnityEvent(obj.GetChild("OnComboChanged")),
            OnAccuracyChanged = ReadUnityEvent(obj.GetChild("OnAccuracyChanged")),
        };
    }

    private static ComboFilterEntry ReadComboFilter(SerializedObject obj)
    {
        var goRef = obj.GetChild("m_GameObject");
        return new()
        {
            HostGameObjectPathId = goRef?.GetLong("m_PathID") ?? 0,
            ComboTarget = obj.GetInt("ComboTarget", 50),
            NthComboReached = ReadUnityEvent(obj.GetChild("NthComboReached")),
        };
    }

    private static EveryNthComboEntry ReadEveryNthComboFilter(SerializedObject obj)
    {
        var goRef = obj.GetChild("m_GameObject");
        return new()
        {
            HostGameObjectPathId = goRef?.GetLong("m_PathID") ?? 0,
            ComboStep = obj.GetInt("ComboStep", 50),
            NthComboReached = ReadUnityEvent(obj.GetChild("NthComboReached")),
        };
    }

    private static AccuracyFilterEntry ReadAccuracyFilter(SerializedObject obj)
    {
        var goRef = obj.GetChild("m_GameObject");
        return new()
        {
            HostGameObjectPathId = goRef?.GetLong("m_PathID") ?? 0,
            Target = obj.GetFloat("Target", 1f),
            OnAccuracyReachTarget = ReadUnityEvent(obj.GetChild("OnAccuracyReachTarget")),
            OnAccuracyHigherThanTarget = ReadUnityEvent(obj.GetChild("OnAccuracyHigherThanTarget")),
            OnAccuracyLowerThanTarget = ReadUnityEvent(obj.GetChild("OnAccuracyLowerThanTarget")),
        };
    }

    private static List<EventCallEntry> ReadUnityEvent(SerializedObject? eventObj)
    {
        var result = new List<EventCallEntry>();
        if (eventObj is null) return result;

        var persistentCalls = eventObj.GetChild("m_PersistentCalls");
        if (persistentCalls is null) return result;

        if (persistentCalls["m_Calls"] is not List<object> callList)
        return result;

        foreach (var element in callList)
        {
            if (element is not SerializedObject call) continue;

            int callState = call.GetInt("m_CallState");
            if (callState is 0) continue;

            var methodName = call.GetString("m_MethodName");
            if (string.IsNullOrEmpty(methodName)) continue;

            var targetRef = call.GetChild("m_Target");
            long targetPathId = targetRef?.GetLong("m_PathID") ?? 0;
            if (targetPathId is 0) continue;

            var args = call.GetChild("m_Arguments");

            result.Add(new()
            {
                TargetPathId = targetPathId,
                MethodName = methodName!,
                Mode = call.GetInt("m_Mode"),
                CallState = callState,
                ObjectArgumentPathId = args?.GetChild("m_ObjectArgument")?.GetLong("m_PathID") ?? 0,
                IntArgument = args?.GetInt("m_IntArgument") ?? 0,
                FloatArgument = args?.GetFloat("m_FloatArgument") ?? 0f,
                StringArgument = args?.GetString("m_StringArgument") ?? string.Empty,
                BoolArgument = args?.GetBool("m_BoolArgument") ?? false,
            });
        }

        return result;
    }
}