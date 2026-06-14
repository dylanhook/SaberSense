// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Catalog.Model;
using SaberSense.Core;
using SaberSense.Core.Messaging;
using SaberSense.Core.Utilities;
using SaberSense.Customization;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Menu.Builders;
using SaberSense.GUI.Menu.Controllers;
using SaberSense.Persistence;
using SaberSense.Profiles;
using SaberSense.Rendering;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Menu.Tabs;

internal sealed class ModifierTabView : IMenuTab, IModifierTab
{
    public string Title => "Modifiers";

    public string IconPath => IconPaths.Wrench;

    private readonly SaberSelectionController _selectionController;
    private readonly MaterialEditingController _materialController;
    private readonly MaterialPropertyRowBuilder _rowBuilder;
    private readonly PreviewSession _previewSession;
    private readonly SaberLoadout _loadout;
    private readonly IMessageBroker _broker;
    private readonly IJsonProvider _jsonProvider;

    private RectTransform _modifierListRoot = null!;
    private readonly List<GameObject> _modifierRows = [];
    private GameObject _rightColGO = null!;

    private RectTransform _saberMaterialListRoot = null!;
    private readonly List<GameObject> _materialRows = [];
    private UILabel _noMaterialsLabel = null!;
    private BaseButton _revertButton = null!;

    private RectTransform _canvasRoot = null!;

    private IDisposable? _selectionChangedSub;

    public ModifierTabView(
    SaberSelectionController selectionController,
    MaterialEditingController materialController,
    MaterialPropertyRowBuilder rowBuilder,
    PreviewSession previewSession,
    SaberLoadout loadout,
    IMessageBroker broker,
    IJsonProvider jsonProvider)
    {
        _selectionController = selectionController;
        _materialController = materialController;
        _rowBuilder = rowBuilder;
        _previewSession = previewSession;
        _loadout = loadout;
        _broker = broker;
        _jsonProvider = jsonProvider;
    }

    private void NotifyModified()
    {
        var entry = _selectionController.SelectedEntry;
        if (entry is not null)
        {
            var focused = entry[_previewSession.FocusedHand];
            var other = entry[_previewSession.FocusedHand.Other()];
            if (focused?.ComponentModifiers is not null && other?.ComponentModifiers is not null
            && other != focused)
            {
                other.ComponentModifiers.SyncFrom(focused.ComponentModifiers);
            }

            CaptureModifiersToCustomization(_loadout.Left, entry[SaberHand.Left]!);
            CaptureModifiersToCustomization(_loadout.Right, entry[SaberHand.Right]!);
        }
    }

    private void CaptureModifiersToCustomization(SaberProfile profile, PieceDefinition piece)
    {
        if (profile?.Customization is null || piece?.ComponentModifiers is null) return;
        profile.Customization.CaptureModifierState(piece.ComponentModifiers, _jsonProvider);
    }

    public GameObject Build(MenuTabContext ctx)
    {
        _canvasRoot = ctx.CanvasRoot;

        var root = UILayoutFactory.TabRoot("ModifierTab", ctx.Parent);

        var columns = new HBox("ModTabCols").SetParent(root);
        UnityEngine.Object.Destroy(columns.GameObject.GetComponent<ContentSizeFitter>());
        columns.SetSpacing(UITheme.ColumnGap).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var leftCol = new VBox("LeftCol").SetParent(columns.RectTransform);
        UnityEngine.Object.Destroy(leftCol.GameObject.GetComponent<ContentSizeFitter>());
        leftCol.SetSpacing(UITheme.GroupGap).SetPadding(0, 0, 0, 0).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var matsGroup = new UIGroupBox("Saber materials");
        matsGroup.SetParent(leftCol.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        _saberMaterialListRoot = new VBox("MaterialListRoot").SetParent(matsGroup.Content).RectTransform;
        var matLayout = _saberMaterialListRoot.GetComponent<VerticalLayoutGroup>();
        matLayout.childForceExpandHeight = false;
        matLayout.childControlHeight = true;
        _saberMaterialListRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _noMaterialsLabel = new UILabel("NoMats", "No editable materials.")
        .SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextMuted)
        .SetAlignment(TMPro.TextAlignmentOptions.Center)
        .SetParent(_saberMaterialListRoot)
        .AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        _revertButton = new BaseButton("Revert").SetParent(matsGroup.Content).AddLayoutElement(preferredHeight: UITheme.ButtonRowHeight, flexibleWidth: 1);
        _revertButton.GameObject.SetActive(false);
        _revertButton.OnClick = () =>
        {
            var entry = _selectionController.SelectedEntry;
            if (entry is null) return;

            _loadout.Left.Customization?.MaterialOverrides.Clear();
            _loadout.Right.Customization?.MaterialOverrides.Clear();

            _broker?.Publish(new PreviewSaberChangedMsg(entry));
        };

        var rightCol = new VBox("RightCol").SetParent(columns.RectTransform);
        UnityEngine.Object.Destroy(rightCol.GameObject.GetComponent<ContentSizeFitter>());
        rightCol.SetSpacing(UITheme.GroupGap).SetPadding(0, 0, 0, 0).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);
        _rightColGO = rightCol.GameObject;
        _rightColGO.SetActive(false);

        var modsGroup = new UIGroupBox("Custom modifiers");
        modsGroup.SetParent(rightCol.RectTransform).AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        _modifierListRoot = new VBox("ModifierListRoot").SetParent(modsGroup.Content).RectTransform;
        var layout = _modifierListRoot.GetComponent<VerticalLayoutGroup>();
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        _modifierListRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var modRevertBtn = new BaseButton("Revert").SetParent(modsGroup.Content).AddLayoutElement(preferredHeight: UITheme.ButtonRowHeight, flexibleWidth: 1);
        modRevertBtn.OnClick = () =>
        {
            var mcm = _previewSession?.ActiveRenderer?.Definition?.ComponentModifiers;
            if (mcm is null) return;
            foreach (var mod in mcm.AllBindings()) mod.Reset();
            NotifyModified();
            RefreshModifiers();
        };

        BindEvents();
        return root.gameObject;
    }

    private void BindEvents()
    {
        _selectionChangedSub = _broker?.Subscribe<SaberEquippedMsg>(msg => OnSelectionChanged(msg.Entry))!;
    }

    private void OnSelectionChanged(SaberAssetEntry entry)
    {
    }

    public void RefreshModifiers()
    {
        foreach (var row in _modifierRows) UnityEngine.Object.Destroy(row);
        _modifierRows.Clear();
        List<UISlider> sliders = [];

        var mcm = _previewSession?.ActiveRenderer?.Definition?.ComponentModifiers;
        bool hasModifiers = mcm is not null && mcm.HasModifiers && mcm.AllBindings().Count is > 0;
        if (_rightColGO != null) _rightColGO.SetActive(hasModifiers);
        if (!hasModifiers) return;

        var focusedHand = _previewSession?.FocusedHand ?? SaberHand.Left;
        var customization = _loadout[focusedHand].Customization;
        var modsJson = customization?.ModifierState?["bindings"] as JObject;

        foreach (var mod in mcm!.AllBindings())
        {
            var modJson = mcm.ResolveSavedBlock(modsJson, mod);

            foreach (var param in mod.DescribeEditor(modJson, _jsonProvider))
            {
                if (param.SectionHeader is not null)
                _modifierRows.Add(BuildSectionHeader(mod.Name, param.SectionHeader));

                switch (param.Kind)
                {
                    case SaberSense.Behaviors.ModifierParamKind.Bool:
                    _modifierRows.Add(BuildBoolRow(mod.Name, param));
                    break;

                    case SaberSense.Behaviors.ModifierParamKind.Float:
                    var slider = BuildFloatSlider(param);
                    _modifierRows.Add(UILayoutFactory.SliderRow(param.Label, slider, _modifierListRoot));
                    sliders.Add(slider);
                    break;

                    default:

                    throw new NotSupportedException($"Unhandled {nameof(SaberSense.Behaviors.ModifierParamKind)} {param.Kind} for modifier param '{param.Label}'.");
                }
            }
        }
        Canvas.ForceUpdateCanvases();

        foreach (var s in sliders) s.ForceClipEvaluation();
    }

    private GameObject BuildSectionHeader(string modName, string text)
    {
        var labelRow = new HBox(modName + "Label").SetParent(_modifierListRoot);
        labelRow.SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.SectionLabelHeight, flexibleWidth: 1);
        labelRow.LayoutGroup.childAlignment = TextAnchor.LowerLeft;
        new UILabel(modName + "L", text).SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextHeader)
        .SetAlignment(TMPro.TextAlignmentOptions.Left).SetParent(labelRow.RectTransform);
        return labelRow.GameObject;
    }

    private GameObject BuildBoolRow(string modName, SaberSense.Behaviors.ModifierParam param)
    {
        var toggle = new UIToggle().SetValue(param.GetBool());
        toggle.OnValueChanged(val =>
        {
            param.SetBool(val);
            NotifyModified();
        });

        var row = new HBox(modName + "CR").SetParent(_modifierListRoot);
        row.SetSpacing(UITheme.ColumnGap).SetPadding(0, 0, 0, 0).AddLayoutElement(preferredHeight: UITheme.LabelHeight, flexibleWidth: 1);
        row.LayoutGroup.childAlignment = TextAnchor.MiddleLeft;
        row.LayoutGroup.childForceExpandHeight = false;
        toggle.SetParent(row.RectTransform);
        new UILabel(modName + "L", param.Label).SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextSecondary)
        .SetAlignment(TMPro.TextAlignmentOptions.Left).SetParent(row.RectTransform).AddLayoutElement(flexibleWidth: 1, preferredHeight: UITheme.LabelHeight);
        UILayoutFactory.AddRowHitArea(row.RectTransform, toggle);

        return row.GameObject;
    }

    private UISlider BuildFloatSlider(SaberSense.Behaviors.ModifierParam param)
    {
        var slider = new UISlider().SetRange(param.Min, param.Max).SetValue(param.GetFloat());
        slider.OnValueChanged(val => param.SetFloat(val));
        slider.OnCommit(val => NotifyModified());
        return slider;
    }

    public void RefreshMaterials()
    {
        if (_saberMaterialListRoot == null) return;

        foreach (var row in _materialRows) UnityEngine.Object.Destroy(row);
        _materialRows.Clear();

        var entry = _selectionController.SelectedEntry;
        if (entry?.LeftPiece is SaberAssetDefinition def)
        _materialController.SnapshotOriginals(def, entry.LeftPiece.Asset?.RelativePath!);

        var leftSaber = _previewSession?.Sabers?.Left;
        PieceRenderer? masterRenderer = leftSaber?.Renderer;
        if (masterRenderer?.GameObject == null)
        {
            if (_noMaterialsLabel is not null) _noMaterialsLabel.GameObject.SetActive(true);
            if (_revertButton is not null) _revertButton.GameObject.SetActive(false);
            return;
        }

        var renderers = new List<Renderer>();
        masterRenderer.GameObject.GetComponentsInChildren(true, renderers);

        const SaberHand editHand = SaberHand.Left;
        List<(string name, Material mat, Renderer rend, int slot, SaberHand hand)> editableMaterials = [];
        var nameResolver = new SaberSense.Core.Utilities.MaterialNameResolver();
        HashSet<string> leftRawNames = [];

        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            var mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || mats[i].shader == null) continue;
                var shaderInfo = _materialController.GetShaderInfo(mats[i].shader);
                if (shaderInfo is null || shaderInfo.Count is 0) continue;

                string baseName = nameResolver.Resolve(mats[i]);

                leftRawNames.Add(SaberSense.Core.Utilities.MaterialNameResolver.StripUnityNameSuffixes(mats[i].name));

                var handMat = _materialController.FindMaterialOnHand(baseName, editHand) ?? mats[i];
                editableMaterials.Add((baseName, handMat, rend, i, editHand));
            }
        }

        var rightSaber = _previewSession?.Sabers?.Right;
        PieceRenderer? rightRenderer = rightSaber?.Renderer;
        if (rightRenderer?.GameObject != null)
        {
            var rightRenderers = new List<Renderer>();
            rightRenderer.GameObject.GetComponentsInChildren(true, rightRenderers);
            var rightNameResolver = new SaberSense.Core.Utilities.MaterialNameResolver();

            foreach (var rend in rightRenderers)
            {
                if (rend == null) continue;
                var mats = rend.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || mats[i].shader == null) continue;
                    var shaderInfo = _materialController.GetShaderInfo(mats[i].shader);
                    if (shaderInfo is null || shaderInfo.Count is 0) continue;

                    string rawName = SaberSense.Core.Utilities.MaterialNameResolver.StripUnityNameSuffixes(mats[i].name);

                    if (leftRawNames.Contains(rawName)) continue;

                    string baseName = rightNameResolver.Resolve(mats[i]);
                    editableMaterials.Add((baseName, mats[i], rend, i, SaberHand.Right));
                }
            }
        }

        if (editableMaterials.Count is 0)
        {
            if (_noMaterialsLabel is not null) _noMaterialsLabel.GameObject.SetActive(true);
            if (_revertButton is not null) _revertButton.GameObject.SetActive(false);
            return;
        }

        if (_noMaterialsLabel is not null) _noMaterialsLabel.GameObject.SetActive(false);
        if (_revertButton is not null) _revertButton.GameObject.SetActive(true);

        foreach (var (name, material, rend, slot, hand) in editableMaterials)
        {
            var displayName = hand == SaberHand.Right ? $"{name} [R]" : name;
            var section = new UICollapsibleSection(displayName);
            section.SetParent(_saberMaterialListRoot);
            _materialRows.Add(section.GameObject);

            var shaderInfo = _materialController.GetShaderInfo(material.shader);
            var customization = _loadout[hand].Customization;
            var propertyRows = _rowBuilder.BuildPropertyRows(material, name, customization!, shaderInfo!,
            section.Content, _canvasRoot);
            _materialRows.AddRange(propertyRows);
        }
        Canvas.ForceUpdateCanvases();
    }

    public void Dispose()
    {
        _selectionChangedSub?.Dispose();
    }
}