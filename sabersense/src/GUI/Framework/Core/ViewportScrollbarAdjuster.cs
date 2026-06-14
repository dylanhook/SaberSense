// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

internal sealed class ViewportScrollbarAdjuster : MonoBehaviour
{
    private ScrollRect? _scrollRect;
    private RectTransform? _viewport;
    private GameObject? _scrollbarGO;
    private RectTransform? _searchBar;

    private const float WithScrollbar = -2f;

    private const float WithoutScrollbar = -1f;

    private const float LeftOffset = 1f;

    private const float SearchBarBorder = 0.4f;

    private bool _lastScrollable = true;

    public void Init(ScrollRect scrollRect, RectTransform viewport, GameObject scrollbarGO)
    {
        _scrollRect = scrollRect;
        _viewport = viewport;
        _scrollbarGO = scrollbarGO;
    }

    public void SetSearchBar(RectTransform searchBar) => _searchBar = searchBar;

    private void LateUpdate()
    {
        if (_scrollRect == null || _viewport == null || _scrollRect.content == null) return;

        bool scrollable = _scrollRect.content.rect.height > _viewport.rect.height;
        if (scrollable == _lastScrollable) return;
        _lastScrollable = scrollable;

        float rightOffset = scrollable ? WithScrollbar : WithoutScrollbar;
        _viewport.offsetMax = new Vector2(rightOffset, _viewport.offsetMax.y);

        if (_scrollbarGO != null)
        _scrollbarGO.SetActive(scrollable);

        if (_searchBar != null)
        {
            float rtLeft = LeftOffset - SearchBarBorder;
            float rtRight = rightOffset + SearchBarBorder;
            float sizeDeltaX = -(rtLeft + Mathf.Abs(rtRight));
            float anchoredX = (rtRight + rtLeft) / 2f;
            _searchBar.sizeDelta = new Vector2(sizeDeltaX, _searchBar.sizeDelta.y);
            _searchBar.anchoredPosition = new Vector2(anchoredX, _searchBar.anchoredPosition.y);
        }
    }
}