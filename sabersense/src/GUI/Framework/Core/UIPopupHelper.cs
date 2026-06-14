// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.UI;
using VRUIControls;

namespace SaberSense.GUI.Framework.Core;

public static class UIPopupHelper
{
    public static GameObject CreateBackdrop(string name, RectTransform canvasRoot, RectTransform parent, System.Action onClose, float alpha = 0f)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.material = UIMaterials.NoBloomMaterial;
        img.color = new Color(0, 0, 0, alpha);
        img.raycastTarget = true;
        SetupPopupCanvas(go, canvasRoot, UIZLayer.PopupBackdrop);
        go.AddComponent<PointerEventHandler>().OnClick = onClose;
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        return go;
    }

    public static GameObject CreatePopupContainer(
    string name, RectTransform canvasRoot, RectTransform parent,
    out RectTransform popupRect, out VerticalLayoutGroup layout)
    {
        var go = new GameObject(name);
        popupRect = go.AddComponent<RectTransform>();

        SetupPopupCanvas(go, canvasRoot, UIZLayer.Popup);

        var bgI = go.AddComponent<Image>();
        bgI.material = UIMaterials.NoBloomMaterial;
        bgI.color = UITheme.Border;
        bgI.raycastTarget = true;

        var containerGO = new GameObject("ItemContainer");
        containerGO.transform.SetParent(popupRect, false);
        var ctrRect = containerGO.AddComponent<RectTransform>();
        ctrRect.anchorMin = Vector2.zero;
        ctrRect.anchorMax = Vector2.one;
        ctrRect.offsetMin = new Vector2(0.166f, 0.166f);
        ctrRect.offsetMax = new Vector2(-0.166f, -0.166f);

        layout = containerGO.AddComponent<VerticalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 0;
        layout.padding = new RectOffset(0, 0, 0, 0);

        go.SetActive(false);
        go.transform.SetParent(parent, false);
        return go;
    }

    public static void SetupPopupCanvas(GameObject go, RectTransform? canvasRoot, int sortingOrder)
    {
        var subCanvas = go.AddComponent<Canvas>();
        subCanvas.overrideSorting = true;
        subCanvas.sortingOrder = sortingOrder;

        var parentVrgr = canvasRoot != null ? canvasRoot.GetComponent<VRGraphicRaycaster>() : null;
        if (parentVrgr != null)
        {
            var vrgr = go.AddComponent<VRGraphicRaycaster>();
            VRRaycasterHelper.CopyPhysicsRaycaster(parentVrgr, vrgr);
        }
        else
        {
            go.AddComponent<GraphicRaycaster>();
        }
    }

    public static void CreateContextButton(string text, Transform parent, System.Action onClick)
    {
        var go = new GameObject(text + "Btn");
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();

        var bg = go.AddComponent<Image>();
        bg.material = UIMaterials.NoBloomMaterial;
        bg.color = UITheme.SurfaceLight;
        bg.raycastTarget = true;

        var label = new UILabel(text + "L", text)
        .SetFontSize(UITheme.FontSmall)
        .SetColor(UITheme.TextSecondary)
        .SetAlignment(TMPro.TextAlignmentOptions.Left);
        label.RectTransform.SetParent(go.transform, false);
        label.SetAnchors(Vector2.zero, Vector2.one);
        label.RectTransform.offsetMin = new Vector2(1.5f, 0);
        label.RectTransform.offsetMax = Vector2.zero;

        var handler = go.AddComponent<PointerEventHandler>();
        handler.OnEnter = () => bg.color = UITheme.SurfacePressed;
        handler.OnExit = () => bg.color = UITheme.SurfaceLight;
        handler.OnClick = () => onClick?.Invoke();
    }

    public static void PositionDropdown(RectTransform buttonRect, RectTransform canvasRoot,
    RectTransform popupRect, float dropH, bool forceUp)
    {
        var rootRect = canvasRoot.rect;
        float pivotOffsetX = (0.5f - canvasRoot.pivot.x) * rootRect.width;
        float pivotOffsetY = (0.5f - canvasRoot.pivot.y) * rootRect.height;

        var corners = new Vector3[4];
        buttonRect.GetWorldCorners(corners);
        Vector3 rawBL = canvasRoot.InverseTransformPoint(corners[0]);
        Vector3 rawBR = canvasRoot.InverseTransformPoint(corners[3]);
        Vector2 localBL = new(rawBL.x - pivotOffsetX, rawBL.y - pivotOffsetY);
        float width = rawBR.x - rawBL.x;

        popupRect.sizeDelta = new Vector2(width, dropH);

        float canvasBottom = -rootRect.height * canvasRoot.pivot.y;
        bool openUp = forceUp || (localBL.y - 0.5f - dropH) < canvasBottom;

        if (openUp)
        {
            Vector3 rawTL = canvasRoot.InverseTransformPoint(corners[1]);
            Vector2 localTL = new(rawTL.x - pivotOffsetX, rawTL.y - pivotOffsetY);
            popupRect.pivot = new Vector2(0f, 0f);
            popupRect.anchoredPosition = new Vector2(localBL.x, localTL.y + 0.5f);
        }
        else
        {
            popupRect.pivot = new Vector2(0f, 1f);
            popupRect.anchoredPosition = new Vector2(localBL.x, localBL.y - 0.5f);
        }
    }
}