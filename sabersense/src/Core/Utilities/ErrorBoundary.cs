// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SaberSense.Core.Utilities;

internal static class ErrorBoundary
{
    public static void Run(Action action, IModLogger log, [CallerMemberName] string? context = null)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            LogError(log, context, ex);
        }
    }

    public static void FireAndForget(Task task, IModLogger log, [CallerMemberName] string? context = null)
    {
        task.ContinueWith(t =>
        {
            if (t.Exception is { } ex)
            LogError(log, context, ex.Flatten().InnerException ?? ex);
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private static void LogError(IModLogger? log, string? context, Exception ex)
    {
        var source = context ?? "Unknown";
        if (log is not null)
        log.ForSource(source).Error(ex.ToString());
        else
        UnityEngine.Debug.LogError($"[SaberSense.{source}] {ex}");
    }
}