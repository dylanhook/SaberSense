// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Core.Logging;
using SaberSense.GUI.Framework.Core;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Menu.Popups;

internal sealed class TexturePickerPopup
{
    private const float ModalWidth = 85f;
    private const float ModalHeight = 75f;
    private const float ButtonWidth = 36f;
    private const float ButtonHeight = 4.5f;
    private const float EmptyMsgHeight = 20f;
    private const float OpenBtnHeight = 8f;
    private const float EmptySpacing = 4f;
    private const int EmptyPadding = 8;

    private readonly UIModal _modal;
    private readonly UIScrollList _list;
    private readonly TextureCache _textureStore;
    private readonly GameObject _emptyStateGO;
    private readonly BaseButton _originalBtn;

    private Action<Texture2D?>? _onSelectionChanged;
    private Texture2D? _originalTexture;

    public TexturePickerPopup(RectTransform canvasRoot, TextureCache textureStore)
    {
        _textureStore = textureStore;
        _textureStore.OnCacheChanged += RefreshList;
        _modal = new UIModal("Choose texture", canvasRoot, ModalWidth, ModalHeight);

        _list = new UIScrollList("TextureList");
        _list.SetCellHeight(8.5f);
        _list.SetCellPadding(2, 2, 1, 1);
        _list.RectTransform.SetParent(_modal.ContentArea.RectTransform, false);
        _list.AddLayoutElement(flexibleHeight: 1);
        _list.EnableSearch(canvasRoot);

        _list.OnSelect((index, data) =>
        {
            if (data.UserData is CachedTexture texAsset)
            {
                _onSelectionChanged?.Invoke(texAsset.Texture);
                _modal.Hide();
            }
        });

        _emptyStateGO = new GameObject("EmptyState");
        _emptyStateGO.transform.SetParent(_modal.ContentArea.RectTransform, false);
        var emptyRT = _emptyStateGO.AddComponent<RectTransform>();
        emptyRT.anchorMin = Vector2.zero;
        emptyRT.anchorMax = Vector2.one;
        emptyRT.sizeDelta = Vector2.zero;
        var emptyVLG = _emptyStateGO.AddComponent<VerticalLayoutGroup>();
        emptyVLG.childControlWidth = true;
        emptyVLG.childControlHeight = true;
        emptyVLG.childForceExpandWidth = true;
        emptyVLG.childForceExpandHeight = false;
        emptyVLG.childAlignment = TextAnchor.MiddleCenter;
        emptyVLG.spacing = EmptySpacing;
        emptyVLG.padding = new RectOffset(EmptyPadding, EmptyPadding, EmptyPadding, EmptyPadding);

        var msgLabel = new UILabel("EmptyMsg", "No textures found.\nPlace .png or .jpg files in the Textures folder.")
        .SetFontSize(UITheme.FontTitle)
        .SetColor(UITheme.TextSecondary)
        .SetAlignment(TMPro.TextAlignmentOptions.Center);
        msgLabel.RectTransform.SetParent(_emptyStateGO.transform, false);
        msgLabel.AddLayoutElement(preferredHeight: EmptyMsgHeight, flexibleWidth: 1);

        var openBtn = new BaseButton("Open textures folder");
        openBtn.RectTransform.SetParent(_emptyStateGO.transform, false);
        openBtn.AddLayoutElement(preferredHeight: OpenBtnHeight, flexibleWidth: 1);
        openBtn.OnClick = () => OpenTexturesFolder();

        _emptyStateGO.SetActive(false);

        _modal.AddButtons("Close", () => _modal.Hide(), "Clear", () =>
        {
            _onSelectionChanged?.Invoke(null);
            _modal.Hide();
        });

        _originalBtn = new BaseButton("Original", showAccent: false);
        _originalBtn.RectTransform.SetParent(_modal.ButtonsRow!.RectTransform, false);
        _originalBtn.RectTransform.SetAsFirstSibling();
        _originalBtn.AddLayoutElement(preferredHeight: ButtonHeight, flexibleWidth: 1);
        _originalBtn.OnClick = () =>
        {
            _onSelectionChanged?.Invoke(_originalTexture!);
            _modal.Hide();
        };

        var openFolderBtn = new BaseButton("Open folder", showAccent: false);
        openFolderBtn.RectTransform.SetParent(_modal.ButtonsRow.RectTransform, false);
        openFolderBtn.RectTransform.SetAsFirstSibling();
        openFolderBtn.AddLayoutElement(preferredHeight: ButtonHeight, flexibleWidth: 1);
        openFolderBtn.OnClick = () => OpenTexturesFolder();
    }

    public async Task ShowAsync(Action<Texture2D?> onSelectionChanged, Texture2D? originalTexture = null, string? currentTextureName = null)
    {
        try
        {
            _onSelectionChanged = onSelectionChanged;
            _originalTexture = originalTexture;
            _originalBtn.GameObject.SetActive(originalTexture != null);

            await _textureStore.PopulateCacheAsync();
            RefreshList();

            if (!string.IsNullOrEmpty(currentTextureName))
            _list.Select(currentTextureName!, triggerEvent: false);
            else
            _list.Deselect();

            _modal.Show();
        }
        catch (Exception ex) { ModLogger.Error($"TexturePicker.Show failed: {ex}"); }
    }

    public void Exit()
    {
        _modal.Hide();
        _textureStore.OnCacheChanged -= RefreshList;
    }

    private void RefreshList()
    {
        var items = _textureStore.EnumerateAll()
        .Select(c => new UIListCellData(c.Identifier, "", c.Sprite, c)).ToList();
        _list.SetItems(items);

        bool hasItems = items.Count is > 0;
        _list.GameObject.SetActive(hasItems);
        _emptyStateGO.SetActive(!hasItems);
    }

    private void OpenTexturesFolder()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo(_textureStore.TexturesFolder.FullName) { UseShellExecute = true };
            System.Diagnostics.Process.Start(psi);
        }
        catch (System.Exception ex) { ModLogger.Debug($"Failed to open textures folder: {ex.Message}"); }
    }
}