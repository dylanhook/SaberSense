// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Rendering.TrailGeometry;

internal sealed class TrailMeshBuilder
{
    private readonly List<Vector3> _vertices = [];
    private readonly List<Vector2> _uvs = [];
    private readonly List<Color> _colors = [];
    private readonly List<int> _indices = [];

    public int Granularity { get; private set; }

    public void Init(Mesh mesh, int granularity)
    {
        Granularity = Mathf.Max(2, granularity);

        int vertCount = Granularity * 2;

        _vertices.Clear();
        _uvs.Clear();
        _colors.Clear();
        _indices.Clear();

        _vertices.Capacity = vertCount;
        _uvs.Capacity = vertCount;
        _colors.Capacity = vertCount;
        _indices.Capacity = (Granularity - 1) * 6;

        for (int i = 0; i < vertCount; i++)
        {
            _vertices.Add(Vector3.zero);
            _uvs.Add(Vector2.zero);
            _colors.Add(Color.white);
        }

        for (int i = 0; i < Granularity - 1; i++)
        {
            int b = i * 2;
            int n = b + 2;

            _indices.Add(b);
            _indices.Add(n);
            _indices.Add(b + 1);

            _indices.Add(b + 1);
            _indices.Add(n);
            _indices.Add(n + 1);
        }

        mesh.SetVertices(_vertices);
        mesh.SetUVs(0, _uvs);
        mesh.SetColors(_colors);
        mesh.SetTriangles(_indices, 0);
    }

    public void Update(
    Mesh mesh,
    SnapshotRingBuffer buffer,
    Vector3 playerOffset,
    bool localSpaceTrails,
    float trailWidth,
    float whiteStep,
    Color color,
    Bounds hugeBounds)
    {
        float halfWidth = trailWidth * 0.5f;
        float tDenominator = Granularity > 1 ? Granularity - 1 : 1;

        for (int i = 0; i < Granularity; i++)
        {
            float t = i / tDenominator;
            CatmullRomInterpolator.Sample(buffer, t, out var pos, out var rawUp);
            if (localSpaceTrails) pos += playerOffset;

            var up = rawUp.sqrMagnitude > 1e-6f ? rawUp.normalized : Vector3.up;

            var c = whiteStep > 0 && t < whiteStep
            ? Color.LerpUnclamped(Color.white, color, t / whiteStep)
            : color;

            int b = i * 2;

            _vertices[b] = pos + up * halfWidth;
            _colors[b] = c;
            _uvs[b] = new Vector2(0f, t);

            _vertices[b + 1] = pos - up * halfWidth;
            _colors[b + 1] = c;
            _uvs[b + 1] = new Vector2(1f, t);
        }

        mesh.SetVertices(_vertices);
        mesh.SetUVs(0, _uvs);
        mesh.SetColors(_colors);

        mesh.bounds = hugeBounds;
    }
}