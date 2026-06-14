// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SaberSense.GUI.Framework.Core;

public class UIMultiComboBox : UIDropdownBase
{
    private HashSet<int> _selectedIndices = [];
    private bool _isUpdating;

    public IReadOnlyCollection<int> SelectedIndices => _selectedIndices;

    private Action<HashSet<int>>? _onSelectionChanged;

    private readonly Dictionary<int, List<GameObject>> _dependentElements = new();
    private readonly List<GameObject> _showWhenAny = [];

    public UIMultiComboBox(string name = "MultiComboBox", RectTransform? canvasRoot = null)
    : base(name, canvasRoot, UITheme.FontNormal, "-", "MBackdrop", "MPopup")
    {
    }

    public UIMultiComboBox SetOptions(List<string> options)
    {
        SetOptionsCore(options);
        UpdateButtonLabel();
        return this;
    }

    public UIMultiComboBox SetSelected(IEnumerable<int> indices)
    {
        if (_isUpdating) return this;
        _isUpdating = true;
        try
        {
            _selectedIndices = [.. indices];
            UpdateVisuals();
            UpdateButtonLabel();
            SyncDependentVisibility();
            UICallbackGuard.Invoke(_onSelectionChanged!, _selectedIndices);
        }
        finally { _isUpdating = false; }
        return this;
    }

    public UIMultiComboBox OnSelectionChanged(Action<HashSet<int>> callback)
    {
        _onSelectionChanged += callback;
        return this;
    }

    public UIMultiComboBox ControlsVisibility(int index, GameObject target)
    {
        if (!_dependentElements.ContainsKey(index))
        _dependentElements[index] = [];
        _dependentElements[index].Add(target);
        target.SetActive(_selectedIndices.Contains(index));
        return this;
    }

    public UIMultiComboBox ShowWhenAnySelected(GameObject target)
    {
        _showWhenAny.Add(target);
        target.SetActive(_selectedIndices.Count is > 0);
        return this;
    }

    protected override void PositionPopup(float dropH)
    {
        PopupRect.sizeDelta = new Vector2(PopupRect.sizeDelta.x, dropH);
        RepositionPopup();
    }

    protected override void OnOpened()
    {
        for (int i = 0; i < ItemBackgrounds.Count; i++)
        ItemBackgrounds[i].color = UITheme.SurfaceLight;

        UpdateVisuals();

        var tracker = PopupGO.GetComponent<PopupTracker>();
        if (tracker == null)
        {
            tracker = PopupGO.AddComponent<PopupTracker>();
            tracker.Owner = this;
        }
        tracker.enabled = true;
    }

    protected override void OnClosed()
    {
        var tracker = PopupGO.GetComponent<PopupTracker>();
        if (tracker != null) tracker.enabled = false;
    }

    protected override void OnItemClicked(int index) => ToggleItem(index);

    private void RepositionPopup()
    {
        if (CanvasRoot == null) return;

        float dropH = PopupRect.sizeDelta.y;
        UIPopupHelper.PositionDropdown(RectTransform, CanvasRoot, PopupRect, dropH, forceUp: false);
    }

    private sealed class PopupTracker : MonoBehaviour
    {
        internal UIMultiComboBox? Owner;
        private void LateUpdate() => Owner?.RepositionPopup();
    }

    private void ToggleItem(int idx)
    {
        if (_selectedIndices.Contains(idx))
        _selectedIndices.Remove(idx);
        else
        _selectedIndices.Add(idx);

        UpdateVisuals();
        UpdateButtonLabel();
        SyncDependentVisibility();
        UICallbackGuard.Invoke(_onSelectionChanged!, _selectedIndices);
    }

    private void SyncDependentVisibility()
    {
        foreach (var kvp in _dependentElements)
        {
            bool active = _selectedIndices.Contains(kvp.Key);
            foreach (var go in kvp.Value)
            if (go != null) go.SetActive(active);
        }
        bool anyActive = _selectedIndices.Count is > 0;
        foreach (var go in _showWhenAny)
        if (go != null) go.SetActive(anyActive);
    }

    private void UpdateButtonLabel()
    {
        if (_selectedIndices.Count is 0)
        {
            ButtonLabel.SetText("-");
            return;
        }

        var names = _selectedIndices
        .Where(i => i >= 0 && i < Options.Count)
        .Select(i => Options[i]);
        string text = string.Join(", ", names);

        if (text.Length > 25)
        text = text[..22] + "...";

        ButtonLabel.SetText(text);
    }

    private void UpdateVisuals()
    {
        var container = ItemContainer;
        if (container == null) return;

        for (int i = 0; i < Options.Count; i++)
        {
            if (i >= container.childCount) break;

            var child = container.GetChild(i);
            var txt = child.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (txt != null)
            {
                bool sel = _selectedIndices.Contains(i);
                txt.color = sel ? UITheme.Accent : UITheme.TextSecondary;
                txt.fontStyle = sel ? TMPro.FontStyles.Bold : TMPro.FontStyles.Normal;
            }
        }
    }
}