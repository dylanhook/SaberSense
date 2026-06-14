// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using CameraUtils.Core;
using SaberSense.Core.Logging;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SaberSense.Rendering;

[DefaultExecutionOrder(10000)]
internal sealed class SaberMotionBlur : MonoBehaviour, ISaberEffect
{
    public float Strength { get; set; } = 50f;

    private const int SweepSubdivisions = 60;
    private const int SkipFirstFrames = 4;
    private const float MinAngVelDeg = 2f;
    private const float FullAngVelDeg = 30f;

    private const float MaxAlphaScale = 0.35f;
    private const float ArcFalloffExponent = 2.5f;
    private const float BottomVertexAlpha = 0.4f;

    private const float MinAccelFactor = 0.15f;
    private const float MaxArcFrames = 4f;
    private const int LeadingEdgeRamp = 6;

    private const float CoherenceDotBreakThreshold = -0.3f;
    private const float SqrMagnitudeEpsilon = 0.0001f;

    private const int ColorCaptureFrameDelay = 60;

    private static readonly int StripRes = MotionBlurColorSampler.StripResolution;
    private static readonly int RowsPerSubdiv = StripRes * 2 + 2;

    private const float EdgeExtFraction = 0.06f;

    private const int SaberLayer = 12;
    private const int RenderQueueAdditive = 3000;
    private const int AngVelSampleCount = 3;
    private const float DefaultProfileRadius = 0.01f;

    private static readonly Bounds HugeBounds = new(Vector3.zero, new Vector3(100000f, 100000f, 100000f));

    private int _frameNum;
    private int _layer = SaberLayer;

    private Vector3 _botLocal;
    private float _edgeExt;

    private readonly MotionBlurHistory _history = new();
    private Renderer[] _saberRenderers = [];
    private Transform _saberRoot = null!;
    private (float minZ, float maxZ)? _parsedBounds;
    private float _minZ, _maxZ;
    private float[]? _profileRadius;
    private Color[]? _colorStrip;
    private bool _colorDirty = true;
    private bool _boundsDirty = true;
    private Matrix4x4 _rootInverse;

    private readonly Dictionary<int, Color[]?> _texStripCache = new();

    private Mesh _sweepMesh = null!;
    private GameObject _sweepObj = null!;
    private Material _sweepMaterial = null!;
    private readonly List<Vector3> _sv = [];
    private readonly List<Color> _sc = [];
    private readonly List<int> _st = [];

    public void Init(GameObject saberGO, (float minZ, float maxZ)? parsedBounds = null, Shader? sweepShader = null)
    {
        var root = saberGO.transform;
        _saberRoot = root;
        _parsedBounds = parsedBounds;

        var allRenderers = saberGO.GetComponentsInChildren<Renderer>(true);
        var meshRenderers = new List<Renderer>();
        foreach (var r in allRenderers)
        {
            if (r is MeshRenderer || r is SkinnedMeshRenderer)
            meshRenderers.Add(r);
        }
        _saberRenderers = meshRenderers.ToArray();

        RecalculateBounds();

        ModLogger.ForSource("MotionBlur").Info($"Bounds: minZ={_minZ:F3} maxZ={_maxZ:F3} range={(_maxZ - _minZ):F3} renderers={_saberRenderers.Length}");

        _sweepMaterial = new Material(sweepShader ?? Shader.Find("Hidden/Internal-Colored"));
        _sweepMaterial.hideFlags = HideFlags.HideAndDontSave;
        _sweepMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        _sweepMaterial.SetInt("_DstBlend", (int)BlendMode.One);
        _sweepMaterial.SetInt("_Cull", (int)CullMode.Off);
        _sweepMaterial.SetInt("_ZWrite", 0);
        _sweepMaterial.renderQueue = RenderQueueAdditive;

        _sweepObj = new GameObject("MotionBlurFill");
        _sweepObj.layer = _layer;
        var mfComp = _sweepObj.AddComponent<MeshFilter>();
        var mr2 = _sweepObj.AddComponent<MeshRenderer>();
        mr2.shadowCastingMode = ShadowCastingMode.Off;
        mr2.receiveShadows = false;
        mr2.sharedMaterial = _sweepMaterial;

        _sweepMesh = new Mesh { bounds = HugeBounds };
        _sweepMesh.MarkDynamic();
        mfComp.sharedMesh = _sweepMesh;
    }

    public void SetLayer(int layer)
    {
        _layer = layer;
        if (_sweepObj) _sweepObj.layer = layer;
    }

    private void OnEnable() { if (_sweepObj) _sweepObj.SetActive(true); }
    private void OnDisable() { if (_sweepObj) _sweepObj.SetActive(false); }

    private void OnDestroy()
    {
        if (_sweepMesh) Destroy(_sweepMesh);
        if (_sweepObj) Destroy(_sweepObj);
        if (_sweepMaterial) Destroy(_sweepMaterial);
    }

    private void LateUpdate()
    {
        if (Strength <= 0f) return;
        _frameNum++;
        if (_frameNum <= SkipFirstFrames)
        {
            if (_frameNum == SkipFirstFrames)
            {
                _history.Fill(transform.position, transform.rotation, transform.lossyScale);
                if (_sweepObj) _sweepObj.SetActive(true);
            }
            return;
        }

        if (_colorDirty && _frameNum >= ColorCaptureFrameDelay)
        {
            if (_boundsDirty)
            {
                RecalculateBounds();
                _boundsDirty = false;
            }
            else if (_saberRoot != null)
            {
                _rootInverse = _saberRoot.worldToLocalMatrix;
            }
            _colorStrip = MotionBlurColorSampler.Sample(
            _saberRenderers, _rootInverse, _minZ, _maxZ, _texStripCache);
            _colorDirty = false;
        }

        _history.Record(transform.position, transform.rotation, transform.lossyScale);
        if (_history.Count is < 2) return;

        float angVel = 0f;
        int samples = Mathf.Min(_history.Count - 1, AngVelSampleCount);
        for (int i = 0; i < samples; i++)
        angVel += Quaternion.Angle(_history.Rot(i), _history.Rot(i + 1));
        angVel /= samples;

        if (angVel < MinAngVelDeg)
        {
            _sweepMesh.Clear();
            return;
        }

        float intensity = Mathf.Clamp01(
        (angVel - MinAngVelDeg) / (FullAngVelDeg - MinAngVelDeg));

        UpdateSweep(intensity);
    }

    private void RecalculateBounds()
    {
        var bounds = MotionBlurBounds.Compute(_saberRenderers, _saberRoot, _parsedBounds);
        _minZ = bounds.minZ;
        _maxZ = bounds.maxZ;

        _profileRadius = MotionBlurBounds.BuildProfile(
        _saberRenderers, _saberRoot, _minZ, _maxZ, StripRes);

        if (_saberRoot != null)
        _rootInverse = _saberRoot.worldToLocalMatrix;

        _botLocal = new Vector3(0, 0, _minZ);
        _edgeExt = (_maxZ - _minZ) * EdgeExtFraction;
    }

    public void NotifyTransformChanged()
    {
        RecalculateBounds();

        _colorDirty = true;
    }

    public void RefreshColors() => _colorDirty = true;

    public void ProcessSaber(LiveSaber saber) { }

    public void OnColorChanged(Color color) => RefreshColors();

    public void OnScaleChanged() => NotifyTransformChanged();

    public void OnVisibilityLayerChanged(VisibilityLayer layer) => SetLayer((int)layer);

    public void OnTeardown() => Destroy(this);

    private void UpdateSweep(float intensity)
    {
        _sv.Clear(); _sc.Clear(); _st.Clear();

        float strengthNorm = Mathf.Clamp01(Strength / 100f);
        float maxAlpha = strengthNorm * intensity * MaxAlphaScale;
        bool hasStrip = _colorStrip is not null && _colorStrip.Length == StripRes;
        bool hasProfile = _profileRadius is not null && _profileRadius.Length == StripRes;

        ComputeArcParameters(intensity, out float arcFrames);

        Vector3 prevTipWorld = Vector3.zero;
        bool hasPrevTip = false;

        for (int i = 0; i < SweepSubdivisions; i++)
        {
            float t = (float)i / (SweepSubdivisions - 1);
            var m = _history.FrameMatrix(t * arcFrames);

            Vector3 tipWorld = m.MultiplyPoint3x4(new Vector3(0, 0, _maxZ));
            if (hasPrevTip && i > LeadingEdgeRamp && CheckCoherenceBreak(tipWorld, prevTipWorld, m))
            break;
            prevTipWorld = tipWorld;
            hasPrevTip = true;

            float arcA = ComputeArcAlpha(t, i);
            AddSubdivisionVertices(m, maxAlpha, arcA, hasStrip, hasProfile);

            if (i < SweepSubdivisions - 1)
            AddSubdivisionTriangles(i);
        }

        _sweepMesh.Clear();
        _sweepMesh.SetVertices(_sv);
        _sweepMesh.SetColors(_sc);
        _sweepMesh.SetTriangles(_st, 0, calculateBounds: false);

        _sweepMesh.bounds = HugeBounds;
    }

    private void ComputeArcParameters(float intensity, out float arcFrames)
    {
        Vector3 tipDir0 = _history.FrameMatrix(0f).MultiplyPoint3x4(new Vector3(0, 0, _maxZ));
        Vector3 tipDir1 = _history.FrameMatrix(1f).MultiplyPoint3x4(new Vector3(0, 0, _maxZ));
        Vector3 tipDir2 = _history.FrameMatrix(2f).MultiplyPoint3x4(new Vector3(0, 0, _maxZ));
        Vector3 moveRecent = tipDir0 - tipDir1;
        Vector3 moveOlder = tipDir1 - tipDir2;
        float coherenceDot = (moveRecent.sqrMagnitude > SqrMagnitudeEpsilon && moveOlder.sqrMagnitude > SqrMagnitudeEpsilon)
        ? Vector3.Dot(moveRecent.normalized, moveOlder.normalized)
        : 1f;

        float accelFactor = Mathf.Max(Mathf.Clamp01((coherenceDot + 1f) * 0.5f), MinAccelFactor);
        arcFrames = Mathf.Min(intensity * accelFactor * (_history.Count - 1), MaxArcFrames);
    }

    private static float ComputeArcAlpha(float t, int subdivIdx)
    {
        if (subdivIdx == 0) return 0f;
        float arcA = Mathf.Pow(1f - t, ArcFalloffExponent);
        if (subdivIdx < LeadingEdgeRamp) arcA *= (float)subdivIdx / LeadingEdgeRamp;
        return arcA;
    }

    private bool CheckCoherenceBreak(Vector3 tipWorld, Vector3 prevTipWorld, Matrix4x4 m)
    {
        Vector3 sweepDir = tipWorld - prevTipWorld;
        Vector3 centerWorld = m.MultiplyPoint3x4(new Vector3(0, 0, (_minZ + _maxZ) * 0.5f));
        Vector3 prevCenterWorld = _sv.Count >= RowsPerSubdiv
        ? _sv[_sv.Count - RowsPerSubdiv + (RowsPerSubdiv / 2)]
        : centerWorld;
        Vector3 centerMove = centerWorld - prevCenterWorld;

        if (sweepDir.sqrMagnitude > SqrMagnitudeEpsilon && centerMove.sqrMagnitude > SqrMagnitudeEpsilon)
        return Vector3.Dot(sweepDir.normalized, centerMove.normalized) < CoherenceDotBreakThreshold;

        return false;
    }

    private void AddSubdivisionVertices(Matrix4x4 m, float maxAlpha, float arcA, bool hasStrip, bool hasProfile)
    {
        for (int j = 0; j < RowsPerSubdiv; j++)
        {
            var (localPos, vertAlpha, vertColor) = GetSubdivisionVertex(j, hasStrip, hasProfile);
            _sv.Add(m.MultiplyPoint3x4(localPos));
            _sc.Add(new Color(vertColor.r, vertColor.g, vertColor.b, maxAlpha * arcA * vertAlpha));
        }
    }

    private (Vector3 localPos, float vertAlpha, Color vertColor) GetSubdivisionVertex(int j, bool hasStrip, bool hasProfile)
    {
        if (j == 0)
        return (new Vector3(0, 0, _maxZ + _edgeExt), 0f,
        hasStrip ? _colorStrip![StripRes - 1] : Color.black);

        if (j <= StripRes)
        {
            int sliceIdx = StripRes - j;
            float z = Mathf.Lerp(_minZ, _maxZ, (float)sliceIdx / (StripRes - 1));
            float r = hasProfile ? _profileRadius![sliceIdx] : DefaultProfileRadius;
            return (new Vector3(-r, 0, z), 1f,
            hasStrip ? _colorStrip![sliceIdx] : Color.black);
        }

        if (j == StripRes + 1)
        return (_botLocal, BottomVertexAlpha,
        hasStrip ? _colorStrip![0] : Color.black);

        {
            int sliceIdx = j - (StripRes + 2);
            float z = Mathf.Lerp(_minZ, _maxZ, (float)sliceIdx / (StripRes - 1));
            float r = hasProfile ? _profileRadius![sliceIdx] : DefaultProfileRadius;
            return (new Vector3(r, 0, z), 1f,
            hasStrip ? _colorStrip![sliceIdx] : Color.black);
        }
    }

    private void AddSubdivisionTriangles(int i)
    {
        int baseIdx = i * RowsPerSubdiv;
        int nextIdx = (i + 1) * RowsPerSubdiv;
        for (int j = 0; j < RowsPerSubdiv - 1; j++)
        {
            int bl = baseIdx + j, nl = nextIdx + j;
            _st.Add(bl); _st.Add(bl + 1); _st.Add(nl);
            _st.Add(nl); _st.Add(bl + 1); _st.Add(nl + 1);
        }
    }
}