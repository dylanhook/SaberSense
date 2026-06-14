// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Profiles;
using UnityEngine;

namespace SaberSense.GUI.Menu;

internal sealed class SplitKeyHoverDetector : MonoBehaviour,
UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
{
    public string MatName = "";
    public string PropName = "";
    public SaberCustomization Customization = null!;
    public SplitPopupManager? PopupManager;
    public RectTransform LabelRT = null!;

    private bool _hovered;

    public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData e) => _hovered = true;
    public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => _hovered = false;

    private void Update()
    {
        if (_hovered && PopupManager is not null && SaberSense.Input.ActionKeyInputBehavior.IsPressedDown)
        PopupManager.Show(LabelRT, MatName, PropName, Customization);
    }
}