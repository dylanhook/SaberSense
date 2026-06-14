// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using IPA.Logging;
using System;

namespace SaberSense.Core.Logging;

internal sealed class IPALoggerAdapter : IModLogger
{
    private readonly Logger _inner;

    public IPALoggerAdapter(Logger logger) =>
    _inner = logger ?? throw new ArgumentNullException(nameof(logger));

    public void Debug(string message) => _inner.Debug(message);
    public void Info(string message) => _inner.Info(message);
    public void Warn(string message) => _inner.Warn(message);
    public void Error(string message) => _inner.Error(message);
    public void Error(Exception exception) => _inner.Error(exception);

    public IModLogger ForSource(string source) =>
    throw new InvalidOperationException(
    $"ForSource(\"{source}\") called on raw IPALoggerAdapter. " +
    "Use LoggingLogger.ForSource instead - the adapter must be wrapped by LoggingLogger for sinks to work.");
}