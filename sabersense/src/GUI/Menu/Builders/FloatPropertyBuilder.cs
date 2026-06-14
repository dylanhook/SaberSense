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

internal sealed class FloatPropertyBuilder : MaterialPropertyBuilderBase
{
    public FloatPropertyBuilder(
    MaterialEditingController materialController,
    SplitPopupManager splitPopup,
    SaberSelectionController selectionController,
    Persistence.IJsonProvider jsonProvider,
    PreviewSession previewSession)
    : base(materialController, splitPopup, selectionController, jsonProvider, previewSession) { }

    public GameObject BuildSharedSliderRow(Material material, string matName,
    ShaderProperty prop, SaberCustomization customization, RectTransform parent,
    float min, float max)
    {
        var val = ResolveFloat(customization, matName, prop.Name);
        var slider = new UISlider().SetRange(min, max).SetValue(val);
        var mat = material;
        var pid = prop.Id;
        var capturedHand = SourceHand;

        slider.OnValueChanged(v => ApplyToBothHands(mat, matName, m => m.SetFloat(pid, v)));
        slider.OnCommit(v =>
        {
            MaterialController.Snapshot(matName, mat, capturedHand);
        });
        var rowGO = UILayoutFactory.SliderRow("  " + prop.Description, slider, parent);
        SplitPopup.MakeLabelInteractiveInRow(rowGO, matName, prop.Name, customization);
        return rowGO;
    }

    public List<GameObject> BuildSplitSliderRows(Material material, string matName,
    ShaderProperty prop, SaberCustomization customization, RectTransform parent,
    float min, float max)
    {
        var pid = prop.Id;

        var leftMat = material;
        var rightMat = MaterialController.FindMaterialOnHand(matName, SaberHand.Right) ?? material;

        return BuildSplitRows(material, matName, prop, customization, parent,
        (handVal, hand, tag) =>
        {
            var targetMat = hand == SaberHand.Left ? leftMat : rightMat;
            float val = handVal is not null
            ? handVal.ToObject<float>(Json)
            : targetMat.GetFloat(pid);
            var slider = new UISlider().SetRange(min, max).SetValue(val);
            var capturedHand = hand;
            slider.OnValueChanged(v =>
            {
                targetMat.SetFloat(pid, v);
                MaterialController.SnapshotSplit(matName, prop.Name,
                Newtonsoft.Json.Linq.JToken.FromObject(v), capturedHand);
            });
            var rowGO = UILayoutFactory.SliderRow("  " + prop.Description + " " + tag, slider, parent);
            SplitPopup.MakeLabelInteractiveInRow(rowGO, matName, prop.Name, customization);
            return rowGO;
        });
    }
}