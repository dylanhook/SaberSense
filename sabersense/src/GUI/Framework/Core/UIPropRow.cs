// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UIPropRow : UIElement
{
    public UILabel Label { get; private set; }
    public RectTransform ControlArea { get; private set; }

    public UIPropRow(string label, UIElement control, float controlWidth = -1) : base("PropRow")
    {
        var layout = GameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.spacing = 2f;
        layout.padding = new RectOffset(1, 1, 0, 0);

        Label = new UILabel("Label", label)
        .SetFontSize(UITheme.FontNormal)
        .SetColor(UITheme.TextSecondary)
        .SetAlignment(TMPro.TextAlignmentOptions.Left);
        Label.RectTransform.SetParent(RectTransform, false);
        Label.AddLayoutElement(flexibleWidth: 1);

        var controlContainer = new GameObject("ControlArea");
        controlContainer.transform.SetParent(RectTransform, false);
        ControlArea = controlContainer.AddComponent<RectTransform>();
        var controlLE = controlContainer.AddComponent<LayoutElement>();

        if (controlWidth > 0)
        controlLE.preferredWidth = controlWidth;
        else
        controlLE.flexibleWidth = 1;

        control.RectTransform.SetParent(ControlArea, false);
        control.SetAnchors(Vector2.zero, Vector2.one);
    }
}