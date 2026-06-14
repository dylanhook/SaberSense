// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;

namespace SaberSense.Core.Logging;

internal readonly struct LogEntry
{
    public readonly DateTime Timestamp;
    public readonly LogLevel Level;
    public readonly string? Source;
    public readonly string Message;

    public LogEntry(DateTime timestamp, LogLevel level, string? source, string message)
    {
        Timestamp = timestamp;
        Level = level;
        Source = source;
        Message = message;
    }

    public string ToPlainText()
    {
        var tag = Level switch
        {
            LogLevel.Debug => "DBG",
            LogLevel.Info => "INF",
            LogLevel.Warn => "WRN",
            LogLevel.Error => "ERR",
            _ => "???"
        };
        return string.IsNullOrEmpty(Source)
        ? $"[{Timestamp:HH:mm:ss.fff}] [{tag}] {Message}"
        : $"[{Timestamp:HH:mm:ss.fff}] [{tag}] [{Source}] {Message}";
    }
}