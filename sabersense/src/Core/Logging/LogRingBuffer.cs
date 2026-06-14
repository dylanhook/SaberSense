// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Messaging;
using System.Collections.Generic;

namespace SaberSense.Core.Logging;

internal sealed class LogRingBuffer : ILogSink
{
    private const int Capacity = 512;

    private readonly LogEntry[] _buffer = new LogEntry[Capacity];
    private readonly object _lock = new();
    private int _head;
    private int _count;
    private IMessageBroker? _broker;

    public void SetBroker(IMessageBroker broker) => _broker = broker;

    public void Push(LogEntry entry)
    {
        lock (_lock)
        {
            _buffer[_head] = entry;
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;
        }

        try { _broker?.Publish(new LogEntryMsg(entry)); }
        catch {  }
    }

    public List<LogEntry> GetEntries(LogLevel minLevel = LogLevel.Debug)
    {
        lock (_lock)
        {
            var result = new List<LogEntry>(_count);
            int start = (_head - _count + Capacity) % Capacity;
            for (int i = 0; i < _count; i++)
            {
                var entry = _buffer[(start + i) % Capacity];
                if (entry.Level >= minLevel)
                result.Add(entry);
            }
            return result;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _head = 0;
            _count = 0;
        }
    }
}