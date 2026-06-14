// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Framework.Core;
using System;
using UnityEngine;

namespace SaberSense.GUI.Menu.Popups;

internal sealed class ConfirmPopup
{
    private const float ModalWidth = 60f;
    private const float ModalHeight = 30f;

    private readonly UIModal _modal;
    private readonly UILabel _messageLabel;
    private Action? _onConfirm;

    public ConfirmPopup(string title, RectTransform canvasRoot, string confirmText = "Continue")
    {
        _modal = new UIModal(title, canvasRoot, ModalWidth, ModalHeight);

        _messageLabel = new UILabel("MessageLabel", "")
        .SetFontSize(UITheme.FontNormal)
        .SetColor(UITheme.TextPrimary)
        .SetAlignment(TMPro.TextAlignmentOptions.Center);

        _messageLabel.RectTransform.SetParent(_modal.ContentArea.RectTransform, false);
        _messageLabel.AddLayoutElement(flexibleHeight: 1, flexibleWidth: 1);

        _modal.AddButtons(confirmText, () => _onConfirm?.Invoke());
    }

    public void Show(string message, Action onConfirm)
    {
        _messageLabel.SetText(message);
        _onConfirm = onConfirm;
        _modal.Show();
    }
}