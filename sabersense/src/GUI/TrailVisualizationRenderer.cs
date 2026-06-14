// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Utilities;
using SaberSense.Rendering;
using SaberSense.Rendering.TrailGeometry;
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SaberSense.GUI;

internal sealed class TrailVisualizationRenderer : IDisposable
{
    private const float UnitsToScale = 0.05f;
    private const int PreviewSortOrder = 3;
    private static readonly Quaternion MeshOrientation = Quaternion.Euler(-90f, 25f, 0f);

    public Material Material { get => GetMaterial()!; set => SetMaterial(value); }

    public bool OnlyColorVertex { set { foreach (var s in _sections) s.OnlyColorVertex = value; } }

    public float Length { set => SetLength(value); }

    private readonly List<TrailSegmentMesh> _sections = [];
    private readonly RotationBaseline _rotationBaseline = new();
    private GameObject? _prefab;
    private Mesh? _prefabMesh;
    private Material? _prefabMaterial;

    public TrailVisualizationRenderer(ShaderRegistry shaders)
    {
        (_prefab, _prefabMesh, _prefabMaterial) = CreateTrailPlane(shaders.SpritesDefault);
    }

    private static (GameObject go, Mesh mesh, Material mat) CreateTrailPlane(Shader shader)
    {
        var go = new GameObject("TrailPlane") { hideFlags = HideFlags.HideAndDontSave };

        var child = new GameObject("default");
        child.transform.SetParent(go.transform, false);

        var mesh = new Mesh
        {
            name = "TrailPlaneMesh",

            vertices = [
            new Vector3(0, 0, -1),
            new Vector3(0, 0, 0),
            new Vector3(-1, 0, 0),
            new Vector3(-1, 0, -1)
            ],
            uv = [
            new Vector2(1, 0),
            new Vector2(0, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
            ],
            triangles = [2, 1, 0, 3, 2, 0],
            normals = [Vector3.up, Vector3.up, Vector3.up, Vector3.up]
        };

        var mat = new Material(shader);

        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        go.SetActive(false);
        return (go, mesh, mat);
    }

    public void Create(Transform parent, LiveTrail trail, bool baseColorOnly)
    {
        foreach (var s in _sections) s.Destroy();
        _sections.Clear();
        if (_prefab == null) return;

        var (start, end) = trail.GetPoints();

        _sections.Add(new TrailSegmentMesh(0, parent, start, end, _prefab, _rotationBaseline) { OnlyColorVertex = baseColorOnly });

        for (var i = 0; i < trail.AuxTrails.Count; i++)
        {
            var sec = trail.AuxTrails[i];
            if (sec.Trail.PointStart && sec.Trail.PointEnd)
            {
                _sections.Add(new TrailSegmentMesh(i + 1, parent, sec.Trail.PointStart!, sec.Trail.PointEnd!, _prefab, _rotationBaseline, sec)
                {
                    OnlyColorVertex = baseColorOnly
                });
            }
        }

        Material = trail.Material!.Material!;
        Length = trail.Length;
        UpdateWidth();
    }

    public void SetMaterial(Material m)
    {
        if (_sections.Count is > 0) _sections[0].SetMaterial(m);
    }

    public void SetColor(Color c)
    {
        foreach (var s in _sections) s.SetColor(c);
    }

    public void UpdateWidth()
    {
        foreach (var s in _sections) s.UpdateWidth();
    }

    public void UpdatePosition()
    {
        foreach (var s in _sections) s.UpdatePosition();
    }

    public Material? GetMaterial() => _sections.Count is > 0 ? _sections[0].GetMaterial() : null;

    public void SetLength(float val)
    {
        foreach (var s in _sections) s.SetLength(val);
    }

    public void SetLayer(int layer)
    {
        foreach (var s in _sections) s.SetLayer(layer);
    }

    public void Destroy()
    {
        foreach (var s in _sections) s.Destroy();
        _sections.Clear();
    }

    public void Dispose()
    {
        Destroy();
        if (_prefabMaterial != null) { Object.Destroy(_prefabMaterial); _prefabMaterial = null; }
        if (_prefabMesh != null) { Object.Destroy(_prefabMesh); _prefabMesh = null; }
        if (_prefab != null) { Object.Destroy(_prefab); _prefab = null; }
    }

    private sealed class RotationBaseline
    {
        public Quaternion? Value;
    }

    private sealed class TrailSegmentMesh
    {
        public int TrailIdx { get; }
        public bool IsPrimaryTrail => TrailIdx == 0;
        public bool OnlyColorVertex { get; set; }

        private readonly GameObject _instance;
        private readonly Mesh _mesh;
        private readonly Transform _ptEnd;
        private readonly Transform _ptStart;
        private readonly Renderer _renderer = null!;
        private readonly LiveTrail.AuxTrailBinding? _handler;
        private readonly Transform _tx;

        private readonly Vector3[] _vertices = new Vector3[4];
        private readonly Color[] _colors = new Color[4];

        public TrailSegmentMesh(
        int idx,
        Transform parent,
        Transform pointStart,
        Transform pointEnd,
        GameObject prefab,
        RotationBaseline baseline,
        LiveTrail.AuxTrailBinding? handler = null)
        {
            TrailIdx = idx;
            _handler = handler;
            _ptStart = pointStart;
            _ptEnd = pointEnd;

            _instance = Object.Instantiate(prefab, _ptEnd.position, MeshOrientation, parent);
            _instance.SetActive(true);
            _instance.name = $"Trail preview {idx}";
            _tx = _instance.transform;

            if (!baseline.Value.HasValue)
            baseline.Value = _tx.localRotation;
            else
            _tx.localRotation = baseline.Value.Value;

            _renderer = _instance.GetComponentInChildren<Renderer>()!;
            var meshFilter = _instance.GetComponentInChildren<MeshFilter>();
            _mesh = meshFilter?.mesh!;

            if (_renderer != null) _renderer.sortingOrder = PreviewSortOrder;

            if (handler?.Trail.TrailMaterial is { } mat)
            {
                SetMaterial(mat);
            }
        }

        public void SetMaterial(Material mat)
        {
            if (_renderer) _renderer.sharedMaterial = mat;
        }

        public Material? GetMaterial() => _renderer ? _renderer.sharedMaterial : null;

        public void SetColor(Color color)
        {
            if (_renderer && _renderer.sharedMaterial is { } mat && !OnlyColorVertex && ShaderUtils.SupportsSaberColoring(mat))
            {
                _renderer.SetPropertyBlock(ShaderUtils.ColorBlock(color));
            }

            Array.Fill(_colors, color);
            _mesh.colors = _colors;
        }

        public void UpdateWidth()
        {
            if (!_instance || !_ptStart || !_ptEnd) return;

            var localStart = _tx.InverseTransformPoint(_ptStart.position);
            var localEnd = _tx.InverseTransformPoint(_ptEnd.position);

            _vertices[0] = new Vector3(0, 0, localStart.z);
            _vertices[1] = new Vector3(0, 0, localEnd.z);
            _vertices[2] = new Vector3(1, 0, localEnd.z);
            _vertices[3] = new Vector3(1, 0, localStart.z);
            _mesh.vertices = _vertices;
        }

        public void UpdatePosition()
        {
            if (_instance && _ptEnd && _ptStart)
            {
                _tx.position = _ptEnd.position;
                UpdateWidth();
            }
        }

        public void SetLength(float val)
        {
            if (_handler is null)
            {
                SetLengthInternal(val);
                return;
            }

            _handler.SyncLength((int)val);
            SetLengthInternal(_handler.Trail.Length);
        }

        public void SetLayer(int layer)
        {
            if (!_instance) return;

            _instance.layer = layer;
            foreach (Transform child in _tx)
            {
                child.gameObject.layer = layer;
            }
        }

        private void SetLengthInternal(float val)
        {
            var scale = _tx.localScale;
            scale.x = val * UnitsToScale;
            _tx.localScale = scale;
        }

        public void Destroy() => _instance.TryDestroy();
    }
}