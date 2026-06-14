// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.EventSystems;

namespace SaberSense.GUI.Framework.Core;

internal sealed class ColorPickerInput : MonoBehaviour,
IPointerDownHandler, IPointerUpHandler, IInitializePotentialDragHandler,
IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public UIColorPicker? Picker;
    public ColorPickerPanel? Panel;

    private enum Area { None, SV, Hue, Alpha }
    private Area _active;
    private bool _didDrag;

    public void OnInitializePotentialDrag(PointerEventData e)
    {
        if (e is not null) e.useDragThreshold = false;
    }

    public void OnPointerDown(PointerEventData e) { _didDrag = false; HandleInput(e, true); }
    public void OnBeginDrag(PointerEventData e) { _didDrag = true; HandleInput(e, false); }
    public void OnDrag(PointerEventData e) => HandleInput(e, false);
    public void OnEndDrag(PointerEventData e) { _active = Area.None; Picker?.NotifyCommit(); }
    public void OnPointerUp(PointerEventData e) { if (!_didDrag) Picker?.NotifyCommit(); }

    private void HandleInput(PointerEventData e, bool isDown)
    {
        if (Picker is null || Panel is null) return;
        if (!Panel.ScreenToFrac(e, out float fx, out float fy)) return;

        if (isDown)
        {
            if (InRect(fx, fy, ColorPickerPanel.SvL, ColorPickerPanel.SvR,
            ColorPickerPanel.SvB, ColorPickerPanel.SvT))
            _active = Area.SV;
            else if (InRect(fx, fy, ColorPickerPanel.HuL, ColorPickerPanel.HuR,
            ColorPickerPanel.HuB, ColorPickerPanel.HuT))
            _active = Area.Hue;
            else if (InRect(fx, fy, ColorPickerPanel.AlL, ColorPickerPanel.AlR,
            ColorPickerPanel.AlB, ColorPickerPanel.AlT))
            _active = Area.Alpha;
            else
            _active = Area.None;
        }

        switch (_active)
        {
            case Area.SV:
            Picker.Sat = Mathf.Clamp01(Remap(fx, ColorPickerPanel.SvL, ColorPickerPanel.SvR));
            Picker.Val = Mathf.Clamp01(Remap(fy, ColorPickerPanel.SvB, ColorPickerPanel.SvT));
            Picker.NotifyColorChanged();
            break;

            case Area.Hue:
            float ht = Remap(fy, ColorPickerPanel.HuB, ColorPickerPanel.HuT);
            Picker.Hue = Mathf.Clamp01(1f - ht);
            Picker.NotifyColorChanged();
            break;

            case Area.Alpha:
            Picker.Alpha = Mathf.Clamp01(Remap(fx, ColorPickerPanel.AlL, ColorPickerPanel.AlR));
            Picker.NotifyColorChanged();
            break;
        }
    }

    private static bool InRect(float fx, float fy, float l, float r, float b, float t)
    => fx >= l && fx <= r && fy >= b && fy <= t;

    private static float Remap(float v, float min, float max)
    => (v - min) / (max - min);
}