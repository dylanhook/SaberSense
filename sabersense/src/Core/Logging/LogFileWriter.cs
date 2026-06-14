// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.IO;

namespace SaberSense.Core.Logging;

internal sealed class LogFileWriter : ILogSink, IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    private bool _disposed;

    public string FilePath { get; }

    public LogFileWriter(string filePath)
    {
        FilePath = filePath;

        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        Directory.CreateDirectory(dir);

        _writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8)
        {
            AutoFlush = true
        };

        _writer.WriteLine($"=== SaberSense Log - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        _writer.WriteLine();
    }

    public void Push(LogEntry entry)
    {
        if (_disposed) return;
        lock (_lock)
        {
            if (_disposed) return;
            try
            {
                _writer.WriteLine(entry.ToPlainText());
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        lock (_lock)
        {
            _disposed = true;
            try { _writer.Dispose(); }
            catch {  }
        }
    }
}