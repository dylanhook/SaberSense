// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using SaberSense.Rendering.TrailGeometry;
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SaberSense.Rendering;

internal readonly record struct TrailSetup(
int Duration,
float WhiteFade,
Color Tint,
int Resolution,
int CaptureRate);

internal sealed class SaberTrail : MonoBehaviour
{
    private const int SaberLayer = 12;
    private const int SkipFirstFrames = 4;
    private const int DefaultTrailLength = 30;
    private const int DefaultGranularity = 60;
    private const int DefaultSamplingFrequency = 0;
    private const int MinGranularity = 2;

    private const float AdvanceRate = 90f;
    private const float AdvanceInterval = 1f / AdvanceRate;

    private const int MaxAdvancesPerFrame = 3;

    private static readonly Bounds HugeBounds = new(Vector3.zero, new Vector3(100000f, 100000f, 100000f));

    public Color Color = Color.white;
    public int Granularity = DefaultGranularity;
    public int SamplingFrequency = DefaultSamplingFrequency;
    public Material? Material;
    public Transform? PointEnd;
    public Transform? PointStart;
    public int TrailLength = DefaultTrailLength;
    public float WhiteStep;
    public bool LocalSpaceTrails { get; set; }
    public PlayerTransforms? PlayerTransforms { get; set; }

    private bool _inited;
    private int _frameNum;
    private float _advanceTimer;
    private float _sampleTimer;
    private float _sampleInterval;
    private bool _lateInitDone;
    private SnapshotRingBuffer.Snapshot _lastSnapshot;

    private Mesh _mesh = null!;
    private GameObject _meshObj = null!;
    private MeshFilter _meshFilter = null!;
    private MeshRenderer _meshRenderer = null!;

    private SnapshotRingBuffer _buffer = null!;
    private TrailMeshBuilder _meshBuilder = null!;

    private Vector3 GetPlayerOffset() => PlayerTransforms ? PlayerTransforms!.transform.position : Vector3.zero;

    private void OnEnable() { if (_meshObj) _meshObj.SetActive(true); }
    private void OnDisable() { if (_meshObj) _meshObj.SetActive(false); }
    private void OnDestroy()
    {
        if (_mesh) Destroy(_mesh);
        if (_meshObj) Destroy(_meshObj);
    }

    public void Setup(TrailSetup setup, Transform pointStart, Transform pointEnd, Material? material, bool editor)
    {
        PointStart = pointStart;
        PointEnd = pointEnd;
        Material = material;

        TrailLength = Mathf.Max(1, setup.Duration);
        Granularity = Mathf.Max(MinGranularity, setup.Resolution);
        WhiteStep = setup.WhiteFade;

        SamplingFrequency = setup.CaptureRate;

        _sampleInterval = SamplingFrequency > 0 ? 1f / SamplingFrequency : 0f;
        _sampleTimer = 0f;
        _advanceTimer = 0f;

        gameObject.layer = SaberLayer;

        if (Material != null)
        {
            try
            {
                AssetBundleLoadingTools.Utilities.ShaderRepair.FixShadersOnMaterials([Material]);
            }
            catch (Exception ex)
            {
                ModLogger.Warn($"Failed to repair trail material shader: {ex.Message}");
            }
        }

        _meshObj = new GameObject("SaberTrailMesh");
        _meshObj.layer = gameObject.layer;
        _meshObj.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        _meshFilter = _meshObj.AddComponent<MeshFilter>();
        _meshRenderer = _meshObj.AddComponent<MeshRenderer>();

        _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;
        _meshRenderer.sharedMaterial = Material;
        _meshRenderer.sortingOrder = editor ? 3 : 0;

        _meshFilter.sharedMesh = _mesh = new Mesh { bounds = HugeBounds };
        _mesh.MarkDynamic();

        _buffer = new(TrailLength);
        _meshBuilder = new();
        _meshBuilder.Init(_mesh, Granularity);

        _mesh.bounds = HugeBounds;

        _inited = true;
    }

    private void InitBuffer()
    {
        if (PointStart == null || PointEnd == null) return;

        var snap = new SnapshotRingBuffer.Snapshot { PointStart = PointStart.position, PointEnd = PointEnd.position };
        ApplyLocalSpaceOffset(ref snap);
        _lastSnapshot = snap;
        _buffer.InitFill(snap);
    }

    private void LateUpdate()
    {
        if (!_inited) return;

        if (PointStart == null || PointEnd == null) return;

        _frameNum++;
        if (!_lateInitDone && _frameNum > SkipFirstFrames)
        {
            _lateInitDone = true;
            if (_meshObj) _meshObj.SetActive(true);
            InitBuffer();
        }
        else if (!_lateInitDone)
        {
            return;
        }

        if (_sampleInterval > 0f)
        {
            _sampleTimer += Time.deltaTime;
            if (_sampleTimer >= _sampleInterval)
            {
                _sampleTimer -= _sampleInterval;
                _sampleTimer = Mathf.Min(_sampleTimer, _sampleInterval);
                CaptureSnapshot();
            }
        }
        else
        {
            CaptureSnapshot();
        }

        _buffer.WriteAtHead(_lastSnapshot);
        _buffer.RecalculateDistances();

        float trailWidth = (PointStart.position - PointEnd.position).magnitude;
        _meshBuilder.Update(
        _mesh,
        _buffer,
        GetPlayerOffset(),
        LocalSpaceTrails,
        trailWidth,
        WhiteStep,
        Color,
        HugeBounds
        );

        _advanceTimer += Time.deltaTime;
        int advances = 0;
        while (_advanceTimer >= AdvanceInterval && advances < MaxAdvancesPerFrame)
        {
            _advanceTimer -= AdvanceInterval;
            _buffer.AdvanceHead();
            _buffer.WriteAtHead(_lastSnapshot);
            advances++;
        }

        if (_advanceTimer > AdvanceInterval)
        _advanceTimer = 0f;
    }

    private void CaptureSnapshot()
    {
        _lastSnapshot = new()
        {
            PointStart = PointStart!.position,
            PointEnd = PointEnd!.position
        };
        ApplyLocalSpaceOffset(ref _lastSnapshot);
    }

    private void ApplyLocalSpaceOffset(ref SnapshotRingBuffer.Snapshot snap)
    {
        if (!LocalSpaceTrails) return;
        var offset = GetPlayerOffset();
        snap.PointStart -= offset;
        snap.PointEnd -= offset;
    }

    public void SetMaterialBlock(MaterialPropertyBlock block)
    {
        if (_meshRenderer) _meshRenderer.SetPropertyBlock(block);
    }

    public void SetLayer(int layer)
    {
        if (_meshObj) _meshObj.layer = layer;
    }
}