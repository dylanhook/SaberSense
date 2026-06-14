// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core;
using SaberSense.Customization;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Menu.Controllers;
using SaberSense.Profiles;
using SaberSense.Rendering.Shaders;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.GUI.Menu.Builders;

internal sealed class MaterialPropertyRowBuilder(
MaterialEditingController materialController,
SplitPopupManager splitPopup,
ColorPropertyBuilder colorBuilder,
FloatPropertyBuilder floatBuilder,
TogglePropertyBuilder toggleBuilder,
TexturePropertyBuilder textureBuilder,
PreviewSession previewSession)
{
    public List<GameObject> BuildPropertyRows(Material material, string matName,
    SaberCustomization customization, IReadOnlyList<ShaderProperty> shaderInfo,
    RectTransform parent, RectTransform canvasRoot)
    {
        List<GameObject> rows = [];

        var sourceHand = previewSession?.FocusedHand ?? SaberHand.Left;
        floatBuilder.CaptureSourceHand();
        colorBuilder.CaptureSourceHand();
        toggleBuilder.CaptureSourceHand();
        textureBuilder.CaptureSourceHand();

        bool hasSavedOverride = customization?.MaterialOverrides?.ContainsKey(matName) == true;
        float rawCustomColorsVal;
        if (customization?.MaterialOverrides?.TryGetValue(matName, out var _mo) == true && _mo["_CustomColors"] is not null)
        rawCustomColorsVal = _mo["_CustomColors"]!.ToObject<float>();
        else if (material.HasProperty(SaberSense.Core.Utilities.ShaderUtils.CustomColorToggleId))
        rawCustomColorsVal = materialController.GetOriginalFloat(matName, "_CustomColors");
        else
        rawCustomColorsVal = 0f;
        bool rawCustomColors = rawCustomColorsVal > 0.5f;
        bool overrideColorOn = hasSavedOverride && !rawCustomColors;

        List<GameObject> colorRows = [];

        ShaderProperty? customColorsProp = null;
        foreach (var p in shaderInfo)
        {
            if (p.Name == "_CustomColors") { customColorsProp = p; break; }
        }

        if (customColorsProp is not null)
        {
            var overrideRow = BuildOverrideColorRow(material, matName, customization!, customColorsProp,
            overrideColorOn, colorRows, parent, canvasRoot, sourceHand);
            rows.Add(overrideRow);

            if (material.HasProperty(SaberSense.Core.Utilities.ShaderUtils.TintColorId)
            && customization is not null && customization.IsPropertySplit(matName, "_Color"))
            {
                var splitRows = colorBuilder.BuildSplitRows(material, matName,
                FindProperty(shaderInfo, "_Color") ?? customColorsProp, customization, parent, canvasRoot);
                foreach (var row in splitRows)
                {
                    colorRows.Add(row);
                    row.SetActive(overrideColorOn);
                    rows.Add(row);
                }
            }
        }

        foreach (var prop in shaderInfo)
        {
            if (prop.Name == "_CustomColors") continue;
            if (prop.Name == "_Color" && customColorsProp is not null) continue;

            bool isSplit = customization is not null && customization.IsPropertySplit(matName, prop.Name);
            var control = MaterialEditorPolicy.Classify(prop);

            switch (control.Kind)
            {
                case MaterialControlKind.Toggle:
                if (!isSplit)
                rows.Add(toggleBuilder.BuildSharedRow(material, matName, prop, customization!, parent));
                else
                rows.AddRange(toggleBuilder.BuildSplitRows(material, matName, prop, customization!, parent));
                break;
                case MaterialControlKind.Slider:
                if (!isSplit)
                rows.Add(floatBuilder.BuildSharedSliderRow(material, matName, prop, customization!, parent, control.Min, control.Max));
                else
                rows.AddRange(floatBuilder.BuildSplitSliderRows(material, matName, prop, customization!, parent, control.Min, control.Max));
                break;
                case MaterialControlKind.Color:
                if (!isSplit)
                {
                    var row = colorBuilder.BuildSharedRow(material, matName, prop, customization!, parent, canvasRoot);
                    rows.Add(row);
                    if (prop.Name == "_Color")
                    {
                        colorRows.Add(row);
                        row.SetActive(overrideColorOn);
                    }
                }
                else
                {
                    var splitRows = colorBuilder.BuildSplitRows(material, matName, prop, customization!, parent, canvasRoot);
                    foreach (var row in splitRows)
                    {
                        rows.Add(row);
                        if (prop.Name == "_Color")
                        {
                            colorRows.Add(row);
                            row.SetActive(overrideColorOn);
                        }
                    }
                }
                break;
                case MaterialControlKind.Texture:
                if (!isSplit)
                rows.Add(textureBuilder.BuildSharedRow(material, matName, prop, customization!, parent, canvasRoot));
                else
                rows.AddRange(textureBuilder.BuildSplitRows(material, matName, prop, customization!, parent, canvasRoot));
                break;
                case MaterialControlKind.Skip:

                break;
                default:

                throw new System.NotSupportedException($"Unhandled control kind {control.Kind} building rows for '{prop.Name}'.");
            }
        }

        return rows;
    }

    private GameObject BuildOverrideColorRow(Material material, string matName,
    SaberCustomization customization, ShaderProperty customColorsProp,
    bool overrideColorOn, List<GameObject> colorRows,
    RectTransform parent, RectTransform canvasRoot, SaberHand sourceHand)
    {
        var toggle = new UIToggle().SetValue(overrideColorOn);
        var mat = material;
        var pid = customColorsProp.Id;
        var capturedColorRows = colorRows;
        var originalColor = materialController.GetOriginalColor(matName, "_Color");

        UIColorPicker? inlinePickerRef = null;

        toggle.OnValueChanged(v =>
        {
            materialController.ApplyToBothHands(mat, matName, m => m.SetFloat(pid, v ? 0 : 1));

            foreach (var row in capturedColorRows)
            row?.SetActive(v);
            if (v && inlinePickerRef is not null && mat.HasProperty(SaberSense.Core.Utilities.ShaderUtils.TintColorId))
            {
                var overrideColor = inlinePickerRef.GetColor();
                materialController.ApplyToBothHands(mat, matName, m => m.SetColor(SaberSense.Core.Utilities.ShaderUtils.TintColorId, overrideColor));
            }
            else if (!v && mat.HasProperty(SaberSense.Core.Utilities.ShaderUtils.TintColorId))
            {
                materialController.ApplyToBothHands(mat, matName, m => m.SetColor(SaberSense.Core.Utilities.ShaderUtils.TintColorId, originalColor));
            }
            materialController.Snapshot(matName, mat, sourceHand);
        });

        var rowGO = UILayoutFactory.CheckboxRow("  Override color", toggle, parent, out var overrideLbl);
        var cbRowRect = (RectTransform)rowGO.transform;
        splitPopup.MakeLabelInteractive(overrideLbl, matName, "_Color", customization, toggle);

        if (mat.HasProperty(SaberSense.Core.Utilities.ShaderUtils.TintColorId))
        {
            bool colorIsSplit = customization is not null && customization.IsPropertySplit(matName, "_Color");
            if (!colorIsSplit)
            {
                var inlineColor = mat.GetColor(SaberSense.Core.Utilities.ShaderUtils.TintColorId);
                var capturedToggle = toggle;
                var inlinePicker = new UIColorPicker("InlineCP_" + matName, canvasRoot)
                .SetColor(inlineColor)
                .OnColorChanged(c =>
                {
                    if (!capturedToggle.IsOn) return;

                    materialController.ApplyToBothHands(mat, matName, m => m.SetColor(SaberSense.Core.Utilities.ShaderUtils.TintColorId, c));
                    materialController.RefreshPropertyBlocks();
                })
                .OnCommit(c =>
                {
                    materialController.Snapshot(matName, mat, sourceHand);
                });
                inlinePicker.SetResetColor(materialController.GetOriginalColor(matName, "_Color"));

                inlinePicker.SetParent(cbRowRect).AddLayoutElement(preferredWidth: UITheme.SwatchWidth, preferredHeight: UITheme.SwatchHeight);
                inlinePickerRef = inlinePicker;
            }
        }

        return rowGO;
    }

    private static ShaderProperty? FindProperty(IReadOnlyList<ShaderProperty> info, string name)
    {
        foreach (var p in info)
        if (p.Name == name) return p;
        return null;
    }
}