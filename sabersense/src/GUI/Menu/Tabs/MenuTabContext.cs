// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Menu.Popups;
using UnityEngine;

namespace SaberSense.GUI.Menu.Tabs;

internal sealed class MenuTabContext
{
    public RectTransform Parent { get; init; } = null!;

    public RectTransform CanvasRoot { get; init; } = null!;

    public MessagePopup MessagePopup { get; init; } = null!;

    public GameObject PreviewWindowGO { get; init; } = null!;

    public MaterialEditorPopup MaterialEditor { get; init; } = null!;

    public ChooseTrailPopup ChooseTrailPopup { get; init; } = null!;
}