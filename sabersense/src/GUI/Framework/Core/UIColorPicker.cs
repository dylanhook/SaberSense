// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UIColorPicker : UIElement
{
    private const float PanelSize = 36f;

    public UIImage SwatchBorder { get; private set; }
    public UIImage SwatchFill { get; private set; }

    internal float Hue;
    internal float Sat = 1f;
    internal float Val = 1f;
    internal float Alpha = 1f;
    internal bool IsExpanded;

    private Action<Color>? _onColorChanged;
    private Action<Color>? _onCommit;
    private readonly RectTransform? _canvasRoot;

    private GameObject _backdropGO = null!;
    private GameObject _expandGO = null!;
    private RectTransform _expandRect = null!;
    private ColorPickerPanel _panel = null!;
    private readonly PopupOwnerGuard _guard;

    private static Color? _clipboard;

    private Color _initialColor = Color.white;
    private bool _hasResetColor;

    private GameObject? _ctxBackdropGO;
    private GameObject? _ctxPanelGO;

    public UIColorPicker(string name = "ColorPicker", RectTransform? canvasRoot = null) : base(name)
    {
        _canvasRoot = canvasRoot;

        SwatchBorder = new UIImage("Border")
        .SetColor(new Color32(30, 30, 30, 255))
        .SetParent(this, false);
        SwatchBorder.ImageComponent.type = Image.Type.Simple;

        SwatchBorder.RectTransform.anchorMin = new Vector2(1f, 0.15f);
        SwatchBorder.RectTransform.anchorMax = new Vector2(1f, 0.85f);
        SwatchBorder.RectTransform.pivot = new Vector2(1f, 0.5f);
        SwatchBorder.RectTransform.sizeDelta = new Vector2(4f, 0f);
        SwatchBorder.ImageComponent.raycastTarget = true;

        SwatchFill = new UIImage("Fill")
        .SetColor(Color.HSVToRGB(0, 1, 1))
        .SetParent(SwatchBorder, false);
        SwatchFill.ImageComponent.type = Image.Type.Simple;
        SwatchFill.SetAnchors(new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
        SwatchFill.ImageComponent.raycastTarget = false;

        var handler = SwatchBorder.AddComponent<PointerEventHandler>();
        handler.OnClickEvent = (eventData) =>
        {
            if (eventData.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
            ShowContextMenu();
            else
            ToggleExpand();
        };

        var hoverDetector = SwatchBorder.AddComponent<ActionKeyHoverDetector>();
        hoverDetector.Picker = this;

        _backdropGO = UIPopupHelper.CreateBackdrop("ColorBackdrop", _canvasRoot!, RectTransform, Collapse, alpha: 0.01f);

        _expandGO = new GameObject("ColorPanel");
        _expandRect = _expandGO.AddComponent<RectTransform>();
        UIPopupHelper.SetupPopupCanvas(_expandGO, _canvasRoot, UIZLayer.Popup);
        _expandGO.SetActive(false);
        _expandGO.transform.SetParent(RectTransform, false);

        var panelGO = new GameObject("PanelRenderer");
        panelGO.transform.SetParent(_expandRect, false);
        var prt = panelGO.AddComponent<RectTransform>();
        prt.anchorMin = Vector2.zero;
        prt.anchorMax = Vector2.one;
        prt.sizeDelta = Vector2.zero;

        _panel = panelGO.AddComponent<ColorPickerPanel>();
        _panel.Picker = this;
        _panel.material = UIMaterials.NoBloomMaterial;
        _panel.raycastTarget = true;

        var input = panelGO.AddComponent<ColorPickerInput>();
        input.Picker = this;
        input.Panel = _panel;

        _guard = RectTransform.gameObject.AddComponent<PopupOwnerGuard>();
        _guard.Register(_backdropGO);
        _guard.Register(_expandGO);
    }

    public UIColorPicker OnColorChanged(Action<Color> callback)
    {
        _onColorChanged = callback;
        return this;
    }

    public UIColorPicker OnCommit(Action<Color> callback)
    {
        _onCommit = callback;
        return this;
    }

    internal void NotifyCommit()
    {
        if (_onCommit is null) return;
        Color c = Color.HSVToRGB(Hue, Sat, Val);
        c.a = Alpha;
        UICallbackGuard.Invoke(_onCommit, c);
    }

    public UIColorPicker SetColor(Color color)
    {
        if (!_hasResetColor) { _initialColor = color; _hasResetColor = true; }
        Alpha = color.a;
        Color.RGBToHSV(new Color(color.r, color.g, color.b, 1f), out Hue, out Sat, out Val);
        SwatchFill.SetColor(color);
        if (_panel != null) _panel.SetVerticesDirty();
        return this;
    }

    public UIColorPicker SetResetColor(Color color)
    {
        _initialColor = color;
        _hasResetColor = true;
        return this;
    }

    internal void NotifyColorChanged()
    {
        Color c = Color.HSVToRGB(Hue, Sat, Val);
        c.a = Alpha;
        SwatchFill.SetColor(c);
        UICallbackGuard.Invoke(_onColorChanged!, c);
        if (_panel != null) _panel.SetVerticesDirty();
    }

    public Color GetColor()
    {
        Color c = Color.HSVToRGB(Hue, Sat, Val);
        c.a = Alpha;
        return c;
    }

    private void ToggleExpand()
    {
        if (IsExpanded) Collapse();
        else Expand();
    }

    private void Expand()
    {
        if (_canvasRoot == null) return;
        IsExpanded = true;

        var corners = new Vector3[4];
        RectTransform.GetWorldCorners(corners);
        Vector3 localBL = _canvasRoot.InverseTransformPoint(corners[0]);
        Vector3 localBR = _canvasRoot.InverseTransformPoint(corners[3]);

        _backdropGO.transform.SetParent(_canvasRoot, false);
        var bRect = _backdropGO.GetComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero;
        bRect.anchorMax = Vector2.one;
        bRect.sizeDelta = Vector2.zero;
        bRect.anchoredPosition = Vector2.zero;
        _backdropGO.SetActive(true);
        _backdropGO.transform.SetAsLastSibling();

        _expandGO.transform.SetParent(_canvasRoot, false);
        _expandRect.anchorMin = new Vector2(0.5f, 0.5f);
        _expandRect.anchorMax = new Vector2(0.5f, 0.5f);
        _expandRect.sizeDelta = new Vector2(PanelSize, PanelSize);

        Rect canvasRect = _canvasRoot.rect;
        float panelH = PanelSize;
        float bottomEdge = localBL.y - panelH;

        if (bottomEdge >= canvasRect.yMin)
        {
            _expandRect.pivot = new Vector2(1f, 1f);
            _expandRect.anchoredPosition = new Vector2(localBR.x, localBL.y);
        }
        else
        {
            Vector3 localTR = _canvasRoot.InverseTransformPoint(corners[2]);
            _expandRect.pivot = new Vector2(1f, 0f);
            _expandRect.anchoredPosition = new Vector2(localBR.x, localTR.y);
        }

        _expandGO.SetActive(true);
        _expandGO.transform.SetAsLastSibling();

        if (_panel != null) _panel.SetVerticesDirty();
    }

    private void Collapse()
    {
        IsExpanded = false;
        _backdropGO.SetActive(false);
        _expandGO.SetActive(false);
        _backdropGO.transform.SetParent(RectTransform, false);
        _expandGO.transform.SetParent(RectTransform, false);
    }

    internal void ShowContextMenu()
    {
        if (_canvasRoot == null) return;
        HideContextMenu();

        (_ctxBackdropGO, _ctxPanelGO) = UIContextMenu.Show(
        _canvasRoot, SwatchBorder.RectTransform, UIContextMenu.Alignment.Right, HideContextMenu,
        ("Copy", () =>
        {
            _clipboard = GetColor();
            HideContextMenu();
        }),
        ("Paste", () =>
        {
            if (_clipboard.HasValue)
            {
                SetColor(_clipboard.Value);
                NotifyColorChanged();
                NotifyCommit();
            }
            HideContextMenu();
        }),
        ("Reset", () =>
        {
            SetColor(_initialColor);
            NotifyColorChanged();
            NotifyCommit();
            HideContextMenu();
        }));

        _guard.Register(_ctxBackdropGO);
        _guard.Register(_ctxPanelGO);
    }

    private void HideContextMenu()
    {
        if (_ctxBackdropGO != null) { _guard.Unregister(_ctxBackdropGO); UnityEngine.Object.Destroy(_ctxBackdropGO); _ctxBackdropGO = null; }
        if (_ctxPanelGO != null) { _guard.Unregister(_ctxPanelGO); UnityEngine.Object.Destroy(_ctxPanelGO); _ctxPanelGO = null; }
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        Collapse();
        HideContextMenu();
        base.Dispose();
    }

    internal sealed class ActionKeyHoverDetector : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public UIColorPicker? Picker;
        private bool _hovered;

        public void OnPointerEnter(PointerEventData e) => _hovered = true;
        public void OnPointerExit(PointerEventData e) => _hovered = false;

        private void Update()
        {
            if (_hovered && Picker is not null && SaberSense.Input.ActionKeyInputBehavior.IsPressedDown)
            Picker.ShowContextMenu();
        }
    }
}