// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UICollapsibleSection : UIElement
{
    private const string ChevronExpanded = "▼";
    private const string ChevronCollapsed = "▶";
    private const float HeaderHeight = 4f;
    private const float ChevronWidth = 2.5f;
    private const float ContentLeftPad = 3;
    private const float GuideLineWidth = 0.2f;
    private static readonly Color GuideLineColor = UITheme.Divider;

    private static readonly HashSet<string> _expandedSections = [];

    public RectTransform Content { get; private set; }

    public bool IsExpanded { get; private set; }

    private readonly string _title;
    private readonly UILabel _chevron;
    private readonly UILabel _titleLabel;
    private readonly GameObject _contentGO;
    private readonly GameObject _guideLineGO;

    public UICollapsibleSection(string title) : base(title + "_Section")
    {
        _title = title;

        var rootLayout = GameObject.AddComponent<VerticalLayoutGroup>();
        rootLayout.spacing = 0f;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        var rootFitter = GameObject.AddComponent<ContentSizeFitter>();
        rootFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var header = new HBox(title + "_Header");
        header.RectTransform.SetParent(RectTransform, false);
        header.SetSpacing(UITheme.RowInnerSpacing).SetPadding(0, 0, 0, 0)
        .AddLayoutElement(preferredHeight: HeaderHeight, flexibleWidth: 1);
        header.LayoutGroup.childAlignment = TextAnchor.LowerLeft;
        header.LayoutGroup.childForceExpandHeight = false;

        _chevron = new UILabel(title + "_Chev", ChevronCollapsed)
        .SetFontSize(UITheme.FontSmall)
        .SetColor(UITheme.TextMuted)
        .SetAlignment(TMPro.TextAlignmentOptions.Left);
        _chevron.RectTransform.SetParent(header.RectTransform, false);
        _chevron.AddLayoutElement(preferredWidth: ChevronWidth, preferredHeight: HeaderHeight);

        _titleLabel = new UILabel(title + "_L", title)
        .SetFontSize(UITheme.FontSmall)
        .SetColor(UITheme.Accent)
        .SetAlignment(TMPro.TextAlignmentOptions.Left);
        _titleLabel.RectTransform.SetParent(header.RectTransform, false);
        _titleLabel.AddLayoutElement(flexibleWidth: 1, preferredHeight: HeaderHeight);
        UITheme.TrackAccent(_titleLabel.TextComponent);

        var hitArea = header.GameObject.AddComponent<Image>();
        hitArea.color = new Color(0, 0, 0, 0);
        hitArea.material = UIMaterials.NoBloomMaterial;
        hitArea.raycastTarget = true;

        var handler = header.GameObject.AddComponent<PointerEventHandler>();
        handler.OnClick = Toggle;
        handler.OnEnter = () => _titleLabel.SetColor(UITheme.AccentHover);
        handler.OnExit = () => _titleLabel.SetColor(UITheme.Accent);

        var contentWrapper = new GameObject(title + "_Wrapper");
        contentWrapper.transform.SetParent(RectTransform, false);
        var wrapperRect = contentWrapper.AddComponent<RectTransform>();
        var wrapperLayout = contentWrapper.AddComponent<HorizontalLayoutGroup>();
        wrapperLayout.spacing = 0f;
        wrapperLayout.childControlWidth = true;
        wrapperLayout.childControlHeight = true;
        wrapperLayout.childForceExpandWidth = false;
        wrapperLayout.childForceExpandHeight = true;
        wrapperLayout.padding = new RectOffset(0, 0, 0, 0);
        var wrapperFitter = contentWrapper.AddComponent<ContentSizeFitter>();
        wrapperFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var guideLine = new UIImage(title + "_Guide")
        .SetColor(GuideLineColor);
        guideLine.RectTransform.SetParent(wrapperRect, false);
        guideLine.ImageComponent.raycastTarget = false;
        guideLine.AddLayoutElement(preferredWidth: GuideLineWidth, flexibleHeight: 1);
        _guideLineGO = guideLine.GameObject;

        var spacer = new GameObject(title + "_Spacer");
        spacer.transform.SetParent(wrapperRect, false);
        spacer.AddComponent<RectTransform>();
        spacer.AddComponent<LayoutElement>().preferredWidth = ContentLeftPad - GuideLineWidth;

        var content = new VBox(title + "_Content");
        content.RectTransform.SetParent(wrapperRect, false);
        content.SetSpacing(UITheme.ColumnGap).SetPadding(0, 0, 0, 0);
        content.LayoutGroup.childForceExpandWidth = true;
        content.LayoutGroup.childForceExpandHeight = false;
        content.AddLayoutElement(flexibleWidth: 1);

        var contentFitter = content.GameObject.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Content = content.RectTransform;
        _contentGO = contentWrapper;

        bool remembered = _expandedSections.Contains(title);
        IsExpanded = remembered;
        _contentGO.SetActive(remembered);
        _guideLineGO.SetActive(remembered);
        _chevron.SetText(remembered ? ChevronExpanded : ChevronCollapsed);
    }

    public void Toggle()
    {
        SetExpanded(!IsExpanded);
    }

    public void SetExpanded(bool expanded)
    {
        IsExpanded = expanded;
        _contentGO.SetActive(expanded);
        _guideLineGO.SetActive(expanded);
        _chevron.SetText(expanded ? ChevronExpanded : ChevronCollapsed);

        if (expanded)
        LayoutRebuilder.ForceRebuildLayoutImmediate(RectTransform);

        if (expanded)
        _expandedSections.Add(_title);
        else
        _expandedSections.Remove(_title);
    }

    public static void ResetExpandedState() => _expandedSections.Clear();
}