// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Framework.Core;
using UnityEngine;

namespace SaberSense.GUI.Menu.Popups;

internal sealed class MessagePopup
{
    private const float ModalWidth = 60f;
    private const float ModalHeight = 30f;

    private readonly UIModal _modal;
    private readonly UILabel _messageLabel;

    public MessagePopup(string title, RectTransform canvasRoot)
    {
        _modal = new UIModal(title, canvasRoot, ModalWidth, ModalHeight);

        _messageLabel = new UILabel("MessageLabel", "")
        .SetFontSize(UITheme.FontNormal)
        .SetColor(UITheme.TextPrimary)
        .SetAlignment(TMPro.TextAlignmentOptions.Center);

        _messageLabel.RectTransform.SetParent(_modal.ContentArea.RectTransform, false);
        _messageLabel.AddLayoutElement(flexibleHeight: 1, flexibleWidth: 1);

        _modal.AddButtons("Okay", () => { });

        if (_modal.ButtonsRow is not null)
        {
            _modal.ButtonsRow.LayoutGroup.childAlignment = UnityEngine.TextAnchor.MiddleCenter;
            var cancelBtn = _modal.ButtonsRow.RectTransform.GetChild(0);
            if (cancelBtn != null) cancelBtn.gameObject.SetActive(false);
        }
    }

    public void Show(string message)
    {
        _messageLabel.SetText(message);
        _modal.Show();
    }

    public void ShowPermanent(string message)
    {
        _messageLabel.SetText(message);
        if (_modal.ButtonsRow is not null) _modal.ButtonsRow.GameObject.SetActive(false);
        if (_modal.ButtonsDivider is not null) _modal.ButtonsDivider.GameObject.SetActive(false);
        _modal.Show();
    }
}