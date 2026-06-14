// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UIComboBox : UIDropdownBase
{
    private int _selectedIndex = 0;
    private bool _openUpward;
    private Action<int, string>? _onSelect;

    public UIComboBox(string name = "ComboBox", RectTransform? canvasRoot = null)
    : base(name, canvasRoot, UITheme.FontSmall, "Select...", "CBackdrop", "CPopup")
    {
    }

    public UIComboBox SetOptions(List<string> options)
    {
        SetOptionsCore(options);
        if (Options.Count is > 0 && _selectedIndex < Options.Count)
        ButtonLabel.SetText(Options[_selectedIndex]);
        return this;
    }

    public UIComboBox SetSelected(int index)
    {
        if (Options.Count is 0) return this;

        _selectedIndex = Mathf.Clamp(index, 0, Options.Count - 1);
        ButtonLabel.SetText(Options[_selectedIndex]);
        return this;
    }

    public UIComboBox OnSelect(Action<int, string> callback)
    {
        _onSelect = callback;
        return this;
    }

    public UIComboBox SetOpenUpward(bool upward = true)
    {
        _openUpward = upward;
        return this;
    }

    protected override void OnOpening()
    {
        var container = ItemContainer;
        if (container == null) return;

        for (int j = 0; j < container.childCount; j++)
        {
            int itemIndex = j;
            bool isSelected = (itemIndex == _selectedIndex);
            var child = container.GetChild(j);

            var bgItem = child.GetComponent<Image>();
            if (bgItem != null) bgItem.color = UITheme.SurfaceLight;

            var txt = child.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (txt != null)
            {
                txt.color = isSelected ? UITheme.Accent : UITheme.TextSecondary;
                txt.fontStyle = isSelected ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
            }
        }
    }

    protected override void PositionPopup(float dropH)
    => UIPopupHelper.PositionDropdown(RectTransform, CanvasRoot!, PopupRect, dropH, _openUpward);

    protected override void OnItemClicked(int index)
    {
        _selectedIndex = index;
        string opt = Options[index];
        ButtonLabel.SetText(opt);
        UICallbackGuard.Invoke(_onSelect!, index, opt);
        Close();
    }
}