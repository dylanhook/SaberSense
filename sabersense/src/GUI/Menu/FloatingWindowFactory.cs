// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using HMUI;
using SaberSense.GUI.Framework.Core;
using UnityEngine;
using UnityEngine.UI;
using VRUIControls;

namespace SaberSense.GUI.Menu;

internal static class FloatingWindowFactory
{
    internal enum Side { Left, Right }

    public static (GameObject windowGO, RectTransform rect, UIImage bg) Build(
    RectTransform parent, PhysicsRaycasterWithCache physicsRaycaster,
    Side side, string windowName, string elementPrefix)
    {
        bool right = side == Side.Right;

        var windowGO = new GameObject(windowName);
        var rect = windowGO.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = right ? new Vector2(1, 0) : new Vector2(0, 0);
        rect.anchorMax = right ? new Vector2(1, 1) : new Vector2(0, 1);
        rect.pivot = right ? new Vector2(0, 0.5f) : new Vector2(1, 0.5f);
        rect.anchoredPosition = new Vector2(right ? 3 : -3, 0);
        rect.sizeDelta = new Vector2(50, 0);

        var canvas = windowGO.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = UIZLayer.MainCanvas;
        if (physicsRaycaster != null)
        {
            var vrgr = windowGO.AddComponent<VRGraphicRaycaster>();
            VRRaycasterHelper.SetPhysicsRaycaster(vrgr, physicsRaycaster);
        }
        windowGO.AddComponent<CurvedCanvasSettings>().SetRadius(0f);
        rect.localEulerAngles = new Vector3(0, right ? 20f : -20f, 0);

        var blockerGO = new GameObject(elementPrefix + "RaycastBlocker");
        blockerGO.transform.SetParent(rect, false);
        var blockerR = blockerGO.AddComponent<RectTransform>();
        blockerR.anchorMin = Vector2.zero;
        blockerR.anchorMax = Vector2.one;
        blockerR.sizeDelta = Vector2.zero;
        var blockerImg = blockerGO.AddComponent<Image>();
        blockerImg.color = new Color(0, 0, 0, 0);
        blockerImg.raycastTarget = true;

        float inset = UILayoutFactory.BuildKaabaOutline(rect, elementPrefix + "Border");

        var bg = new UIImage(elementPrefix + "Bg").SetColor(UITheme.Surface);
        bg.RectTransform.SetParent(rect, false);
        bg.SetAnchors(Vector2.zero, Vector2.one);
        bg.RectTransform.offsetMin = new Vector2(inset, inset);
        bg.RectTransform.offsetMax = new Vector2(-inset, -inset);
        NavBarBuilder.BuildRainbowBar(bg.RectTransform);

        return (windowGO, rect, bg);
    }
}