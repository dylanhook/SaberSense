// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UISaberPreview : UIElement
{
    private const int SaberLayer = 12;
    private const float PreviewWidthBoost = 3f;
    private const float DefaultCameraY = 0.35f;
    private const float DefaultCameraZ = -1.2f;
    private const float DefaultFieldOfView = 65f;
    private const float NearClip = 0.01f;
    private const float FarClip = 10f;
    private const float CameraDepth = -10f;
    private const float FrameBorder = 0.166f;
    private const float MinRenderSize = 1024;
    private const int AntiAliasingLevel = 4;
    private const int DepthBits = 16;

    private const float ResumeDelay = 0.5f;
    private const float DragSensitivity = 6.5f;
    private const float DefaultRotationSpeed = 25f;
    private const float PitchStraightenRate = 8f;
    private const float IdentityPullRate = 10f;
    private const float BoundsMargin = 1.05f;
    private const float MinFrameHeight = 0.5f;
    private const float MinFrameDistance = 0.5f;
    private const float MaxFrameDistance = 6f;
    private const float MaxReframeDistance = 8f;
    private const float MaxBoundsReasonable = 5f;

    private static readonly Vector3 ContainerPosition = new(0, -1000, 0);
    private static readonly Color BackgroundColor = new(0.06f, 0.06f, 0.06f, 0f);
    private static readonly Quaternion RestRotation = Quaternion.Euler(0, -35, 0);

    private GameObject? _containerGO;
    private Transform _saberPivot = null!;
    private Transform _saberHost = null!;
    private Camera _previewCamera = null!;
    private RenderTexture? _renderTexture;
    private RawImage _rawImage = null!;
    private PreviewClickDrag _clickDrag = null!;

    private float _rotationSpeed = DefaultRotationSpeed;
    private bool _rotationEnabled = true;
    private bool _displayTrails = true;
    private Quaternion _currentRotation = Quaternion.identity;
    private float _resumeTimer;
    private bool _wasDragging;

    public Action? OnDragStarted;

    public Action? OnDragEnded;

    public bool IsDragging => _wasDragging;

    private float _referenceCenterY;
    private float _referenceHeight;
    private float _referenceLength = 1f;

    public UISaberPreview(string name = "SaberPreview") : base(name)
    {
        _containerGO = new GameObject("SS_PreviewContainer");
        _containerGO.transform.position = ContainerPosition;

        var pivotGO = new GameObject("SaberPivot");
        _saberPivot = pivotGO.transform;
        _saberPivot.SetParent(_containerGO.transform, false);
        _saberPivot.localPosition = Vector3.zero;
        _saberPivot.localEulerAngles = Vector3.zero;

        _saberHost = new GameObject("SaberHost").transform;
        _saberHost.SetParent(_saberPivot, false);
        _saberHost.localPosition = Vector3.zero;
        _saberHost.localEulerAngles = new Vector3(-90, 0, 0);

        var camGO = new GameObject("PreviewCamera");
        camGO.transform.SetParent(_containerGO.transform, false);
        camGO.transform.localPosition = new Vector3(0, DefaultCameraY, DefaultCameraZ);
        camGO.transform.localEulerAngles = Vector3.zero;

        _previewCamera = camGO.AddComponent<Camera>();
        _previewCamera.cullingMask = 1 << SaberLayer;
        _previewCamera.clearFlags = CameraClearFlags.SolidColor;
        _previewCamera.backgroundColor = BackgroundColor;
        _previewCamera.nearClipPlane = NearClip;
        _previewCamera.farClipPlane = FarClip;
        _previewCamera.fieldOfView = DefaultFieldOfView;
        _previewCamera.allowHDR = true;
        _previewCamera.depth = CameraDepth;
        _previewCamera.enabled = true;

        camGO.AddComponent<PreviewBloom>();

        int renderSize = Mathf.Max(Screen.height * 2, (int)MinRenderSize);
        _renderTexture = new RenderTexture(renderSize, renderSize, DepthBits, RenderTextureFormat.ARGBHalf);
        _renderTexture.antiAliasing = AntiAliasingLevel;
        _renderTexture.Create();
        _previewCamera.targetTexture = _renderTexture;

        var bgImg = GameObject.AddComponent<Image>();
        bgImg.material = UIMaterials.NoBloomMaterial;
        bgImg.color = UITheme.Border;
        bgImg.raycastTarget = false;

        var innerGO = new GameObject("PreviewImage");
        innerGO.transform.SetParent(RectTransform, false);
        var innerRect = innerGO.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(FrameBorder, FrameBorder);
        innerRect.offsetMax = new Vector2(-FrameBorder, -FrameBorder);

        _rawImage = innerGO.AddComponent<RawImage>();
        _rawImage.material = UIMaterials.NoBloomMaterial;
        _rawImage.texture = _renderTexture;
        _rawImage.raycastTarget = true;

        _clickDrag = innerGO.AddComponent<PreviewClickDrag>();
    }

    public void SetSaber(SaberSense.Rendering.LiveSaber LiveSaber)
    {
        if (LiveSaber?.GameObject == null) return;

        for (int i = _saberHost.childCount - 1; i >= 0; i--)
        _saberHost.GetChild(i).SetParent(null, false);

        _saberHost.localPosition = Vector3.zero;
        _saberHost.localScale = new Vector3(PreviewWidthBoost, PreviewWidthBoost, 1f);

        LiveSaber.CachedTransform.SetParent(_saberHost, false);
        LiveSaber.CachedTransform.localPosition = Vector3.zero;
        LiveSaber.CachedTransform.localRotation = Quaternion.identity;

        if (!_rotationEnabled)
        {
            _currentRotation = RestRotation;
            _saberPivot.localRotation = RestRotation;
        }
        _resumeTimer = 0f;

        var saberScale = LiveSaber.CachedTransform.localScale;

        var renderers = LiveSaber.GameObject.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        r.gameObject.layer = SaberLayer;

        Bounds bounds = default;
        bool init = false;
        foreach (var r in renderers)
        {
            var b = r.bounds;
            if (b.size.sqrMagnitude < 0.0001f) continue;
            if (!init) { bounds = b; init = true; }
            else bounds.Encapsulate(b);
        }

        if (!init || bounds.size.y > MaxBoundsReasonable || bounds.size.x > MaxBoundsReasonable)
        {
            _referenceCenterY = DefaultCameraY;
            _referenceHeight = 1f;
            _referenceLength = saberScale.z;
            _previewCamera.transform.localPosition = new Vector3(0, DefaultCameraY, DefaultCameraZ);
            ApplyTrailVisibility();
            return;
        }

        Vector3 containerPos = _containerGO!.transform.position;
        float centerY = bounds.center.y - containerPos.y;
        float height = Mathf.Max(bounds.size.y, bounds.size.x) * BoundsMargin;
        height = Mathf.Max(height, MinFrameHeight);

        _referenceCenterY = centerY;
        _referenceHeight = height;
        _referenceLength = saberScale.z;

        float halfFov = _previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float dist = Mathf.Clamp((height * 0.5f) / Mathf.Tan(halfFov), MinFrameDistance, MaxFrameDistance);

        _previewCamera.transform.localPosition = new Vector3(0, centerY, -dist);
        _previewCamera.transform.localEulerAngles = Vector3.zero;

        ApplyTrailVisibility();
    }

    public void RefreshFraming(float saberLength = -1f, float saberOffset = 0f)
    {
        if (_saberHost == null || _saberHost.childCount == 0) return;

        var child = _saberHost.GetChild(0);

        float currentLength = saberLength > 0 ? saberLength : (child != null ? child.localScale.z : 1f);
        float ratio = (_referenceLength > 0.001f) ? currentLength / _referenceLength : 1f;

        float baseShift = saberOffset * currentLength;
        float centerY = (_referenceCenterY * ratio) + baseShift;

        float scaledHeight = Mathf.Max(_referenceHeight * ratio, MinFrameHeight);

        float height = scaledHeight + (Mathf.Abs(saberOffset) * currentLength * 0.5f);

        float halfFov = _previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;

        float dist = Mathf.Clamp((height * 0.5f) / Mathf.Tan(halfFov), MinFrameDistance, MaxReframeDistance);

        _previewCamera.transform.localPosition = new Vector3(0, centerY, -dist);
        _previewCamera.transform.localEulerAngles = Vector3.zero;
    }

    public void Tick()
    {
        if (_saberPivot == null || _saberHost == null || _saberHost.childCount == 0) return;
        float dt = Time.deltaTime;
        if (dt <= 0) return;

        bool isDragging = _clickDrag != null && _clickDrag.Held;

        if (isDragging && !_wasDragging)
        {
            OnDragStarted?.Invoke();
        }
        else if (!isDragging && _wasDragging)
        {
            OnDragEnded?.Invoke();
        }
        _wasDragging = isDragging;

        if (isDragging)
        {
            Vector2 delta = _clickDrag!.ConsumeDelta();
            if (delta.sqrMagnitude > 0.001f)
            {
                Quaternion yaw = Quaternion.AngleAxis(-delta.x * DragSensitivity, Vector3.up);
                Quaternion pitch = Quaternion.AngleAxis(delta.y * DragSensitivity, Vector3.right);
                _currentRotation = yaw * pitch * _currentRotation;
            }
        }
        else if (_clickDrag != null && _clickDrag.JustReleased)
        {
            _clickDrag.ClearRelease();
            _resumeTimer = ResumeDelay;
        }
        else
        {
            if (_resumeTimer > 0)
            {
                _resumeTimer -= dt;
            }
            else
            {
                if (_rotationEnabled && Mathf.Abs(_rotationSpeed) > 0.01f)
                {
                    _currentRotation = Quaternion.AngleAxis(_rotationSpeed * dt, Vector3.up) * _currentRotation;

                    _currentRotation = Quaternion.Slerp(_currentRotation,
                    ExtractYaw(_currentRotation), dt * PitchStraightenRate);
                }
                else
                {
                    _currentRotation = Quaternion.Slerp(_currentRotation, RestRotation, dt * IdentityPullRate);
                }
            }
        }

        _saberPivot.localRotation = _currentRotation;
    }

    public void SetRotation(bool enabled, float speedPercent)
    {
        _rotationEnabled = enabled;
        _rotationSpeed = (speedPercent / 100f) * 120f;
    }

    public void SetBloom(bool val)
    {
        var bloom = _previewCamera?.GetComponent<PreviewBloom>();
        if (bloom != null) bloom.SetBloom(val);
    }

    public void SetDisplayTrails(bool val)
    {
        _displayTrails = val;
        ApplyTrailVisibility();
    }

    private void ApplyTrailVisibility()
    {
        if (_saberHost == null) return;
        foreach (var trail in _saberHost.GetComponentsInChildren<SaberSense.Rendering.SaberTrail>(true))
        trail.enabled = _displayTrails;
    }

    public Sprite? CaptureSnapshot(int size = 128)
    {
        if (_previewCamera == null) return null;

        var rt = new RenderTexture(size, size, DepthBits, RenderTextureFormat.ARGB32);
        rt.antiAliasing = AntiAliasingLevel;
        rt.Create();

        var originalTarget = _previewCamera.targetTexture;
        _previewCamera.targetTexture = rt;
        _previewCamera.Render();
        _previewCamera.targetTexture = originalTarget;

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        rt.Release();
        UnityEngine.Object.Destroy(rt);

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Quaternion ExtractYaw(Quaternion q)
    {
        float len = Mathf.Sqrt(q.y * q.y + q.w * q.w);
        if (len < 0.0001f) return Quaternion.identity;
        return new Quaternion(0f, q.y / len, 0f, q.w / len);
    }

    public override void Dispose()
    {
        if (IsDisposed) return;

        if (_renderTexture != null)
        {
            _previewCamera.targetTexture = null;
            _renderTexture.Release();
            UnityEngine.Object.Destroy(_renderTexture);
            _renderTexture = null;
        }
        if (_containerGO != null)
        {
            UnityEngine.Object.Destroy(_containerGO);
            _containerGO = null;
        }
        base.Dispose();
    }
}