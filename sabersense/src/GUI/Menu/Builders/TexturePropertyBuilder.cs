// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Catalog;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using SaberSense.Customization;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Menu.Popups;
using SaberSense.GUI.Menu.Controllers;
using SaberSense.Profiles;
using SaberSense.Rendering.Shaders;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SaberSense.GUI.Menu.Builders;

internal sealed class TexturePropertyBuilder : MaterialPropertyBuilderBase
{
    private readonly TextureCache _textureCache;
    private readonly IModLogger? _logger;
    private TexturePickerPopup? _picker;

    public TexturePropertyBuilder(
    MaterialEditingController materialController,
    SplitPopupManager splitPopup,
    SaberSelectionController selectionController,
    Persistence.IJsonProvider jsonProvider,
    PreviewSession previewSession,
    TextureCache textureCache,
    IModLogger? logger = null)
    : base(materialController, splitPopup, selectionController, jsonProvider, previewSession)
    {
        _textureCache = textureCache;
        _logger = logger;
    }

    public GameObject BuildSharedRow(Material material, string matName,
    ShaderProperty prop, SaberCustomization customization,
    RectTransform parent, RectTransform canvasRoot)
    {
        EnsurePicker(canvasRoot);

        var currentTex = material.GetTexture(prop.Id);
        var btn = new BaseButton(FormatTextureName(currentTex));
        var mat = material;
        var pid = prop.Id;
        var capturedHand = SourceHand;

        btn.OnClick = () =>
        {
            var originalTex = MaterialController.GetOriginalTexture(matName, prop.Name) as Texture2D;
            var curTex = mat.GetTexture(pid);
            string? curTexDisplayName = curTex != null ? Path.GetFileName(curTex.name) : null;
            ErrorBoundary.FireAndForget(
            _picker!.ShowAsync(newTex =>
            {
                ApplyToBothHands(mat, matName, m => m.SetTexture(pid, newTex));
                btn.Label.SetText(FormatTextureName(newTex));
                MaterialController.Snapshot(matName, mat, capturedHand);
            }, originalTex, curTexDisplayName), _logger!, "SaberTexturePicker");
        };

        var row = new UIPropRow(prop.Description + " (Tex)", btn, 30);
        row.RectTransform.SetParent(parent, false);
        row.AddLayoutElement(preferredHeight: UITheme.LabelHeight);
        SplitPopup.MakeLabelInteractiveInPropRow(row, matName, prop.Name, customization);
        return row.GameObject;
    }

    public List<GameObject> BuildSplitRows(Material material, string matName,
    ShaderProperty prop, SaberCustomization customization,
    RectTransform parent, RectTransform canvasRoot)
    {
        EnsurePicker(canvasRoot);

        var pid = prop.Id;

        var leftMat = material;
        var rightMat = MaterialController.FindMaterialOnHand(matName, SaberHand.Right) ?? material;

        return BuildSplitRows(material, matName, prop, customization, parent,
        (handVal, hand, tag) =>
        {
            var targetMat = hand == SaberHand.Left ? leftMat : rightMat;
            Texture currentTex = targetMat.GetTexture(pid);
            string currentTexName = handVal?.ToObject<string>() ?? currentTex?.name ?? "None";

            var btn = new BaseButton(FormatTextureName(currentTexName));
            var capturedHand = hand;

            btn.OnClick = () =>
            {
                var originalTex = MaterialController.GetOriginalTexture(matName, prop.Name) as Texture2D;
                var clickTimeHandVal = customization.GetPropertyForHand(matName, prop.Name, capturedHand);
                string? clickTimeTexName = clickTimeHandVal?.ToObject<string>();
                if (!string.IsNullOrEmpty(clickTimeTexName))
                clickTimeTexName = Path.GetFileName(clickTimeTexName);
                ErrorBoundary.FireAndForget(
                _picker!.ShowAsync(newTex =>
                {
                    targetMat.SetTexture(pid, newTex);
                    btn.Label.SetText(FormatTextureName(newTex));

                    var texToken = newTex != null ? new JValue(newTex.name) : new JValue("");
                    MaterialController.SnapshotSplit(
                    matName, prop.Name,
                    texToken, capturedHand);
                }, originalTex, clickTimeTexName), _logger!, "SaberTexturePicker");
            };

            var row = new UIPropRow(prop.Description + " " + tag, btn, 30);
            row.RectTransform.SetParent(parent, false);
            row.AddLayoutElement(preferredHeight: UITheme.LabelHeight);
            SplitPopup.MakeLabelInteractiveInPropRow(row, matName, prop.Name, customization);
            return row.GameObject;
        });
    }

    public void Cleanup() => _picker?.Exit();

    private void EnsurePicker(RectTransform canvasRoot)
    {
        _picker ??= new TexturePickerPopup(canvasRoot, _textureCache);
    }

    private static string FormatTextureName(Texture? tex)
    {
        if (tex == null) return "None";
        return FormatTextureName(tex.name);
    }

    private static string FormatTextureName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "None";
        name = Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrEmpty(name)) return "None";
        if (name.Length > 20) name = name[..20] + "...";
        return name;
    }
}