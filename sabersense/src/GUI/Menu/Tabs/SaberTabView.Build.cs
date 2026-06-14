// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Core.Utilities;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Menu.Controllers;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Menu.Tabs;

internal sealed partial class SaberTabView
{
    private void BuildRightColumn(RectTransform parent)
    {
        var rightCol = new VBox("RightCol").SetParent(parent).SetAlignment(TextAnchor.UpperLeft);
        UnityEngine.Object.Destroy(rightCol.GameObject.GetComponent<ContentSizeFitter>());
        rightCol.LayoutGroup.childForceExpandHeight = false;
        rightCol.SetPadding(0, 0, 0, 0).SetSpacing(UITheme.GroupGap)
        .AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var optionsGroup = new UIGroupBox("Saber options");
        optionsGroup.SetParent(rightCol.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        _favoriteButton = new BaseButton("Pin saber").SetParent(optionsGroup.Content).AddLayoutElement(preferredHeight: UITheme.ButtonRowHeight, flexibleWidth: 1);
        _favoriteButton.OnClick = () => ErrorBoundary.FireAndForget(TogglePinAsync(!(selectionController.SelectedEntry?.IsPinned ?? false)), _log, nameof(TogglePinAsync));

        var sortCombo = new UIComboBox("SortCombo", _canvasRoot);
        sortCombo.SetOptions([.. SaberSense.GUI.Menu.UIStrings.SortModes]);
        sortCombo.BindInt(settings, c => c.Editor.SortMode, _bindingScope, async (idx) =>
        {
            catalogController.SortMode = (SortMode)idx;
            await ShowSabersAsync(true);
        });
        UILayoutFactory.DropdownRow("Sort by", sortCombo, optionsGroup.Content);

        _grabSaberCombo = new UIMultiComboBox("GrabSaberCombo", _canvasRoot);
        _grabSaberCombo.SetOptions([.. SaberSense.GUI.Menu.UIStrings.GrabOptions]);
        _grabSaberCombo.BindList(settings, c => c.GrabSelections, _bindingScope, sel =>
        {
            if (editor is null) return;
            bool left = sel.Contains(0);
            bool right = sel.Contains(1);
            editor.SetGrab(left, right);
        });
        UILayoutFactory.DropdownRow("Grab saber", _grabSaberCombo, optionsGroup.Content);
        if (editor is not null && settings?.GrabSelections is not null)
        {
            HashSet<int> initGrab = [.. settings.GrabSelections];
            editor.SetGrab(initGrab.Contains(0), initGrab.Contains(1));
        }

        BuildTransformControls(optionsGroup.Content);

        GameObject? goSmoothSlider = null;
        var smoothToggle = new UIToggle().Bind(settings!, c => c.SmoothingEnabled, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Motion smoothing", smoothToggle, optionsGroup.Content);

        var smoothSlider = new UISlider().SetRange(0, 100).Bind(settings!, c => c.SmoothingStrength, scope: _bindingScope);
        goSmoothSlider = UILayoutFactory.SliderRow("Smoothing strength", smoothSlider, optionsGroup.Content);
        smoothToggle.ControlsVisibility(goSmoothSlider);

        GameObject? goBlurSlider = null;
        var blurToggle = new UIToggle().Bind(settings!, c => c.MotionBlur.Enabled, scope: _bindingScope);
        UILayoutFactory.CheckboxRow("Motion blur", blurToggle, optionsGroup.Content, experimental: true);

        var blurSlider = new UISlider().SetRange(0, 100).Bind(settings!, c => c.MotionBlur.Strength, scope: _bindingScope);
        goBlurSlider = UILayoutFactory.SliderRow("Blur strength", blurSlider, optionsGroup.Content);
        blurToggle.ControlsVisibility(goBlurSlider);

        var previewToggle = new UIToggle().Bind(settings!, c => c.Editor.PreviewSaber, _bindingScope, val =>
        {
            _previewWindowGO?.SetActive(val);
        });
        _previewWindowGO?.SetActive(settings?.Editor?.PreviewSaber ?? true);
        UILayoutFactory.CheckboxRow("Preview saber", previewToggle, optionsGroup.Content);
    }

    private List<UIListCellData> BuildSortedListItems()
    {
        var sortMode = (SortMode)(settings?.Editor?.SortMode ?? 0);
        catalogController.SortMode = sortMode;
        var sortedPreviews = SaberCatalogController.ApplySort(
        catalog!.EnumeratePreviews(), sortMode);

        List<ISaberListItem> items = [.. sortedPreviews];
        var processed = catalogController.Folders!.Process(items);

        var folderSprite = SaberSense.GUI.Framework.Core.VectorSpriteGenerator.Generate(
        SaberSense.GUI.Framework.Core.IconPaths.Folder, 64);
        var returnSprite = SaberSense.GUI.Framework.Core.VectorSpriteGenerator.Generate(
        SaberSense.GUI.Framework.Core.IconPaths.Return, 64);

        List<UIListCellData> uiItems = [];
        foreach (var item in processed)
        {
            if (item is FolderItem dir)
            {
                var isUp = dir.DisplayName == "<";
                uiItems.Add(new UIListCellData(isUp ? "Back" : dir.DisplayName, isUp ? "Return to parent" : "Directory", isUp ? returnSprite : folderSprite, dir));
            }
            else if (!string.IsNullOrEmpty(item.DisplayName))
            uiItems.Add(new UIListCellData(item.DisplayName, item.CreatorName ?? "", item.CoverImage, item, item.IsPinned));
        }

        if (uiItems.Count is 0) uiItems.Add(new UIListCellData("No sabers found", ""));
        return uiItems;
    }

    private void BuildTransformControls(RectTransform parent)
    {
        HashSet<int> activeTransformSels = [];

        _transformSabersCombo = new UIMultiComboBox("TransformSabersCombo", _canvasRoot);
        _transformSabersCombo.SetOptions([.. SaberSense.GUI.Menu.UIStrings.TransformOptions]);
        _transformSabersCombo.BindList(settings!, c => c.TransformSelections, _bindingScope, sel => { activeTransformSels = sel; });
        UILayoutFactory.DropdownRow("Transform sabers", _transformSabersCombo, parent);

        _saberWidthSlider = new UISlider().SetRange(0, 3).Bind(transformController.State, t => t.Width, _bindingScope, val =>
        {
            if (transformController.IsSyncing) return;
            transformController.SetWidth(selectionController.SelectedEntry!, val);
            previewController.SaberPreview?.RefreshFraming(_activeSaberLength, _activeSaberOffset);
        });
        var goSaberWidth = UILayoutFactory.SliderRow("Saber width", _saberWidthSlider, parent);

        _saberLengthSlider = new UISlider().SetRange(0f, 3f).Bind(transformController.State, t => t.Length, _bindingScope, val =>
        {
            if (transformController.IsSyncing) return;
            _activeSaberLength = val;
            transformController.SetLength(selectionController.SelectedEntry!, val);
            previewController.SaberPreview?.RefreshFraming(val, _activeSaberOffset);
            trailPreviewer?.UpdatePosition();
        });
        var goSaberLength = UILayoutFactory.SliderRow("Saber length", _saberLengthSlider, parent);

        _rotationSlider = new UISlider().SetRange(-180, 180).Bind(transformController.State, t => t.Rotation, _bindingScope, val =>
        {
            if (transformController.IsSyncing) return;
            transformController.SetRotation(selectionController.SelectedEntry!, val);
        });
        var goRotation = UILayoutFactory.SliderRow("Rotation amount", _rotationSlider, parent);

        _offsetSlider = new UISlider().SetRange(-0.5f, 0.5f).Bind(transformController.State, t => t.Offset, _bindingScope, val =>
        {
            if (transformController.IsSyncing) return;
            _activeSaberOffset = val;
            transformController.SetOffset(selectionController.SelectedEntry!, val);
            previewController.SaberPreview?.RefreshFraming(_activeSaberLength, val);
            trailPreviewer?.UpdatePosition();
        });
        var goOffset = UILayoutFactory.SliderRow("Offset amount", _offsetSlider, parent);

        var btnRevertTransform = new BaseButton("Revert").SetParent(parent).AddLayoutElement(preferredHeight: UITheme.ButtonRowHeight, flexibleWidth: 1);
        btnRevertTransform.OnClick = () =>
        {
            var entry = selectionController.SelectedEntry;
            if (activeTransformSels.Contains(0)) transformController.ResetWidth(entry!);
            if (activeTransformSels.Contains(1)) transformController.ResetLength(entry!);
            if (activeTransformSels.Contains(2)) transformController.ResetRotation(entry!);
            if (activeTransformSels.Contains(3)) transformController.ResetOffset(entry!);
            transformController.SyncFromActiveSaber();
            _activeSaberLength = activeTransformSels.Contains(1) ? 1f : _activeSaberLength;
            _activeSaberOffset = activeTransformSels.Contains(3) ? 0f : _activeSaberOffset;
            broker?.Publish(new SaberSense.Core.Messaging.PreviewSaberChangedMsg(entry!));
        };

        _transformSabersCombo.ControlsVisibility(0, goSaberWidth);
        _transformSabersCombo.ControlsVisibility(1, goSaberLength);
        _transformSabersCombo.ControlsVisibility(2, goRotation);
        _transformSabersCombo.ControlsVisibility(3, goOffset);
        _transformSabersCombo.ShowWhenAnySelected(btnRevertTransform.GameObject);
    }
}