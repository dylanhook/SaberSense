// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core;
using SaberSense.Customization;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Menu.Controllers;
using SaberSense.Profiles;
using SaberSense.Rendering.Shaders;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.GUI.Menu.Builders;

internal sealed class TogglePropertyBuilder : MaterialPropertyBuilderBase
{
    public TogglePropertyBuilder(
    MaterialEditingController materialController,
    SplitPopupManager splitPopup,
    SaberSelectionController selectionController,
    Persistence.IJsonProvider jsonProvider,
    PreviewSession previewSession)
    : base(materialController, splitPopup, selectionController, jsonProvider, previewSession) { }

    public GameObject BuildSharedRow(Material material, string matName,
    ShaderProperty prop, SaberCustomization customization, RectTransform parent)
    {
        bool val = ResolveFloat(customization, matName, prop.Name) > 0;
        var toggle = new UIToggle().SetValue(val);
        var mat = material;
        var pid = prop.Id;
        var capturedHand = SourceHand;
        toggle.OnValueChanged(v =>
        {
            ApplyToBothHands(mat, matName, m => m.SetFloat(pid, v ? 1 : 0));
            MaterialController.Snapshot(matName, mat, capturedHand);
        });

        var rowGO = UILayoutFactory.CheckboxRow("  " + prop.Description, toggle, parent, out var lbl);
        SplitPopup.MakeLabelInteractive(lbl, matName, prop.Name, customization, toggle);
        return rowGO;
    }

    public List<GameObject> BuildSplitRows(Material material, string matName,
    ShaderProperty prop, SaberCustomization customization, RectTransform parent)
    {
        var pid = prop.Id;

        var leftMat = material;
        var rightMat = MaterialController.FindMaterialOnHand(matName, SaberHand.Right) ?? material;

        return BuildSplitRows(material, matName, prop, customization, parent,
        (handVal, hand, tag) =>
        {
            var targetMat = hand == SaberHand.Left ? leftMat : rightMat;
            bool val = handVal is not null
            ? handVal.ToObject<float>(Json) > 0
            : targetMat.GetFloat(pid) > 0;
            var toggle = new UIToggle().SetValue(val);
            var capturedHand = hand;
            toggle.OnValueChanged(v =>
            {
                targetMat.SetFloat(pid, v ? 1 : 0);
                MaterialController.SnapshotSplit(matName, prop.Name,
                Newtonsoft.Json.Linq.JToken.FromObject(v ? 1f : 0f), capturedHand);
            });

            var rowGO = UILayoutFactory.CheckboxRow("  " + prop.Description + " " + tag, toggle, parent, out var lbl);
            SplitPopup.MakeLabelInteractive(lbl, matName, prop.Name, customization, toggle);
            return rowGO;
        });
    }
}