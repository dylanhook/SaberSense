// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;

namespace SaberSense.Core.Logging;

internal sealed class ScopedLogger : IModLogger
{
    private readonly LoggingLogger _root;
    private readonly string _source;

    internal ScopedLogger(LoggingLogger root, string source)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _source = source;
    }

    public void Debug(string message) => _root.Log(LogLevel.Debug, _source, message);
    public void Info(string message) => _root.Log(LogLevel.Info, _source, message);
    public void Warn(string message) => _root.Log(LogLevel.Warn, _source, message);
    public void Error(string message) => _root.Log(LogLevel.Error, _source, message);

    public void Error(Exception exception) =>
    _root.Log(LogLevel.Error, _source, exception?.ToString() ?? "(null exception)");

    public IModLogger ForSource(string source) => new ScopedLogger(_root, source);
}