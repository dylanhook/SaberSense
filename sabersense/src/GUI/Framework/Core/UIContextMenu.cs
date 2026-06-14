// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public static class UIContextMenu
{
    private const float ButtonWidth = 14f;
    private const float ButtonHeight = 3.3f;
    private const float ButtonSpacing = 0.15f;

    public enum Alignment
    {
        Right,

        Left,
    }

    public static (GameObject Backdrop, GameObject Panel) Show(
    RectTransform canvasRoot, RectTransform anchor, Alignment align,
    Action onDismiss, params (string Label, Action OnClick)[] items)
    {
        var backdropGO = new GameObject("CtxBackdrop");
        backdropGO.AddComponent<RectTransform>();
        var bImg = backdropGO.AddComponent<Image>();
        bImg.material = UIMaterials.NoBloomMaterial;
        bImg.color = new Color(0, 0, 0, 0.01f);
        bImg.raycastTarget = true;
        UIPopupHelper.SetupPopupCanvas(backdropGO, canvasRoot, UIZLayer.ContextBackdrop);
        backdropGO.AddComponent<PointerEventHandler>().OnClick = onDismiss;
        backdropGO.transform.SetParent(canvasRoot, false);
        var bRect = backdropGO.GetComponent<RectTransform>();
        bRect.anchorMin = Vector2.zero;
        bRect.anchorMax = Vector2.one;
        bRect.sizeDelta = Vector2.zero;
        bRect.anchoredPosition = Vector2.zero;
        backdropGO.transform.SetAsLastSibling();

        var panelGO = new GameObject("CtxPanel");
        var panelRect = panelGO.AddComponent<RectTransform>();
        UIPopupHelper.SetupPopupCanvas(panelGO, canvasRoot, UIZLayer.ContextMenu);
        panelGO.transform.SetParent(canvasRoot, false);

        var corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector3 localBL = canvasRoot.InverseTransformPoint(corners[0]);

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight * items.Length);
        if (align == Alignment.Right)
        {
            Vector3 localBR = canvasRoot.InverseTransformPoint(corners[3]);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.anchoredPosition = new Vector2(localBR.x, localBL.y);
        }
        else
        {
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(localBL.x, localBL.y);
        }

        var borderImg = panelGO.AddComponent<Image>();
        borderImg.material = UIMaterials.NoBloomMaterial;
        borderImg.color = UITheme.Border;
        borderImg.raycastTarget = true;

        var vlg = panelGO.AddComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = true;
        vlg.spacing = ButtonSpacing;
        vlg.padding = new RectOffset(0, 0, 0, 0);

        foreach (var (label, onClick) in items)
        UIPopupHelper.CreateContextButton(label, panelGO.transform, onClick);

        panelGO.transform.SetAsLastSibling();
        return (backdropGO, panelGO);
    }
}