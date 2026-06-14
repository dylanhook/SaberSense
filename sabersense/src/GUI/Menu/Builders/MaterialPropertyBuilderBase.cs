// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Core;
using SaberSense.Customization;
using SaberSense.GUI.Menu.Controllers;
using SaberSense.Profiles;
using SaberSense.Rendering.Materials;
using SaberSense.Rendering.Shaders;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.GUI.Menu.Builders;

internal abstract class MaterialPropertyBuilderBase
{
    protected readonly MaterialEditingController MaterialController;
    protected readonly SplitPopupManager SplitPopup;
    protected readonly SaberSelectionController SelectionController;
    protected readonly PreviewSession PreviewSession;
    protected Newtonsoft.Json.JsonSerializer Json { get; }

    protected SaberHand SourceHand { get; private set; }

    protected MaterialPropertyBuilderBase(
    MaterialEditingController materialController,
    SplitPopupManager splitPopup,
    SaberSelectionController selectionController,
    Persistence.IJsonProvider jsonProvider,
    PreviewSession previewSession)
    {
        MaterialController = materialController;
        SplitPopup = splitPopup;
        SelectionController = selectionController;
        Json = jsonProvider.Json;
        PreviewSession = previewSession;
    }

    public void CaptureSourceHand()
    {
        SourceHand = PreviewSession?.FocusedHand ?? SaberHand.Left;
    }

    protected float ResolveFloat(SaberCustomization customization, string matName, string propName)
    {
        if (customization?.MaterialOverrides?.TryGetValue(matName, out var mo) == true)
        {
            var val = mo[propName];
            if (val is not null && !MaterialPropertyApplier.IsSplit(val))
            return val.ToObject<float>(Json);
        }
        return MaterialController.GetOriginalFloat(matName, propName);
    }

    protected Color ResolveColor(SaberCustomization customization, string matName, string propName)
    {
        if (customization?.MaterialOverrides?.TryGetValue(matName, out var mo) == true)
        {
            var val = mo[propName];
            if (val is not null && !MaterialPropertyApplier.IsSplit(val))
            return val.ToObject<Color>(Json);
        }
        return MaterialController.GetOriginalColor(matName, propName);
    }

    protected void ApplyToBothHands(Material mat, string matName, Action<Material> apply)
    => MaterialController.ApplyToBothHands(mat, matName, apply);

    protected List<GameObject> BuildSplitRows(
    Material material, string matName, ShaderProperty prop,
    SaberCustomization customization, RectTransform parent,
    Func<JToken, SaberHand, string, GameObject> buildOneRow)
    {
        List<GameObject> rows = [];
        foreach (var hand in (SaberHand[])[SaberHand.Left, SaberHand.Right])
        {
            string tag = hand == SaberHand.Left ? "[Left]" : "[Right]";
            var handVal = customization.GetPropertyForHand(matName, prop.Name, hand);
            rows.Add(buildOneRow(handVal!, hand, tag));
        }
        return rows;
    }
}