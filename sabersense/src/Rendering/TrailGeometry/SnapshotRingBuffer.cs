// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.Rendering.TrailGeometry;

internal sealed class SnapshotRingBuffer
{
    public struct Snapshot
    {
        public Vector3 PointStart;
        public Vector3 PointEnd;
        public Vector3 Pos => (PointStart + PointEnd) * 0.5f;
        public Vector3 Normal => PointEnd - PointStart;
    }

    private readonly Snapshot[] _snapshots;
    private readonly float[] _distances;
    private float _totalDistance;
    private int _headIdx;
    private int _count;

    public int Capacity { get; }
    public int Count => _count;

    public SnapshotRingBuffer(int capacity)
    {
        Capacity = Mathf.Max(2, capacity);
        _snapshots = new Snapshot[Capacity];
        _distances = new float[Capacity];
    }

    public void WriteAtHead(Snapshot snap)
    {
        _snapshots[_headIdx] = snap;
    }

    public void RecalculateDistances()
    {
        _distances[0] = 0;
        _totalDistance = 0;

        if (_count is < 2) return;

        var prev = Get(0);
        for (int i = 1; i < _count; i++)
        {
            var cur = Get(i);
            float dist = (cur.Pos - prev.Pos).magnitude;
            _totalDistance += dist;
            _distances[i] = _totalDistance;
            prev = cur;
        }
    }

    public void AdvanceHead()
    {
        _headIdx = (_headIdx - 1 + Capacity) % Capacity;
        if (_count < Capacity) _count++;
    }

    public Snapshot Get(int index)
    {
        if (_count is 0) return default;
        index = Mathf.Clamp(index, 0, _count - 1);
        return _snapshots[(_headIdx + index) % Capacity];
    }

    public void InitFill(Snapshot snap)
    {
        for (int i = 0; i < Capacity; i++)
        _snapshots[i] = snap;
        _count = Capacity;
        _headIdx = 0;
    }

    public int FindIndexByDistance(float t, out float localT)
    {
        if (_count is < 2)
        {
            localT = 0;
            return 0;
        }

        float targetDist = t * _totalDistance;

        int lo = 0, hi = _count - 1;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (_distances[mid] < targetDist) lo = mid + 1;
            else hi = mid;
        }

        int i = lo;
        if (i == 0)
        {
            localT = 0;
            return 0;
        }
        if (_distances[i] < targetDist)
        {
            localT = 1;
            return Mathf.Max(_count - 2, 0);
        }

        float prevDist = _distances[i - 1];
        float segLen = _distances[i] - prevDist;
        localT = segLen > 0 ? (targetDist - prevDist) / segLen : 0;
        return i - 1;
    }
}