// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UISlider : UIElement
{
    private const float TrackHeight = 1f;
    private const float BorderInset = 0.2f;
    private const float DragJumpThreshold = 0.3f;
    private const float LabelWidth = 12f;
    private const float PercentMin = 0f;
    private const float PercentMax = 100f;
    private const string PercentSuffix = "%";

    private float _min = 0f;
    private float _max = 1f;
    private float _value = 0f;
    public bool Interactable
    {
        get => _interactable;
        set { _interactable = value; if (_unitySlider != null) _unitySlider.interactable = value; }
    }
    private bool _interactable = true;

    private Action<float>? _onValueChanged;
    private Action<float>? _onCommit;
    private Func<float, string>? _labelFormatter;

    private UIImage _trackBg;
    private UIImage _innerBg;
    private UIImage _fill;
    private UILabel _valueLabel;
    private Slider _unitySlider;
    private ViewportClip _viewportClip;

    private float _lastValidValue;
    private bool _isDragging;

    public RectTransform? FillRect => _fill?.RectTransform;

    public UISlider(string name = "Slider") : base(name)
    {
        var le = RectTransform.gameObject.AddComponent<LayoutElement>();
        le.minHeight = TrackHeight;
        le.preferredHeight = TrackHeight;

        _trackBg = new UIImage("Track")
        .SetColor(UITheme.Border);
        _trackBg.RectTransform.SetParent(RectTransform, false);
        _trackBg.SetAnchors(Vector2.zero, Vector2.one);
        _trackBg.ImageComponent.raycastTarget = true;

        _innerBg = new UIImage("InnerBg");
        _innerBg.RectTransform.SetParent(_trackBg.RectTransform, false);
        _innerBg.SetAnchors(Vector2.zero, Vector2.one);
        _innerBg.RectTransform.offsetMin = new Vector2(BorderInset, BorderInset);
        _innerBg.RectTransform.offsetMax = new Vector2(-BorderInset, -BorderInset);
        _innerBg.ImageComponent.raycastTarget = false;
        _innerBg.SetSprite(UIGradient.SldNormal);
        _innerBg.ImageComponent.color = Color.white;

        _fill = new UIImage("Fill");
        _fill.RectTransform.SetParent(_innerBg.RectTransform, false);
        _fill.SetAnchors(Vector2.zero, new Vector2(0, 1f));
        _fill.ImageComponent.raycastTarget = false;
        _fill.SetSprite(UIGradient.AccentVert);
        _fill.ImageComponent.color = Color.white;
        _fill.RectTransform.offsetMin = Vector2.zero;
        _fill.RectTransform.offsetMax = Vector2.zero;

        var dummyFillGO = new GameObject("DummyFill");
        var dummyFillRect = dummyFillGO.AddComponent<RectTransform>();
        dummyFillRect.SetParent(_innerBg.RectTransform, false);
        dummyFillRect.anchorMin = Vector2.zero;
        dummyFillRect.anchorMax = new Vector2(0, 1);
        dummyFillRect.offsetMin = Vector2.zero;
        dummyFillRect.offsetMax = Vector2.zero;

        var labelContainer = new GameObject("LabelContainer");
        var labelRect = labelContainer.AddComponent<RectTransform>();
        labelRect.SetParent(_trackBg.RectTransform, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        labelRect.pivot = new Vector2(0.5f, 0.5f);

        _valueLabel = new UILabel("Val", "0.0")
        .SetFontSize(UITheme.FontSmall)
        .SetColor(UITheme.TextLabel);
        _valueLabel.TextComponent.fontStyle = TMPro.FontStyles.Bold;
        _valueLabel.RectTransform.SetParent(labelRect, false);
        _valueLabel.TextComponent.raycastTarget = false;
        _valueLabel.TextComponent.enableAutoSizing = false;
        _valueLabel.TextComponent.overflowMode = TMPro.TextOverflowModes.Overflow;
        _valueLabel.TextComponent.maskable = false;

        _viewportClip = labelContainer.AddComponent<ViewportClip>();
        _viewportClip.Init(_valueLabel.TextComponent);

        _unitySlider = _trackBg.GameObject.AddComponent<VRSafeSlider>();
        ((VRSafeSlider)_unitySlider).SetOwner(this);
        _unitySlider.targetGraphic = _innerBg.ImageComponent;
        _unitySlider.fillRect = dummyFillRect;
        _unitySlider.handleRect = null;
        _unitySlider.direction = Slider.Direction.LeftToRight;
        _unitySlider.minValue = _min;
        _unitySlider.maxValue = _max;
        _unitySlider.wholeNumbers = false;
        _unitySlider.value = _value;
        _unitySlider.interactable = _interactable;
        _unitySlider.navigation = new() { mode = Navigation.Mode.None };

        _unitySlider.transition = Selectable.Transition.SpriteSwap;
        var ss = new SpriteState();
        ss.highlightedSprite = UIGradient.SldHover;
        ss.pressedSprite = UIGradient.SldHover;
        ss.selectedSprite = UIGradient.SldNormal;
        _unitySlider.spriteState = ss;

        _unitySlider.onValueChanged.AddListener(OnUnitySliderChanged);
    }

    private void OnUnitySliderChanged(float val)
    {
        if (!_interactable) return;

        if (float.IsNaN(val) || float.IsInfinity(val))
        {
            _unitySlider.SetValueWithoutNotify(_lastValidValue);
            return;
        }

        if (_isDragging)
        {
            float range = _max - _min;
            if (range > 0.001f && Mathf.Abs(val - _lastValidValue) > range * DragJumpThreshold)
            {
                _unitySlider.SetValueWithoutNotify(_lastValidValue);
                return;
            }
        }

        _lastValidValue = val;
        _value = val;
        UpdateFill();
        UpdateLabel();
        UICallbackGuard.Invoke(_onValueChanged!, _value);
    }

    public UISlider SetRange(float min, float max)
    {
        _min = min;
        _max = max;
        if (_unitySlider != null)
        {
            _unitySlider.minValue = _min;
            _unitySlider.maxValue = _max;
        }
        UpdateFill();
        UpdateLabel();
        return this;
    }

    public UISlider SetValue(float val)
    {
        _value = Mathf.Clamp(val, _min, _max);
        _lastValidValue = _value;
        if (_unitySlider)
        _unitySlider.SetValueWithoutNotify(_value);
        UpdateFill();
        UpdateLabel();
        return this;
    }

    public UISlider OnValueChanged(Action<float> callback)
    {
        _onValueChanged = callback;
        return this;
    }

    public UISlider OnCommit(Action<float> callback)
    {
        _onCommit = callback;
        return this;
    }

    public UISlider SetLabelFormatter(Func<float, string> formatter)
    {
        _labelFormatter = formatter;
        UpdateLabel();
        return this;
    }

    private void UpdateFill()
    {
        if (_fill is null || !_fill.RectTransform) return;
        float p = Mathf.Clamp01(Mathf.InverseLerp(_min, _max, _value));
        _fill.RectTransform.anchorMin = new Vector2(0, 0);
        _fill.RectTransform.anchorMax = new Vector2(p, 1);
        _fill.RectTransform.offsetMin = Vector2.zero;
        _fill.RectTransform.offsetMax = Vector2.zero;
    }

    private void UpdateLabel()
    {
        if (_fill is null || !_fill.RectTransform || _valueLabel is null || !_valueLabel.RectTransform) return;

        float p = Mathf.Clamp01(Mathf.InverseLerp(_min, _max, _value));

        string vStr;
        if (_labelFormatter is not null)
        {
            vStr = _labelFormatter(_value);
        }
        else
        {
            vStr = (Mathf.Abs(_max - _min) <= 10f && Mathf.Abs(_value - Mathf.Round(_value)) > 0.001f)
            ? _value.ToString("F2")
            : Mathf.RoundToInt(_value).ToString();

            if (_min == PercentMin && _max == PercentMax)
            {
                vStr += PercentSuffix;
            }
        }

        _valueLabel.SetText(vStr);

        float labelWidth = LabelWidth;

        _valueLabel.RectTransform.pivot = new Vector2(0.5f, 0.5f);
        _valueLabel.RectTransform.anchorMin = new Vector2(p, 0);
        _valueLabel.RectTransform.anchorMax = new Vector2(p, 1);
        _valueLabel.RectTransform.anchoredPosition = Vector2.zero;
        _valueLabel.RectTransform.sizeDelta = new Vector2(labelWidth, 0);
    }

    private class VRSafeSlider : Slider
    {
        private UISlider? _owner;
        private RectTransform? _clipRect;

        public void SetOwner(UISlider owner) => _owner = owner;

        protected override void Start()
        {
            base.Start();
            var mask = GetComponentInParent<Mask>();
            if (mask != null) _clipRect = mask.rectTransform;
        }

        private bool IsOutsideClip(PointerEventData eventData)
        {
            if (_clipRect == null) return false;
            var cam = eventData.pointerCurrentRaycast.module != null
            ? eventData.pointerCurrentRaycast.module.eventCamera
            : eventData.pressEventCamera;
            return !RectTransformUtility.RectangleContainsScreenPoint(_clipRect, eventData.position, cam);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (IsOutsideClip(eventData)) return;
            if (_owner is not null) _owner._isDragging = false;
            base.OnPointerDown(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (_owner is not null) _owner._isDragging = true;

            if (eventData.pointerCurrentRaycast.gameObject == null) return;

            base.OnDrag(eventData);
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (_owner is not null) _owner._isDragging = false;
            base.OnPointerUp(eventData);
            if (_owner?._onCommit is not null)
            UICallbackGuard.Invoke(_owner._onCommit, _owner._value);
        }
    }

    public void ForceClipEvaluation() => _viewportClip?.ForceClip();

    private class ViewportClip : MonoBehaviour
    {
        private TMPro.TextMeshProUGUI? _text;
        private RectTransform? _maskRect;
        private Vector3[] _corners = new Vector3[4];
        private bool _skipFrame;

        public void Init(TMPro.TextMeshProUGUI text) => _text = text;

        private void Start()
        {
            FindMask();
        }

        private void OnEnable()
        {
            _skipFrame = true;
            if (_text != null) _text.enabled = false;
        }

        public void ForceClip()
        {
            if (_maskRect == null) FindMask();
            _skipFrame = false;
            EvaluateBounds();
        }

        private void FindMask()
        {
            Transform t = transform.parent;
            while (t != null)
            {
                if (t.GetComponent<Mask>() != null)
                {
                    _maskRect = t as RectTransform;
                    break;
                }
                t = t.parent;
            }
        }

        private void LateUpdate()
        {
            if (_text == null || _maskRect == null) return;

            if (_skipFrame) { _skipFrame = false; return; }

            EvaluateBounds();
        }

        private void EvaluateBounds()
        {
            if (_text == null || _maskRect == null) return;

            _maskRect.GetWorldCorners(_corners);
            float maskBottom = _corners[0].y;
            float maskTop = _corners[2].y;

            float myY = transform.position.y;
            _text.enabled = myY >= maskBottom && myY <= maskTop;
        }
    }
}