// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System;
using System.Collections.Generic;

namespace SaberSense.Core.Messaging;

internal sealed class MessageBroker : IMessageBroker
{
    private readonly Dictionary<Type, List<Delegate>> _subscriptions = [];
    private readonly IModLogger _log;
    private readonly object _lock = new();
    private volatile bool _disposed;

    public MessageBroker(IModLogger log)
    {
        _log = log.ForSource(nameof(MessageBroker));
    }

    public void Publish<T>(T message) where T : struct
    {
        if (_disposed) return;

        List<Delegate> snapshot;
        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(typeof(T), out var handlers)) return;
            snapshot = [.. handlers];
        }

        foreach (var handler in snapshot)
        {
            try
            {
                ((Action<T>)handler)(message);
            }
            catch (Exception ex)
            {
                _log?.Error($"Handler for {typeof(T).Name} threw: {ex}");
            }
        }
    }

    public IDisposable Subscribe<T>(Action<T> handler) where T : struct
    {
        if (_disposed) return new Subscription(null);

        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(typeof(T), out var list))
            _subscriptions[typeof(T)] = list = [];
            list.Add(handler);
        }

        return new Subscription(() =>
        {
            lock (_lock)
            {
                if (_subscriptions.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
            }
        });
    }

    public void Dispose()
    {
        _disposed = true;
        lock (_lock) _subscriptions.Clear();
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _unsub;
        public Subscription(Action? unsub) => _unsub = unsub;

        public void Dispose()
        {
            _unsub?.Invoke();
            _unsub = null;
        }
    }
}