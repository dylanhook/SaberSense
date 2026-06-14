// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Menu.Tabs;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Menu;

internal sealed class NavBarBuilder
{
    private VectorIcon[] _navIcons = [];
    private UIImage[] _navBgs = [];
    private UIImage[] _navCellBorderBlack = [];
    private UIImage[] _navCellBorderGray = [];
    private UIImage[] _navCellTopBlack = [];
    private UIImage[] _navCellTopGray = [];
    private UIImage[] _navCellBotGray = [];
    private UIImage[] _navCellBotBlack = [];

    private int _activeTab;
    private GameObject[]? _tabs;

    public void SetTabs(GameObject[] tabs) => _tabs = tabs;

    public int ActiveTab => _activeTab;

    public void Build(RectTransform parent, IReadOnlyList<IMenuTab> tabs)
    {
        int tabCount = tabs.Count;
        _navIcons = new VectorIcon[tabCount];
        _navBgs = new UIImage[tabCount];
        _navCellBorderBlack = new UIImage[tabCount];
        _navCellBorderGray = new UIImage[tabCount];
        _navCellTopBlack = new UIImage[tabCount];
        _navCellTopGray = new UIImage[tabCount];
        _navCellBotGray = new UIImage[tabCount];
        _navCellBotBlack = new UIImage[tabCount];

        var navOuter = new HBox("NavOuter").SetParent(parent).SetAlignment(TextAnchor.UpperLeft);
        navOuter.SetSpacing(0f).AddLayoutElement(minWidth: UITheme.NavWidth, preferredWidth: UITheme.NavWidth, flexibleWidth: 0, flexibleHeight: 1);

        var navCol = new VBox("NavCol").SetParent(navOuter.RectTransform).SetAlignment(TextAnchor.UpperCenter);
        navCol.SetPadding(0, 0, UITheme.PanelPad, 0).SetSpacing(0f).AddLayoutElement(minWidth: UITheme.NavWidth, preferredWidth: UITheme.NavWidth, flexibleWidth: 0, flexibleHeight: 1);
        UnityEngine.Object.Destroy(navCol.GameObject.GetComponent<ContentSizeFitter>());

        var navBgImg = new UIImage("NavBg").SetColor(UITheme.SurfaceDark);
        navBgImg.RectTransform.SetParent(navCol.RectTransform, false);
        navBgImg.SetAnchors(Vector2.zero, Vector2.one);
        navBgImg.RectTransform.SetAsFirstSibling();

        for (int i = 0; i < tabCount; i++)
        {
            int tabIdx = i;
            var cellGO = new GameObject("NavCell" + i);
            cellGO.transform.SetParent(navCol.RectTransform, false);
            cellGO.AddComponent<RectTransform>();
            var le = cellGO.AddComponent<LayoutElement>();
            le.minHeight = UITheme.NavCellHeight; le.preferredHeight = UITheme.NavCellHeight; le.flexibleHeight = 0;

            var bg = new UIImage("NBg" + i).SetColor(new Color(0, 0, 0, 0));
            bg.RectTransform.SetParent(cellGO.transform, false);
            bg.SetAnchors(Vector2.zero, Vector2.one);
            bg.ImageComponent.raycastTarget = true;
            _navBgs[i] = bg;

            var icon = VectorIcon.Create("NI" + i, tabs[i].IconPath);
            icon.RectTransform.SetParent(cellGO.transform, false);
            icon.SetAnchors(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            icon.RectTransform.sizeDelta = new Vector2(UITheme.NavIconSize, UITheme.NavIconSize);
            icon.GraphicComponent.raycastTarget = false;
            icon.SetColor(UITheme.NavIcon);
            _navIcons[i] = icon;

            var vb = new UIImage("VB" + i).SetColor(UITheme.BorderBlack);
            vb.RectTransform.SetParent(cellGO.transform, false);
            vb.RectTransform.anchorMin = new Vector2(1, 0); vb.RectTransform.anchorMax = new Vector2(1, 1);
            vb.RectTransform.pivot = new Vector2(1, 0.5f); vb.RectTransform.sizeDelta = new Vector2(UITheme.BorderThickness, 0);
            vb.ImageComponent.raycastTarget = false;
            _navCellBorderBlack[i] = vb;

            var vg = new UIImage("VG" + i).SetColor(UITheme.Divider);
            vg.RectTransform.SetParent(cellGO.transform, false);
            vg.RectTransform.anchorMin = new Vector2(1, 0); vg.RectTransform.anchorMax = new Vector2(1, 1);
            vg.RectTransform.pivot = new Vector2(0, 0.5f); vg.RectTransform.sizeDelta = new Vector2(UITheme.BorderThickness, 0);
            vg.RectTransform.anchoredPosition = Vector2.zero;
            vg.ImageComponent.raycastTarget = false;
            _navCellBorderGray[i] = vg;

            var tb = new UIImage("TB" + i).SetColor(UITheme.BorderBlack);
            tb.RectTransform.SetParent(cellGO.transform, false);
            tb.RectTransform.anchorMin = new Vector2(0, 1); tb.RectTransform.anchorMax = new Vector2(1, 1);
            tb.RectTransform.pivot = new Vector2(0.5f, 1); tb.RectTransform.sizeDelta = new Vector2(0, UITheme.BorderThickness);
            tb.ImageComponent.raycastTarget = false; tb.GameObject.SetActive(false);
            _navCellTopBlack[i] = tb;

            var tg = new UIImage("TG" + i).SetColor(UITheme.Divider);
            tg.RectTransform.SetParent(cellGO.transform, false);
            tg.RectTransform.anchorMin = new Vector2(0, 1); tg.RectTransform.anchorMax = new Vector2(1, 1);
            tg.RectTransform.pivot = new Vector2(0.5f, 1);
            tg.RectTransform.sizeDelta = new Vector2(0.3f, 0.15f);
            tg.RectTransform.anchoredPosition = new Vector2(0, -0.15f);
            tg.ImageComponent.raycastTarget = false; tg.GameObject.SetActive(false);
            _navCellTopGray[i] = tg;

            var bg2 = new UIImage("BG" + i).SetColor(UITheme.Divider);
            bg2.RectTransform.SetParent(cellGO.transform, false);
            bg2.RectTransform.anchorMin = new Vector2(0, 0); bg2.RectTransform.anchorMax = new Vector2(1, 0);
            bg2.RectTransform.pivot = new Vector2(0.5f, 0); bg2.RectTransform.sizeDelta = new Vector2(0.3f, 0.15f);
            bg2.ImageComponent.raycastTarget = false; bg2.GameObject.SetActive(false);
            _navCellBotGray[i] = bg2;

            var bb = new UIImage("BB" + i).SetColor(UITheme.BorderBlack);
            bb.RectTransform.SetParent(cellGO.transform, false);
            bb.RectTransform.anchorMin = new Vector2(0, 0); bb.RectTransform.anchorMax = new Vector2(1, 0);
            bb.RectTransform.pivot = new Vector2(0.5f, 0); bb.RectTransform.sizeDelta = new Vector2(0, UITheme.BorderThickness);
            bb.RectTransform.anchoredPosition = new Vector2(0, 0.15f);
            bb.ImageComponent.raycastTarget = false; bb.GameObject.SetActive(false);
            _navCellBotBlack[i] = bb;

            var handler = bg.GameObject.AddComponent<PointerEventHandler>();
            handler.OnClick = () => SwitchTab(tabIdx);
            handler.OnEnter = () => { if (_activeTab != tabIdx) _navIcons[tabIdx].GraphicComponent.color = UITheme.NavIconHover; };
            handler.OnExit = () => { if (_activeTab != tabIdx) _navIcons[tabIdx].GraphicComponent.color = UITheme.NavIcon; };
        }

        var bottomFill = new GameObject("NavBottom");
        bottomFill.transform.SetParent(navCol.RectTransform, false);
        bottomFill.AddComponent<RectTransform>();
        var bfLe = bottomFill.AddComponent<LayoutElement>();
        bfLe.flexibleHeight = 1;
        var bfBg = new UIImage("BFBg").SetColor(UITheme.SurfaceDark);
        bfBg.RectTransform.SetParent(bottomFill.transform, false);
        bfBg.SetAnchors(Vector2.zero, Vector2.one); bfBg.ImageComponent.raycastTarget = false;

        var bfBorderBlack = new UIImage("BFB").SetColor(UITheme.BorderBlack);
        bfBorderBlack.RectTransform.SetParent(bottomFill.transform, false);
        bfBorderBlack.RectTransform.anchorMin = new Vector2(1, 0); bfBorderBlack.RectTransform.anchorMax = new Vector2(1, 1);
        bfBorderBlack.RectTransform.pivot = new Vector2(1, 0.5f); bfBorderBlack.RectTransform.sizeDelta = new Vector2(UITheme.BorderThickness, 0);
        bfBorderBlack.ImageComponent.raycastTarget = false;

        var bfBorderGray = new UIImage("BFG").SetColor(UITheme.Divider);
        bfBorderGray.RectTransform.SetParent(bottomFill.transform, false);
        bfBorderGray.RectTransform.anchorMin = new Vector2(1, 0); bfBorderGray.RectTransform.anchorMax = new Vector2(1, 1);
        bfBorderGray.RectTransform.pivot = new Vector2(0, 0.5f); bfBorderGray.RectTransform.sizeDelta = new Vector2(UITheme.BorderThickness, 0);
        bfBorderGray.ImageComponent.raycastTarget = false;
    }

    public void SwitchTab(int index)
    {
        _activeTab = index;
        if (_tabs != null)
        for (int i = 0; i < _tabs.Length; i++) _tabs[i].SetActive(i == index);
        for (int i = 0; i < _navIcons.Length; i++)
        {
            bool on = i == index;
            _navBgs[i].SetColor(on ? UITheme.Surface : UITheme.SurfaceDark);
            _navIcons[i].GraphicComponent.color = on ? UITheme.NavIconActive : UITheme.NavIcon;
            _navCellBorderBlack[i].GameObject.SetActive(!on);
            _navCellBorderGray[i].GameObject.SetActive(!on);
            _navCellTopBlack[i].GameObject.SetActive(on);
            _navCellTopGray[i].GameObject.SetActive(on);
            _navCellBotGray[i].GameObject.SetActive(on);
            _navCellBotBlack[i].GameObject.SetActive(on);
        }
    }

    public static void BuildRainbowBar(RectTransform parent)
    {
        var rbGO = new GameObject("RainbowBar");
        rbGO.transform.SetParent(parent, false);
        var rbRect = rbGO.AddComponent<RectTransform>();
        rbRect.anchorMin = new Vector2(0, 1); rbRect.anchorMax = new Vector2(1, 1);
        rbRect.pivot = new Vector2(0.5f, 1); rbRect.sizeDelta = new Vector2(0, 0.6f);
        rbRect.anchoredPosition = Vector2.zero;

        void Grad(string n, float yMin, float yMax, Color32 l, Color32 r)
        {
            var imgL = new GameObject(n + "L").AddComponent<Image>();
            imgL.material = UIMaterials.NoBloomMaterial;
            imgL.rectTransform.SetParent(rbRect, false);
            imgL.rectTransform.anchorMin = new Vector2(0, yMin); imgL.rectTransform.anchorMax = new Vector2(0.5f, yMax);
            imgL.rectTransform.sizeDelta = Vector2.zero;
            var gradL = imgL.gameObject.AddComponent<UIHorizontalGradient>();
            gradL.ColorLeft = l;
            gradL.ColorRight = r;

            var imgR = new GameObject(n + "R").AddComponent<Image>();
            imgR.material = UIMaterials.NoBloomMaterial;
            imgR.rectTransform.SetParent(rbRect, false);
            imgR.rectTransform.anchorMin = new Vector2(0.5f, yMin); imgR.rectTransform.anchorMax = new Vector2(1, yMax);
            imgR.rectTransform.sizeDelta = Vector2.zero;
            var gradR = imgR.gameObject.AddComponent<UIHorizontalGradient>();
            gradR.ColorLeft = r;
            gradR.ColorRight = new Color32(204, 227, 53, 255);
        }

        Grad("L1", 0.5f, 1f, new Color32(55, 177, 218, 255), new Color32(202, 70, 205, 255));
        Grad("L2", 0f, 0.5f, new Color32(55, 177, 218, 255), new Color32(202, 70, 205, 255));
    }
}