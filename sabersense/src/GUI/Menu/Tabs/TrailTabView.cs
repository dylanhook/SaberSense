// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog.Model;
using SaberSense.Configuration;
using SaberSense.Core.Messaging;
using SaberSense.Customization;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Menu.Popups;
using SaberSense.GUI.Menu.Controllers;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.GUI.Menu.Tabs;

internal sealed class TrailTabView(
SaberSelectionController selectionController,
ModSettings settings,
PreviewSession previewSession,
TrailSettingsController trailController,
SaberSense.GUI.TrailVisualizationRenderer trailPreviewer,
SaberSense.Catalog.SaberCatalog catalog,
SaberEditor editor,
IMessageBroker broker) : IMenuTab, ITrailTab
{
    public string Title => "Trail";

    public string IconPath => IconPaths.Trail;

    private readonly BindingScope _bindingScope = new();

    private UISlider _lenSlider = null!;
    private UISlider _widSlider = null!;
    private UISlider _wstepSlider = null!;
    private UISlider _offSlider = null!;
    private UISlider _granSlider = null!;
    private UISlider _freqSlider = null!;
    private UIToggle _flipToggle = null!;
    private UIToggle _clampToggle = null!;

    private RectTransform _canvasRoot = null!;
    private MaterialEditorPopup _materialEditor = null!;
    private ChooseTrailPopup _chooseTrailPopup = null!;

    private IDisposable? _selectionChangedSub;
    private IDisposable? _settingsChangedSub;

    public GameObject Build(MenuTabContext ctx)
    {
        _canvasRoot = ctx.CanvasRoot;
        _materialEditor = ctx.MaterialEditor;
        _chooseTrailPopup = ctx.ChooseTrailPopup;

        var root = UILayoutFactory.TabRoot("TrailTab", ctx.Parent);

        var (columns, leftCol, rightCol) = UILayoutFactory.TabColumns(root);

        var advGroup = new UIGroupBox("Advanced");
        advGroup.SetParent(leftCol.RectTransform).AddLayoutElement(flexibleWidth: 1, preferredHeight: 22);
        _granSlider = new UISlider().SetRange(0, 100).BindInt(settings, c => c.Trail.CurveSmoothnessPercent, _bindingScope, _ => { broker?.Publish(new TrailSettingsChangedMsg()); });
        UILayoutFactory.SliderRow("Curve smoothness", _granSlider, advGroup.Content);
        _freqSlider = new UISlider().SetRange(0, 144).BindInt(settings, c => c.Trail.CaptureSamplesPerSecond, _bindingScope, _ => { broker?.Publish(new TrailSettingsChangedMsg()); });
        _freqSlider.SetLabelFormatter(v => Mathf.RoundToInt(v) == 0 ? "Auto" : Mathf.RoundToInt(v).ToString());
        UILayoutFactory.SliderRow("Trail refresh rate", _freqSlider, advGroup.Content);

        var optionsGroup = new UIGroupBox("Rendering");
        optionsGroup.SetParent(leftCol.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);
        _flipToggle = new UIToggle().Bind(trailController.State, t => t.Flip, _bindingScope, val =>
        {
            if (trailController.IsSyncing) return;
            trailController.SetFlip(val);
        });
        UILayoutFactory.CheckboxRow("Flip trail", _flipToggle, optionsGroup.Content);
        _clampToggle = new UIToggle().Bind(trailController.State, t => t.ClampTexture, _bindingScope,
        val => { if (!trailController.IsSyncing) trailController.SetClampTexture(val); });
        UILayoutFactory.CheckboxRow("Clamp texture", _clampToggle, optionsGroup.Content);
        var localSpaceToggle = new UIToggle().Bind(settings, c => c.Trail.LocalSpaceTrails, _bindingScope, _ => { broker?.Publish(new TrailSettingsChangedMsg()); });
        UILayoutFactory.CheckboxRow("Local space trails", localSpaceToggle, optionsGroup.Content);
        var vertexToggle = new UIToggle().Bind(settings, c => c.Trail.VertexColorOnly, _bindingScope, val => { if (trailPreviewer is not null) trailPreviewer.OnlyColorVertex = val; broker?.Publish(new TrailSettingsChangedMsg()); });
        UILayoutFactory.CheckboxRow("Use vertex color only", vertexToggle, optionsGroup.Content);
        if (trailPreviewer is not null) trailPreviewer.OnlyColorVertex = settings.Trail.VertexColorOnly;
        var trailSortToggle = new UIToggle().Bind(settings, c => c.Trail.OverrideTrailSortOrder, _bindingScope, _ => { broker?.Publish(new TrailSettingsChangedMsg()); });
        UILayoutFactory.CheckboxRow("Override trail sort order", trailSortToggle, optionsGroup.Content, experimental: true);

        var primaryGroup = new UIGroupBox("Dimensions");
        primaryGroup.SetParent(rightCol.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        _lenSlider = new UISlider().SetRange(0, 100).Bind(trailController.State, t => t.LengthPercent, _bindingScope, val =>
        {
            if (trailController.IsSyncing) return;
            trailController.SetLength(val);
            trailPreviewer?.SetLength(previewSession?.FocusedSaber?.GetTrailLayout().Primary?.TrailSettings?.TrailLength ?? 14);
        });
        _lenSlider.SetLabelFormatter(v => $"{Mathf.RoundToInt(v)}%");
        UILayoutFactory.SliderRow("Trail duration", _lenSlider, primaryGroup.Content);
        _widSlider = new UISlider().SetRange(0, 100).Bind(trailController.State, t => t.WidthPercent, _bindingScope, val =>
        {
            if (trailController.IsSyncing) return;
            trailController.SetWidth(val);
            trailPreviewer?.UpdateWidth();
        });
        _widSlider.SetLabelFormatter(v => $"{Mathf.RoundToInt(v)}%");
        UILayoutFactory.SliderRow("Trail width", _widSlider, primaryGroup.Content);
        _wstepSlider = new UISlider().SetRange(0, 1).Bind(trailController.State, t => t.Whitestep, _bindingScope,
        val => { if (!trailController.IsSyncing) trailController.SetWhitestep(val); });
        UILayoutFactory.SliderRow("Whitestep", _wstepSlider, primaryGroup.Content);
        _offSlider = new UISlider().SetRange(-100, 100).Bind(trailController.State, t => t.OffsetPercent, _bindingScope, val =>
        {
            if (trailController.IsSyncing) return;
            trailController.SetOffset(val);
            trailPreviewer?.UpdateWidth();
        });
        _offSlider.SetLabelFormatter(v => $"{Mathf.RoundToInt(v)}%");
        UILayoutFactory.SliderRow("Offset", _offSlider, primaryGroup.Content);

        BuildActionRow(root);
        BindEvents();

        return root.gameObject;
    }

    public void Refresh() => trailController.SyncFromActiveSaber();

    private void BindEvents()
    {
        _selectionChangedSub = broker?.Subscribe<SaberEquippedMsg>(_ => Refresh())!;
        _settingsChangedSub = broker?.Subscribe<SettingsChangedMsg>(_ =>
        {
            Refresh();

            if (trailPreviewer is not null) trailPreviewer.OnlyColorVertex = settings.Trail.VertexColorOnly;
        })!;
    }

    private void BuildActionRow(RectTransform parent)
    {
        var actionRow = new HBox("TrailActions").SetParent(parent);
        actionRow.SetSpacing(UITheme.ColumnGap).AddLayoutElement(minHeight: UITheme.ActionRowHeight, preferredHeight: UITheme.ActionRowHeight, flexibleHeight: 0);
        var revertBtn = new BaseButton("Revert").SetParent(actionRow.RectTransform).AddLayoutElement(flexibleWidth: 1);
        revertBtn.OnClick = () =>
        {
            if (settings is not null)
            {
                settings.Trail.CurveSmoothnessPercent = 60;
                settings.Trail.CaptureSamplesPerSecond = 0;
                settings.Trail.VertexColorOnly = true;
                settings.Trail.OverrideTrailSortOrder = false;
                settings.Trail.LocalSpaceTrails = false;
                _granSlider?.SetValue(60);
                _freqSlider?.SetValue(0);
                if (trailPreviewer is not null) trailPreviewer.OnlyColorVertex = true;

                settings.RaisePropertyChanged("Trail");
            }

            trailController.Revert(selectionController.SelectedEntry!);
        };
        var editMatBtn = new BaseButton("Edit material").SetParent(actionRow.RectTransform).AddLayoutElement(flexibleWidth: 1);
        editMatBtn.OnClick = () =>
        {
            var trail = previewSession?.FocusedSaber?.GetTrailLayout().Primary;
            SaberSense.Rendering.MaterialHandle? matDesc = null;
            if (trail is not null) matDesc = trail.Material;
            else matDesc = previewSession?.FocusedSaber?.Profile?.Customization?.TrailSettings?.Material;

            if (matDesc is not null && matDesc.Material != null && _materialEditor is not null)
            {
                _materialEditor.Show(matDesc);
            }
            else
            {
                var msg = new UIModal("Notice", _canvasRoot, 70, 30);
                var label = new UILabel("Msg", "No editable material is available for the currently selected trail.")
                .SetFontSize(UITheme.FontSmall)
                .SetColor(UITheme.TextPrimary)
                .SetAlignment(TMPro.TextAlignmentOptions.Center);
                label.RectTransform.SetParent(msg.ContentArea.RectTransform, false);
                label.AddLayoutElement(flexibleHeight: 1);
                msg.AddButtons("OK", () => msg.Hide());
                msg.Show();
            }
        };

        var chooseTrailBtn = new BaseButton("Choose trail").SetParent(actionRow.RectTransform).AddLayoutElement(flexibleWidth: 1);
        chooseTrailBtn.OnClick = () =>
        {
            if (_chooseTrailPopup is not null && catalog is not null)
            {
                var sortMode = (SortMode)(settings?.Editor?.SortMode ?? 0);
                var currentOriginPath = previewSession?.FocusedSaber?.Profile?.Customization?.TrailSettings?.OriginAssetPath;
                _chooseTrailPopup.Show(
                catalog.EnumeratePreviews(),
                sortMode,
                catalog.ExternalSearchPaths,
                TrailPopupSelectionChanged,
                currentOriginPath
                );
            }
        };
    }

    private void TrailPopupSelectionChanged(TrailSettings? trailSettings, List<SaberSense.Rendering.SaberTrailMarker>? trailList)
    {
        editor.ApplyTrailSelection(selectionController.SelectedEntry!, trailSettings, trailList!, previewSession?.FocusedSaber);
        Refresh();
    }

    public void Dispose()
    {
        _bindingScope.Dispose();
        _selectionChangedSub?.Dispose();
        _settingsChangedSub?.Dispose();
    }
}