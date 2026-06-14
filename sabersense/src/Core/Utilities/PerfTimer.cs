// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System;
using System.Diagnostics;

namespace SaberSense.Core.Utilities;

public readonly record struct PerfTimer(string Label, Stopwatch Sw, IModLogger? Logger = null) : IDisposable
{
    public PerfTimer(string label = "Operation", IModLogger? logger = null)
    : this(label, Stopwatch.StartNew(), logger) { }

    public void Print(IModLogger logger)
    {
        Sw.Stop();
        logger.Info(FormatResult());
    }

    private string FormatResult() => $"{Label} completed in {Sw.Elapsed.TotalSeconds:F3}s";

    public void Dispose()
    {
        if (!Sw.IsRunning) return;
        Sw.Stop();

        if (Logger is not null) Logger.Info(FormatResult());
        else ModLogger.Info(FormatResult());
    }
}