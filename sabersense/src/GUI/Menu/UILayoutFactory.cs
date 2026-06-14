// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Framework.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Menu;

internal static class UILayoutFactory
{
    public static RectTransform TabRoot(string name, RectTransform parent)
    {
        var root = new VBox(name).SetParent(parent);
        Object.Destroy(root.GameObject.GetComponent<ContentSizeFitter>());
        root.SetAnchors(Vector2.zero, Vector2.one);
        root.SetSpacing(UITheme.GroupGap)
        .SetPadding((int)UITheme.TabPadLeft, (int)UITheme.TabPadRight,
        (int)UITheme.TabPadTop, (int)UITheme.TabPadBottom);
        root.LayoutGroup.childForceExpandHeight = false;
        root.AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);
        return root.RectTransform;
    }

    public static (HBox columns, VBox left, VBox right) TabColumns(RectTransform parent)
    {
        var columns = new HBox("TabCols").SetParent(parent);
        Object.Destroy(columns.GameObject.GetComponent<ContentSizeFitter>());
        columns.SetSpacing(UITheme.ColumnGap)
        .AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var left = new VBox("LeftCol").SetParent(columns.RectTransform);
        Object.Destroy(left.GameObject.GetComponent<ContentSizeFitter>());
        left.SetSpacing(UITheme.GroupGap).SetPadding(0, 0, 0, 0)
        .AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var right = new VBox("RightCol").SetParent(columns.RectTransform);
        Object.Destroy(right.GameObject.GetComponent<ContentSizeFitter>());
        right.SetSpacing(UITheme.GroupGap).SetPadding(0, 0, 0, 0)
        .AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        return (columns, left, right);
    }

    public static GameObject SliderRow(string label, UIElement control, RectTransform parent)
    {
        var row = new VBox(label + "SR").SetParent(parent);
        row.SetSpacing(UITheme.RowInnerSpacing).SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.SliderRowHeight, flexibleWidth: 1);
        row.LayoutGroup.childForceExpandWidth = true;
        new UILabel(label + "L", label).SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextLabel)
        .SetAlignment(TMPro.TextAlignmentOptions.Left).SetParent(row.RectTransform).AddLayoutElement(preferredHeight: UITheme.LabelHeight);
        control.SetParent(row.RectTransform).AddLayoutElement(preferredHeight: 1f, flexibleWidth: 1);
        return row.GameObject;
    }

    public static GameObject DropdownRow(string label, UIElement control, RectTransform parent)
    {
        var row = new VBox(label + "DR").SetParent(parent);
        row.SetSpacing(UITheme.RowInnerSpacing).SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.DropdownRowHeight, flexibleWidth: 1);
        row.LayoutGroup.childForceExpandWidth = true;
        new UILabel(label + "L", label).SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextLabel)
        .SetAlignment(TMPro.TextAlignmentOptions.Left).SetParent(row.RectTransform).AddLayoutElement(preferredHeight: UITheme.LabelHeight);
        control.SetParent(row.RectTransform).AddLayoutElement(preferredHeight: UITheme.LabelHeight, flexibleWidth: 1);
        return row.GameObject;
    }

    public static void KeybindRow(string label, UIElement control, RectTransform parent)
    {
        var row = new HBox(label + "KR").SetParent(parent);
        row.SetSpacing(UITheme.ColumnGap).SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.LabelHeight, flexibleWidth: 1);
        row.LayoutGroup.childAlignment = TextAnchor.MiddleLeft;
        row.LayoutGroup.childForceExpandHeight = false;
        new UILabel(label + "L", label).SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextLabel)
        .SetAlignment(TMPro.TextAlignmentOptions.Left).SetParent(row.RectTransform).AddLayoutElement(flexibleWidth: 1, preferredHeight: UITheme.LabelHeight);
        control.SetParent(row.RectTransform).AddLayoutElement(preferredWidth: 20f, preferredHeight: UITheme.LabelHeight);
    }

    public static GameObject CheckboxKeybindRow(string label, UIElement checkbox, UIElement keybind, RectTransform parent)
    {
        var row = new HBox(label + "CKR").SetParent(parent);
        row.SetSpacing(UITheme.ColumnGap).SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.LabelHeight, flexibleWidth: 1);
        row.LayoutGroup.childAlignment = TextAnchor.MiddleLeft;
        row.LayoutGroup.childForceExpandHeight = false;
        checkbox.SetParent(row.RectTransform);
        new UILabel(label + "L", label).SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextSecondary)
        .SetAlignment(TMPro.TextAlignmentOptions.Left).SetParent(row.RectTransform).AddLayoutElement(flexibleWidth: 1, preferredHeight: UITheme.LabelHeight);
        keybind.SetParent(row.RectTransform).AddLayoutElement(preferredWidth: 20f, preferredHeight: UITheme.LabelHeight);
        if (checkbox is UIToggle toggle) AddRowHitArea(row.RectTransform, toggle);

        return row.GameObject;
    }

    public static GameObject CheckboxRow(string label, UIElement control, RectTransform parent, bool experimental = false)
    => CheckboxRow(label, control, parent, out _, experimental);

    public static GameObject CheckboxRow(string label, UIElement control, RectTransform parent, out UILabel labelRef, bool experimental = false)
    {
        var row = new HBox(label + "CR").SetParent(parent);
        row.SetSpacing(UITheme.ColumnGap).SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.LabelHeight, flexibleWidth: 1);
        row.LayoutGroup.childAlignment = TextAnchor.MiddleLeft;
        row.LayoutGroup.childForceExpandHeight = false;
        control.SetParent(row.RectTransform);
        var color = experimental ? UITheme.TextExperimental : UITheme.TextSecondary;
        labelRef = new UILabel(label + "L", label).SetFontSize(UITheme.FontSmall).SetColor(color)
        .SetAlignment(TMPro.TextAlignmentOptions.Left).SetParent(row.RectTransform).AddLayoutElement(flexibleWidth: 1, preferredHeight: UITheme.LabelHeight);
        if (control is UIToggle toggle) AddRowHitArea(row.RectTransform, toggle);
        return row.GameObject;
    }

    public static void AddRowHitArea(RectTransform row, UIToggle toggle)
    {
        var hitGO = new GameObject("RowHit");
        hitGO.transform.SetParent(row, false);
        var hitR = hitGO.AddComponent<RectTransform>();
        hitR.anchorMin = Vector2.zero;
        hitR.anchorMax = Vector2.one;
        hitR.sizeDelta = Vector2.zero;
        hitR.SetAsFirstSibling();
        var hitLE = hitGO.AddComponent<LayoutElement>();
        hitLE.ignoreLayout = true;
        var hitImg = hitGO.AddComponent<Image>();
        hitImg.color = new Color(0, 0, 0, 0);
        hitImg.raycastTarget = true;
        var handler = hitGO.AddComponent<PointerEventHandler>();
        handler.OnClick = () => toggle.InvokeToggle();
    }

    public static (HBox, UILabel) PartCell(string name, bool selected, RectTransform parent)
    {
        var cell = new HBox(name + "Cell").SetParent(parent);
        cell.SetSpacing(0).SetPadding(2, 2, 1, 1).AddLayoutElement(preferredHeight: UITheme.SliderRowHeight + 1, flexibleWidth: 1);
        cell.LayoutGroup.childAlignment = TextAnchor.MiddleLeft;
        if (selected)
        {
            var bg = new UIImage(name + "Bg").SetColor(UITheme.SurfaceCell);
            bg.RectTransform.SetParent(cell.RectTransform, false);
            bg.SetAnchors(Vector2.zero, Vector2.one);
            bg.RectTransform.SetAsFirstSibling();
            bg.AddComponent<LayoutElement>().ignoreLayout = true;
        }
        var label = new UILabel(name + "L", name).SetFontSize(UITheme.FontSmall)
        .SetColor(selected ? UITheme.TextPrimary : UITheme.TextMuted)
        .SetAlignment(TMPro.TextAlignmentOptions.Left)
        .SetParent(cell.RectTransform);
        label.AddLayoutElement(flexibleWidth: 1);
        return (cell, label);
    }

    public static float BuildKaabaOutline(RectTransform parent, string namePrefix)
    {
        byte[] outlineGrays = { 10, 60, 40, 40, 40, 60 };
        float inset = 0f;
        for (int b = 0; b < outlineGrays.Length; b++)
        {
            var borderImg = new UIImage(namePrefix + b)
            .SetColor(new Color32(outlineGrays[b], outlineGrays[b], outlineGrays[b], 255));
            borderImg.RectTransform.SetParent(parent, false);
            borderImg.SetAnchors(Vector2.zero, Vector2.one);
            borderImg.RectTransform.offsetMin = new Vector2(inset, inset);
            borderImg.RectTransform.offsetMax = new Vector2(-inset, -inset);
            borderImg.ImageComponent.raycastTarget = false;
            inset += 0.15f;
        }
        return inset;
    }
}