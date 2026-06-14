// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Gameplay;

internal sealed class NetworkEmitter
{
    private static readonly Vector3 BoxHalf = new(20f, 10f, 20f);
    private const float ConnectionDist = 5f;
    private const int MaxLines = 500;
    private const float PlayerClearSqr = 2.5f * 2.5f;

    private const int MinParticleCount = 50;
    private const int MaxParticleCount = 400;

    private const float MinVelocity = 0.3f;
    private const float MaxVelocity = 0.8f;

    private const float InfiniteLifetime = 9999f;
    private const float MinDotSize = 0.025f;
    private const float MaxDotSize = 0.055f;
    private const float LineWidth = 0.012f;
    private const float MinLineWidth = 0.004f;
    private const float MaxLineWidth = 0.014f;

    private static readonly Color DefaultDotColor = new(0.4f, 0.8f, 1f, 0.9f);
    private static readonly Color DefaultLineColor = new(0.4f, 0.8f, 1f, 0.6f);

    private const int GX = 8, GY = 4, GZ = 8;
    private const int InitialCellCapacity = 8;
    private readonly List<int>[] _grid = new List<int>[GX * GY * GZ];

    private readonly int _count;
    private readonly Vector3[] _pos;
    private readonly Vector3[] _vel;
    private readonly ParticleSystem.Particle[] _particleBuf;
    private readonly ParticleSystem _renderer;
    private readonly List<LineRenderer> _lines = [];
    private readonly ModSettings _settings;

    public GameObject Root { get; }

    public NetworkEmitter(Transform parent, float strength, ProceduralMaterialFactory materialFactory, ModSettings settings)
    {
        _settings = settings;

        Root = new GameObject("Network");
        Root.transform.SetParent(parent, false);

        _count = (int)Mathf.Lerp(MinParticleCount, MaxParticleCount, strength);
        _pos = new Vector3[_count];
        _vel = new Vector3[_count];
        _particleBuf = new ParticleSystem.Particle[_count];

        for (int c = 0; c < _grid.Length; c++) _grid[c] = new(InitialCellCapacity);
        InitializePoints();
        _renderer = CreateParticleRenderer(materialFactory);
        SeedParticles();
        CreateLinePool(materialFactory);
    }

    private void InitializePoints()
    {
        for (int i = 0; i < _count; i++)
        {
            _pos[i] = new Vector3(
            Random.Range(-BoxHalf.x, BoxHalf.x),
            Random.Range(-BoxHalf.y, BoxHalf.y),
            Random.Range(-BoxHalf.z, BoxHalf.z)
            );
            _vel[i] = Random.onUnitSphere * Random.Range(MinVelocity, MaxVelocity);
        }
    }

    private ParticleSystem CreateParticleRenderer(ProceduralMaterialFactory materialFactory)
    {
        var ps = Root.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = _count;
        main.startLifetime = InfiniteLifetime;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(MinDotSize, MaxDotSize);
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.enabled = false;

        var renderer = Root.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = materialFactory.GetSoftParticleMaterial();
        return ps;
    }

    private void SeedParticles()
    {
        var camPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        Color dotColor = GetOverrideOrDefault(DefaultDotColor);

        for (int i = 0; i < _count; i++)
        {
            _pos[i] += camPos;
            _particleBuf[i].position = _pos[i];
            _particleBuf[i].startLifetime = InfiniteLifetime;
            _particleBuf[i].remainingLifetime = InfiniteLifetime;
            _particleBuf[i].startSize = Random.Range(MinDotSize, MaxDotSize);
            _particleBuf[i].startColor = dotColor;
        }
        _renderer.SetParticles(_particleBuf, _count);
        _renderer.Play();
    }

    private void CreateLinePool(ProceduralMaterialFactory materialFactory)
    {
        var lineMat = materialFactory.GetLineMaterial();
        for (int i = 0; i < MaxLines; i++)
        {
            var lineGO = new GameObject("NLine" + i);
            lineGO.transform.SetParent(Root.transform, false);
            var lr = lineGO.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.startWidth = LineWidth;
            lr.endWidth = LineWidth;
            lr.material = lineMat;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            _lines.Add(lr);
        }
    }

    public void Tick(float dt, Vector3 camPos)
    {
        if (_renderer == null) return;

        MoveAndWrapPoints(dt, camPos);
        _renderer.SetParticles(_particleBuf, _count);
        DrawConnections(camPos);
    }

    private void MoveAndWrapPoints(float dt, Vector3 camPos)
    {
        var bh = BoxHalf;
        for (int i = 0; i < _count; i++)
        {
            _pos[i] += _vel[i] * dt;

            float ox = _pos[i].x - camPos.x;
            float oy = _pos[i].y - camPos.y;
            float oz = _pos[i].z - camPos.z;

            if (ox > bh.x) _pos[i].x -= bh.x * 2f;
            else if (ox < -bh.x) _pos[i].x += bh.x * 2f;
            if (oy > bh.y) _pos[i].y -= bh.y * 2f;
            else if (oy < -bh.y) _pos[i].y += bh.y * 2f;
            if (oz > bh.z) _pos[i].z -= bh.z * 2f;
            else if (oz < -bh.z) _pos[i].z += bh.z * 2f;

            _particleBuf[i].position = _pos[i];
            _particleBuf[i].remainingLifetime = InfiniteLifetime;
        }
    }

    private static int GridCell(float pos, float boxMin, int cellCount)
    {
        int c = (int)((pos - boxMin) / ConnectionDist);
        return Mathf.Clamp(c, 0, cellCount - 1);
    }

    private void DrawConnections(Vector3 camPos)
    {
        float gridMinX = camPos.x - BoxHalf.x;
        float gridMinY = camPos.y - BoxHalf.y;
        float gridMinZ = camPos.z - BoxHalf.z;

        for (int c = 0; c < _grid.Length; c++) _grid[c].Clear();
        for (int i = 0; i < _count; i++)
        {
            int cx = GridCell(_pos[i].x, gridMinX, GX);
            int cy = GridCell(_pos[i].y, gridMinY, GY);
            int cz = GridCell(_pos[i].z, gridMinZ, GZ);
            _grid[cx * GY * GZ + cy * GZ + cz].Add(i);
        }

        int lineIdx = 0;
        Color baseColor = GetOverrideOrDefault(DefaultLineColor);
        float connDistSqr = ConnectionDist * ConnectionDist;

        for (int i = 0; i < _count && lineIdx < _lines.Count; i++)
        {
            float pdx = _pos[i].x - camPos.x;
            float pdy = _pos[i].y - camPos.y;
            float pdz = _pos[i].z - camPos.z;
            if (pdx * pdx + pdy * pdy + pdz * pdz < PlayerClearSqr) continue;

            DrawConnectionsForParticle(i, camPos, baseColor, connDistSqr, gridMinX, gridMinY, gridMinZ, ref lineIdx);
        }

        for (int k = lineIdx; k < _lines.Count; k++)
        {
            if (_lines[k] != null) _lines[k].enabled = false;
        }
    }

    private void DrawConnectionsForParticle(int i, Vector3 camPos, Color baseColor, float connDistSqr,
    float gridMinX, float gridMinY, float gridMinZ, ref int lineIdx)
    {
        int cx = GridCell(_pos[i].x, gridMinX, GX);
        int cy = GridCell(_pos[i].y, gridMinY, GY);
        int cz = GridCell(_pos[i].z, gridMinZ, GZ);

        for (int nx = cx - 1; nx <= cx + 1 && lineIdx < _lines.Count; nx++)
        {
            if (nx < 0 || nx >= GX) continue;
            for (int ny = cy - 1; ny <= cy + 1 && lineIdx < _lines.Count; ny++)
            {
                if (ny < 0 || ny >= GY) continue;
                for (int nz = cz - 1; nz <= cz + 1 && lineIdx < _lines.Count; nz++)
                {
                    if (nz < 0 || nz >= GZ) continue;
                    var cell = _grid[nx * GY * GZ + ny * GZ + nz];
                    for (int ci = 0; ci < cell.Count && lineIdx < _lines.Count; ci++)
                    TryDrawLine(i, cell[ci], camPos, baseColor, connDistSqr, ref lineIdx);
                }
            }
        }
    }

    private void TryDrawLine(int i, int j, Vector3 camPos, Color baseColor, float connDistSqr, ref int lineIdx)
    {
        if (j <= i) return;

        float qdx = _pos[j].x - camPos.x;
        float qdy = _pos[j].y - camPos.y;
        float qdz = _pos[j].z - camPos.z;
        if (qdx * qdx + qdy * qdy + qdz * qdz < PlayerClearSqr) return;

        float dx = _pos[i].x - _pos[j].x;
        float dy = _pos[i].y - _pos[j].y;
        float dz = _pos[i].z - _pos[j].z;
        float dSqr = dx * dx + dy * dy + dz * dz;
        if (dSqr >= connDistSqr) return;

        var lr = _lines[lineIdx];
        if (lr == null) return;
        lr.enabled = true;
        lr.SetPosition(0, _particleBuf[i].position);
        lr.SetPosition(1, _particleBuf[j].position);

        float dist = Mathf.Sqrt(dSqr);
        float t = 1f - (dist / ConnectionDist);
        var c = baseColor;
        c.a *= t;
        lr.startColor = c;
        lr.endColor = c;

        float w = Mathf.Lerp(MinLineWidth, MaxLineWidth, t);
        lr.startWidth = w;
        lr.endWidth = w;
        lineIdx++;
    }

    private Color GetOverrideOrDefault(Color defaultColor)
    {
        if (_settings is null || !_settings.WorldMod.OverrideColor) return defaultColor;
        return _settings.WorldMod.NetworkColor;
    }
}