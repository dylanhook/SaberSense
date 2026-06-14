// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Menu.Builders;
using SaberSense.Rendering.Shaders;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.GUI.Menu.Popups;

internal sealed class MaterialEditorPopup
{
    private const float ModalWidth = 90f;
    private const float ModalHeight = 70f;

    private readonly UIModal _modal;
    private readonly UIGroupBox _groupBox;
    private readonly ShaderIntrospector _shaderCache;
    private readonly RectTransform _canvasRoot;
    private readonly TexturePickerPopup? _texturePicker;
    private readonly Rendering.Materials.OriginalMaterialCache? _originalCache;
    private readonly IModLogger? _logger;

    private Action? _onClose;
    private Action<Material>? _onPropertyChanged;
    private Action<Material>? _onCommit;

    private Material? _activeMaterial;
    private string? _activeMaterialName;
    private Dictionary<int, (ShaderProperty Prop, object Value)> _snapshot = [];

    public bool IsOpen => _modal.Backdrop.GameObject.activeSelf;

    public MaterialEditorPopup(RectTransform canvasRoot, ShaderIntrospector shaderCache, TextureCache textureStore, Rendering.Materials.OriginalMaterialCache? originalCache = null, IModLogger? logger = null, Action? onClose = null, Action<Material>? onPropertyChanged = null, Action<Material>? onCommit = null)
    {
        _shaderCache = shaderCache;
        _canvasRoot = canvasRoot;
        _originalCache = originalCache;
        _logger = logger;
        _onClose = onClose;
        _onPropertyChanged = onPropertyChanged;
        _onCommit = onCommit;
        _modal = new UIModal("Material editor", canvasRoot, ModalWidth, ModalHeight);
        if (textureStore is not null)
        _texturePicker = new TexturePickerPopup(canvasRoot, textureStore);

        _groupBox = new UIGroupBox("Shader properties");
        _groupBox.RectTransform.SetParent(_modal.ContentArea.RectTransform, false);
        _groupBox.AddLayoutElement(flexibleHeight: 1, flexibleWidth: 1);

        _modal.AddButtons("Close", () => { _modal.Hide(); _onClose?.Invoke(); },
        "Cancel", () => { RestoreSnapshot(); _modal.Hide(); _onClose?.Invoke(); });

        _modal.ButtonsRow!.LayoutGroup!.childAlignment = TextAnchor.MiddleCenter;
        _modal.ButtonsRow!.LayoutGroup!.childForceExpandWidth = true;
    }

    private void SnapshotMaterial(Material material, IReadOnlyList<ShaderProperty> shaderProps)
    {
        _snapshot.Clear();
        _activeMaterial = material;
        foreach (var prop in shaderProps)
        {
            var value = prop.ReadValue(material);
            if (value is not null) _snapshot[prop.Id] = (prop, value);
        }
    }

    private void RestoreSnapshot()
    {
        if (_activeMaterial == null) return;
        foreach (var entry in _snapshot.Values)
        {
            entry.Prop.WriteTo(_activeMaterial, entry.Value);
        }
        _onPropertyChanged?.Invoke(_activeMaterial);

        _onCommit?.Invoke(_activeMaterial);
    }

    public void Show(SaberSense.Rendering.MaterialHandle MaterialHandle, string? materialName = null)
    {
        if (MaterialHandle is null || MaterialHandle.Material == null) return;
        var material = MaterialHandle.Material;
        _activeMaterialName = materialName ?? SaberSense.Core.Utilities.MaterialNameResolver.StripInstanceSuffix(material.name);
        foreach (Transform child in _groupBox.Content)
        {
            UnityEngine.Object.Destroy(child.gameObject);
        }

        var shaderProps = _shaderCache[material.shader]!;
        SnapshotMaterial(material, shaderProps);

        foreach (var prop in shaderProps)
        {
            var control = MaterialEditorPolicy.Classify(prop);
            switch (control.Kind)
            {
                case MaterialControlKind.Toggle:
                BuildToggleRow(material, prop);
                break;
                case MaterialControlKind.Slider:
                BuildSliderRow(material, prop, control.Min, control.Max);
                break;
                case MaterialControlKind.Color:
                BuildColorRow(material, prop);
                break;
                case MaterialControlKind.Texture:
                BuildTextureRow(material, prop);
                break;
                case MaterialControlKind.Skip:

                break;
                default:

                throw new NotSupportedException($"Unhandled control kind {control.Kind} building editor row for '{prop.Name}'.");
            }
        }

        _modal.Show();
    }

    public void Close()
    {
        _modal.Hide();
        _texturePicker?.Exit();
    }

    private void BuildToggleRow(Material material, ShaderProperty prop)
    {
        bool val = ((float)prop.ReadValue(material)!) > 0;
        var toggle = new UIToggle().SetValue(val);
        toggle.OnValueChanged(v => { material.SetFloat(prop.Id, v ? 1 : 0); _onPropertyChanged?.Invoke(material); });
        UILayoutFactory.CheckboxRow("  " + prop.Description, toggle, _groupBox.Content);
    }

    private void BuildSliderRow(Material material, ShaderProperty prop, float min, float max)
    {
        var val = material.GetFloat(prop.Id);
        var slider = new UISlider().SetRange(min, max).SetValue(val);
        slider.OnValueChanged(v => { material.SetFloat(prop.Id, v); _onPropertyChanged?.Invoke(material); });
        slider.OnCommit(v => _onCommit?.Invoke(material));
        UILayoutFactory.SliderRow(prop.Description, slider, _groupBox.Content);
    }

    private void BuildColorRow(Material material, ShaderProperty prop)
    {
        var color = material.GetColor(prop.Id);
        var colorPicker = new UIColorPicker("ColorPicker", _canvasRoot)
        .SetColor(color)
        .OnColorChanged(c => { material.SetColor(prop.Id, c); _onPropertyChanged?.Invoke(material); })
        .OnCommit(c => _onCommit?.Invoke(material));
        colorPicker.SetResetColor(color);

        var row = new UIPropRow(prop.Description, colorPicker, 10);
        row.RectTransform.SetParent(_groupBox.Content, false);
        row.AddLayoutElement(preferredHeight: UITheme.LabelHeight);
    }

    private void BuildTextureRow(Material material, ShaderProperty prop)
    {
        var currentTex = material.GetTexture(prop.Id);
        var texName = FormatTextureName(currentTex);

        var btn = new BaseButton(texName);
        var row = new UIPropRow(prop.Description + " (Texture)", btn, 30);
        row.RectTransform.SetParent(_groupBox.Content, false);
        row.AddLayoutElement(preferredHeight: UITheme.LabelHeight);

        var matName = _activeMaterialName;
        var propName = prop.Name;
        var snapPropId = prop.Id;
        btn.OnClick = () =>
        {
            var originalTex = (_originalCache?.GetOriginalTexture(matName!, propName) as Texture2D);

            if (originalTex == null && _snapshot.TryGetValue(snapPropId, out var snapped) && snapped.Value is Texture2D snapTex)
            originalTex = snapTex;
            if (_texturePicker is not null) ErrorBoundary.FireAndForget(_texturePicker.ShowAsync((Texture2D? newTex) =>
            {
                material.SetTexture(prop.Id, newTex);
                if (newTex != null) btn.Label.SetText(FormatTextureName(newTex));
                _onPropertyChanged?.Invoke(material);
            }, originalTex), _logger!, "TexturePicker");
        };
    }

    private static string FormatTextureName(Texture tex)
    {
        if (tex == null) return "None";
        var name = tex.name;
        if (string.IsNullOrEmpty(name)) return "None";
        if (name.Length > 15) name = name[..15] + "...";
        return name;
    }
}