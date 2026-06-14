// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Menu.Controllers;
using SaberSense.Profiles;
using SaberSense.Rendering.Shaders;
using System;
using UnityEngine;

namespace SaberSense.GUI.Menu;

internal sealed class SplitPopupManager
{
    private readonly MaterialEditingController _materialController;
    private readonly SaberSelectionController _selectionController;
    private readonly Persistence.IJsonProvider _jsonProvider;

    private GameObject? _splitPopupBackdrop;
    private GameObject? _splitPopupPanel;

    public Action? OnPropertyChanged;

    public RectTransform? CanvasRoot { get; set; }

    public SplitPopupManager(
    MaterialEditingController materialController,
    SaberSelectionController selectionController,
    Persistence.IJsonProvider jsonProvider)
    {
        _materialController = materialController;
        _selectionController = selectionController;
        _jsonProvider = jsonProvider;
    }

    public void MakeLabelInteractive(UILabel label, string matName, string propName,
    SaberCustomization customization, UIToggle? toggle = null)
    {
        if (customization is null) return;
        label.TextComponent.raycastTarget = true;
        var handler = label.GameObject.AddComponent<PointerEventHandler>();
        handler.OnClickEvent = e =>
        {
            if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
            Show(label.RectTransform, matName, propName, customization);
            else if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left)
            toggle?.InvokeToggle();
        };
        var hoverDetector = label.GameObject.AddComponent<SplitKeyHoverDetector>();
        hoverDetector.MatName = matName;
        hoverDetector.PropName = propName;
        hoverDetector.Customization = customization;
        hoverDetector.PopupManager = this;
        hoverDetector.LabelRT = label.RectTransform;
    }

    public void MakeLabelInteractiveInRow(GameObject rowGO, string matName, string propName,
    SaberCustomization customization)
    {
        if (customization is null) return;
        var lbl = rowGO.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (lbl == null) return;
        var rt = lbl.GetComponent<RectTransform>();
        lbl.raycastTarget = true;
        var handler = lbl.gameObject.AddComponent<PointerEventHandler>();
        handler.OnClickEvent = e =>
        {
            if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
            Show(rt, matName, propName, customization);
        };
        var hoverDetector = lbl.gameObject.AddComponent<SplitKeyHoverDetector>();
        hoverDetector.MatName = matName;
        hoverDetector.PropName = propName;
        hoverDetector.Customization = customization;
        hoverDetector.PopupManager = this;
        hoverDetector.LabelRT = rt;
    }

    public void MakeLabelInteractiveInPropRow(UIPropRow row, string matName, string propName,
    SaberCustomization customization)
    {
        if (customization is null || row?.Label is null) return;
        var lbl = row.Label;
        lbl.TextComponent.raycastTarget = true;
        var handler = lbl.GameObject.AddComponent<PointerEventHandler>();
        handler.OnClickEvent = e =>
        {
            if (e.button == UnityEngine.EventSystems.PointerEventData.InputButton.Right)
            Show(lbl.RectTransform, matName, propName, customization);
        };
        var hoverDetector = lbl.GameObject.AddComponent<SplitKeyHoverDetector>();
        hoverDetector.MatName = matName;
        hoverDetector.PropName = propName;
        hoverDetector.Customization = customization;
        hoverDetector.PopupManager = this;
        hoverDetector.LabelRT = lbl.RectTransform;
    }

    public void Show(RectTransform anchorRT, string matName, string propName,
    SaberCustomization customization)
    {
        if (CanvasRoot == null || customization is null) return;
        Hide();

        bool isSplit = customization.IsPropertySplit(matName, propName);

        (string Label, Action OnClick) item;
        if (!isSplit)
        {
            item = ("Split", () =>
            {
                if (!customization.MaterialOverrides.ContainsKey(matName))
                {
                    var activeMat = _materialController.FindLiveMaterial(matName);
                    if (activeMat != null) _materialController.Snapshot(matName, activeMat);
                }

                if (customization.MaterialOverrides.ContainsKey(matName) && customization.MaterialOverrides[matName][propName] is null)
                {
                    var liveMat = _materialController.FindLiveMaterial(matName);
                    if (liveMat != null)
                    {
                        var shaderInfo = _materialController.GetShaderInfo(liveMat.shader);
                        if (shaderInfo is not null)
                        {
                            foreach (var p in shaderInfo)
                            {
                                if (p.Name == propName)
                                {
                                    var json = MaterialPropertyCodec.Encode(p, liveMat, _jsonProvider);
                                    if (json is not null) customization.MaterialOverrides[matName][propName] = json;
                                    break;
                                }
                            }
                        }
                    }
                }
                _materialController.SplitProperty(_selectionController.SelectedEntry!, matName, propName);
                Hide();
                OnPropertyChanged?.Invoke();
            });
        }
        else
        {
            item = ("Unsplit", () =>
            {
                _materialController.UnsplitProperty(_selectionController.SelectedEntry!, matName, propName);
                Hide();
                OnPropertyChanged?.Invoke();
            });
        }

        (_splitPopupBackdrop, _splitPopupPanel) = UIContextMenu.Show(
        CanvasRoot, anchorRT, UIContextMenu.Alignment.Left, Hide, item);
    }

    public void Hide()
    {
        if (_splitPopupBackdrop != null) { UnityEngine.Object.Destroy(_splitPopupBackdrop); _splitPopupBackdrop = null; }
        if (_splitPopupPanel != null) { UnityEngine.Object.Destroy(_splitPopupPanel); _splitPopupPanel = null; }
    }
}