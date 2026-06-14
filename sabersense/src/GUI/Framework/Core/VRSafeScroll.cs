// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

internal sealed class VRSafeScrollRect : ScrollRect
{
    public override void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerCurrentRaycast.gameObject == null) return;
        if (content == null) { base.OnDrag(eventData); return; }

        Vector2 savedPos = content.anchoredPosition;
        base.OnDrag(eventData);

        var pos = content.anchoredPosition;
        bool corrupt = float.IsNaN(pos.x) || float.IsNaN(pos.y)
        || float.IsInfinity(pos.x) || float.IsInfinity(pos.y)
        || (pos - savedPos).sqrMagnitude > 10000f;

        if (corrupt)
        content.anchoredPosition = savedPos;
    }
}

internal sealed class VRSafeScrollbar : Scrollbar
{
    private RectTransform? _visibleHandle;
    private Image? _visibleHandleImg;
    private float _lastValidValue;
    private HapticFeedbackManager? _hapticManager;
    private bool _hapticWasEnabled;

    private static readonly Color32 HandleNormal = UITheme.ScrollHandle;
    private static readonly Color32 HandleHovered = new(80, 80, 80, 255);

    public void SetVisibleHandle(RectTransform handle)
    {
        _visibleHandle = handle;
        _visibleHandleImg = handle.GetComponent<Image>();
        _hapticManager = FindObjectOfType<HapticFeedbackManager>();
        onValueChanged.AddListener(OnScrollbarValueChanged);
    }

    private void OnScrollbarValueChanged(float val)
    {
        if (float.IsNaN(val) || float.IsInfinity(val))
        {
            SetValueWithoutNotify(_lastValidValue);
            return;
        }
        _lastValidValue = val;
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        if (_visibleHandleImg != null) _visibleHandleImg.color = HandleHovered;
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        if (_visibleHandleImg != null) _visibleHandleImg.color = HandleNormal;
    }

    public override void OnPointerDown(PointerEventData eventData)
    {
        var trackRect = (RectTransform)transform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
        trackRect, eventData.position, eventData.pressEventCamera, out var localPoint))
        {
            float trackHeight = trackRect.rect.height;
            if (trackHeight > 0)
            {
                float normalized = Mathf.Clamp01((localPoint.y - trackRect.rect.yMin) / trackHeight);
                value = normalized;
            }
        }

        if (_hapticManager != null)
        {
            _hapticWasEnabled = _hapticManager.hapticFeedbackEnabled;
            _hapticManager.hapticFeedbackEnabled = false;
        }
    }

    public override void OnPointerUp(PointerEventData eventData)
    {
        base.OnPointerUp(eventData);
        if (_hapticManager != null)
        _hapticManager.hapticFeedbackEnabled = _hapticWasEnabled;
    }

    public override void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerCurrentRaycast.gameObject == null) return;
        base.OnDrag(eventData);
    }

    private void LateUpdate()
    {
        UpdateVisibleHandle();
    }

    private void UpdateVisibleHandle()
    {
        if (_visibleHandle == null) return;

        float v = _lastValidValue;
        float s = size;

        if (float.IsNaN(v) || float.IsInfinity(v)) v = 0f;
        if (float.IsNaN(s) || float.IsInfinity(s)) s = 0.1f;

        v = Mathf.Clamp01(v);
        float handleSize = Mathf.Clamp(s, 0.05f, 1f);

        float handleBottom = v * (1f - handleSize);
        float handleTop = handleBottom + handleSize;
        _visibleHandle.anchorMin = new Vector2(_visibleHandle.anchorMin.x, handleBottom);
        _visibleHandle.anchorMax = new Vector2(_visibleHandle.anchorMax.x, handleTop);
    }
}

[DefaultExecutionOrder(1000)]
internal sealed class ContentGuard : MonoBehaviour
{
    private RectTransform? _content;

    public void Init(ScrollRect scrollRect, RectTransform viewport, RectTransform content)
    {
        _content = content;
    }

    private void LateUpdate()
    {
        if (_content == null) return;

        var pos = _content.anchoredPosition;
        bool corrupt = float.IsNaN(pos.x) || float.IsNaN(pos.y)
        || float.IsInfinity(pos.x) || float.IsInfinity(pos.y);

        if (corrupt)
        _content.anchoredPosition = new Vector2(
        float.IsNaN(pos.x) || float.IsInfinity(pos.x) ? 0f : pos.x,
        float.IsNaN(pos.y) || float.IsInfinity(pos.y) ? 0f : pos.y);
    }
}

[DefaultExecutionOrder(998)]
internal sealed class CanvasAttachGuard : MonoBehaviour
{
    private Graphic[] _graphics = null!;

    private void Awake()
    {
        _graphics = GetComponentsInChildren<Graphic>(true);
        foreach (var g in _graphics)
        g.enabled = false;
    }

    private void LateUpdate()
    {
        if (GetComponentInParent<Canvas>() != null)
        {
            foreach (var g in _graphics)
            if (g != null) g.enabled = true;
            Destroy(this);
        }
    }
}