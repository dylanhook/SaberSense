// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public class UIScrollList : UIElement
{
    private ScrollRect _scrollRect = null!;
    private RectTransform _viewportRect = null!;
    private RectTransform _topBlocker = null!;
    private VBox _contentContainer = null!;
    private RectTransform _bgRect = null!;
    private List<UIListCellData> _items = [];
    private List<UIListCell> _cells = [];
    private readonly Stack<UIListCell> _cellPool = new();
    private const int MaxPoolSize = 50;
    private int _selectedIndex = -1;
    private Action<int, UIListCellData>? _onSelect;
    private float _cellHeight = 10f;
    private RectOffset? _cellPadding;
    private UITextInput? _searchBar;
    private string _filterQuery = "";
    private ViewportScrollbarAdjuster _viewportAdjuster = null!;
    private bool _compact;

    private Func<UIListCell> _cellFactory;

    private Func<UIListCellData, string, bool> _filterPredicate =
    static (d, q) => string.IsNullOrEmpty(q) ||
    d.Title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

    private List<int> _filteredIndices = [];
    private int _virtualStart = 0;
    private int _virtualEnd = 0;
    private const int VirtualBuffer = 4;
    private LayoutElement _topSpacer = null!;
    private LayoutElement _bottomSpacer = null!;
    private float _spacing;
    private bool _virtualScrollRegistered;

    public UIScrollList(string name = "ScrollList", string? title = null) : base(name)
    {
        _cellFactory = () => _compact
        ? new UISimpleCell(new UIListCellData(""))
        : new UIListCell(new UIListCellData(""));

        UITheme.OnAccentChanged += RefreshSelectedAccent;

        var (titleLabel, textStartX, textWidth) = CreateTitleLabel(title);

        var bg = new UIImage("ListBg").SetColor(UITheme.SurfaceInner);
        bg.RectTransform.SetParent(RectTransform, false);
        bg.SetAnchors(Vector2.zero, Vector2.one);
        bg.RectTransform.offsetMin = new Vector2(0.4f, 0.4f);
        bg.RectTransform.offsetMax = new Vector2(-0.4f, -0.4f);
        _bgRect = bg.RectTransform;

        UIBorderUtils.DrawBorderLines("Outer", RectTransform, UITheme.Border, 0f, 0.2f, textStartX, textWidth);
        UIBorderUtils.DrawBorderLines("Inner", RectTransform, UITheme.Divider, 0.2f, 0.2f, textStartX, textWidth);

        if (!string.IsNullOrEmpty(title)) titleLabel.RectTransform.SetAsLastSibling();

        var viewportRect = CreateViewportWithMask(bg, title);
        CreateContentContainer(viewportRect);

        _topBlocker = CreateRaycastBlocker("TopBlocker", bg.RectTransform);
        _topBlocker.anchorMin = new Vector2(0, 1);
        _topBlocker.anchorMax = new Vector2(1, 1);
        _topBlocker.offsetMin = new Vector2(0, viewportRect.offsetMax.y);
        _topBlocker.offsetMax = new Vector2(0, 20);

        var bottomBlocker = CreateRaycastBlocker("BottomBlocker", bg.RectTransform);
        bottomBlocker.anchorMin = Vector2.zero;
        bottomBlocker.anchorMax = new Vector2(1, 0);
        bottomBlocker.offsetMin = new Vector2(0, -20);
        bottomBlocker.offsetMax = new Vector2(0, viewportRect.offsetMin.y);

        _scrollRect = bg.GameObject.AddComponent<VRSafeScrollRect>();
        _scrollRect.viewport = viewportRect;
        _scrollRect.content = _contentContainer.RectTransform;
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 5f;

        var scrollbarGO = BuildScrollbar(bg.RectTransform);

        _viewportAdjuster.Init(_scrollRect, viewportRect, scrollbarGO);

        var guard = bg.GameObject.AddComponent<ContentGuard>();
        guard.Init(_scrollRect, viewportRect, _contentContainer.RectTransform);

        GameObject.AddComponent<CanvasAttachGuard>();
    }

    private (UILabel label, float textStartX, float textWidth) CreateTitleLabel(string? title)
    {
        var titleLabel = new UILabel("ListTitle", title!)
        .SetFontSize(UITheme.FontSmall)
        .SetColor(UITheme.TextLabel);
        titleLabel.TextComponent.fontStyle = TMPro.FontStyles.Bold;
        titleLabel.TextComponent.alignment = TMPro.TextAlignmentOptions.Left;

        float textStartX = -1;
        float textWidth = -1;

        if (!string.IsNullOrEmpty(title))
        {
            titleLabel.RectTransform.SetParent(RectTransform, false);
            titleLabel.RectTransform.anchorMin = new Vector2(0, 1);
            titleLabel.RectTransform.anchorMax = new Vector2(0, 1);
            titleLabel.RectTransform.pivot = new Vector2(0, 0.5f);

            textStartX = 4f;
            textWidth = titleLabel.TextComponent.GetPreferredValues(title).x;

            titleLabel.RectTransform.anchoredPosition = new Vector2(textStartX, 0);
            titleLabel.RectTransform.sizeDelta = new Vector2(textWidth, 4);

            textStartX -= 0.8f;
            textWidth += 1.6f;
        }

        return (titleLabel, textStartX, textWidth);
    }

    private RectTransform CreateViewportWithMask(UIImage bg, string? title)
    {
        var viewport = new GameObject("Viewport");
        var viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.SetParent(bg.RectTransform, false);
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(1, 1);
        viewportRect.offsetMax = new Vector2(-2, string.IsNullOrEmpty(title) ? -1 : -2);

        _viewportAdjuster = bg.GameObject.AddComponent<ViewportScrollbarAdjuster>();
        _viewportRect = viewportRect;

        var maskImg = viewport.AddComponent<Image>();
        maskImg.color = Color.white;
        maskImg.material = UIMaterials.NoBloomMaterial;
        maskImg.raycastTarget = true;
        var mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        return viewportRect;
    }

    private void CreateContentContainer(RectTransform viewportRect)
    {
        _contentContainer = new VBox("Content");
        _contentContainer.RectTransform.SetParent(viewportRect, false);
        _spacing = UITheme.HeaderSpacing;
        _contentContainer.SetSpacing(_spacing);
        _contentContainer.SetPadding(0, 0, 0, 0);
        _contentContainer.RectTransform.anchorMin = new Vector2(0, 1);
        _contentContainer.RectTransform.anchorMax = Vector2.one;
        _contentContainer.RectTransform.pivot = new Vector2(0.5f, 1f);

        var sizeFitter = _contentContainer.AddComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var topGO = new GameObject("TopSpacer");
        topGO.transform.SetParent(_contentContainer.RectTransform, false);
        topGO.AddComponent<RectTransform>();
        _topSpacer = topGO.AddComponent<LayoutElement>();
        _topSpacer.preferredHeight = 0;

        var bottomGO = new GameObject("BottomSpacer");
        bottomGO.transform.SetParent(_contentContainer.RectTransform, false);
        bottomGO.AddComponent<RectTransform>();
        _bottomSpacer = bottomGO.AddComponent<LayoutElement>();
        _bottomSpacer.preferredHeight = 0;
    }

    private GameObject BuildScrollbar(RectTransform bgRect)
    {
        var scrollbarGO = new GameObject("Scrollbar");
        scrollbarGO.transform.SetParent(bgRect, false);
        var scrollbarRect = scrollbarGO.AddComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1, 0);
        scrollbarRect.anchorMax = Vector2.one;
        scrollbarRect.sizeDelta = new Vector2(1, 0);
        scrollbarRect.anchoredPosition = Vector2.zero;
        scrollbarRect.pivot = new Vector2(1, 0.5f);
        scrollbarRect.offsetMin = new Vector2(scrollbarRect.offsetMin.x, 0.15f);
        scrollbarRect.offsetMax = new Vector2(scrollbarRect.offsetMax.x, -0.3f);

        var scrollbarBg = scrollbarGO.AddComponent<Image>();
        scrollbarBg.color = UITheme.SurfaceHover;
        scrollbarBg.material = UIMaterials.NoBloomMaterial;

        var handleGO = new GameObject("Handle");
        handleGO.transform.SetParent(scrollbarGO.transform, false);
        var handleRect = handleGO.AddComponent<RectTransform>();
        handleRect.anchorMin = Vector2.zero;
        handleRect.anchorMax = Vector2.one;
        handleRect.sizeDelta = Vector2.zero;
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.offsetMin = new Vector2(0.15f, 0);
        handleRect.offsetMax = new Vector2(-0.15f, 0);

        var handleImg = handleGO.AddComponent<Image>();
        handleImg.color = UITheme.ScrollHandle;
        handleImg.material = UIMaterials.NoBloomMaterial;

        var dummyHandleGO = new GameObject("DummyHandle");
        dummyHandleGO.transform.SetParent(scrollbarGO.transform, false);
        var dummyHandleRect = dummyHandleGO.AddComponent<RectTransform>();
        dummyHandleRect.anchorMin = Vector2.zero;
        dummyHandleRect.anchorMax = Vector2.one;
        dummyHandleRect.sizeDelta = Vector2.zero;

        var scrollbar = scrollbarGO.AddComponent<VRSafeScrollbar>();
        scrollbar.handleRect = dummyHandleRect;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handleImg;
        scrollbar.transition = Selectable.Transition.None;
        scrollbar.SetVisibleHandle(handleRect);

        _scrollRect.verticalScrollbar = scrollbar;
        _scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        _scrollRect.verticalScrollbarSpacing = 0;

        return scrollbarGO;
    }

    public UIScrollList SetCellHeight(float height)
    {
        _cellHeight = height;
        return this;
    }

    public UIScrollList SetCellPadding(int left, int right, int top, int bottom)
    {
        _cellPadding = new RectOffset(left, right, top, bottom);
        return this;
    }

    public UIScrollList SetCompact(bool compact = true)
    {
        _compact = compact;
        if (compact) _cellHeight = 5f;
        return this;
    }

    public UIScrollList SetCellFactory(Func<UIListCell>? factory)
    {
        _cellFactory = factory ?? (() => _compact
        ? new UISimpleCell(new UIListCellData(""))
        : new UIListCell(new UIListCellData("")));
        return this;
    }

    public UIScrollList SetFilter(Func<UIListCellData, string, bool>? predicate)
    {
        _filterPredicate = predicate ?? (static (d, q) => string.IsNullOrEmpty(q) ||
        d.Title.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        return this;
    }

    public UIScrollList EnableSearch(RectTransform canvasRoot)
    {
        if (_searchBar is not null || _bgRect == null) return this;

        _searchBar = new UITextInput("Search", "Search...", canvasRoot);
        _searchBar.RectTransform.SetParent(_bgRect, false);
        _searchBar.RectTransform.anchorMin = new Vector2(0, 1);
        _searchBar.RectTransform.anchorMax = new Vector2(1, 1);
        _searchBar.RectTransform.pivot = new Vector2(0.5f, 1);

        _searchBar.RectTransform.sizeDelta = new Vector2(-2.2f, 4.5f);
        _searchBar.RectTransform.anchoredPosition = new Vector2(-0.5f, -1f);

        var viewport = _scrollRect.viewport;
        viewport.offsetMax = new Vector2(viewport.offsetMax.x, viewport.offsetMax.y - 5f);

        if (_topBlocker != null)
        _topBlocker.offsetMin = new Vector2(0, viewport.offsetMax.y);

        _viewportAdjuster.SetSearchBar(_searchBar.RectTransform);

        _searchBar.OnTextChanged(FilterItems);
        return this;
    }

    private float RowStride => _cellHeight + _spacing;

    private void RebuildFilteredIndices()
    {
        _filteredIndices.Clear();
        for (int i = 0; i < _items.Count; i++)
        {
            if (_filterPredicate(_items[i], _filterQuery))
            {
                _filteredIndices.Add(i);
            }
        }
    }

    private void FilterItems(string query)
    {
        _filterQuery = query ?? "";
        RebuildFilteredIndices();

        if (_selectedIndex >= 0 && !_filteredIndices.Contains(_selectedIndex))
        _selectedIndex = -1;

        if (_scrollRect != null)
        _scrollRect.verticalNormalizedPosition = 1f;
        RefreshVirtualWindow(true);
    }

    public UIScrollList SetItems(List<UIListCellData> items)
    {
        _items = items;
        _selectedIndex = -1;
        RebuildFilteredIndices();
        RebuildCells();
        return this;
    }

    public UIScrollList OnSelect(Action<int, UIListCellData> callback)
    {
        _onSelect = callback;
        return this;
    }

    public UIScrollList Select(int index)
    {
        _selectedIndex = index;
        RefreshVirtualWindow(false);
        return this;
    }

    private void DeselectAll()
    {
        _selectedIndex = -1;
        foreach (var cell in _cells)
        cell.SetSelected(false);
    }

    private void RebuildCells()
    {
        foreach (var cell in _cells)
        {
            cell.SetSelected(false);
            cell.GameObject.SetActive(false);
            if (_cellPool.Count < MaxPoolSize)
            _cellPool.Push(cell);
            else
            cell.Dispose();
        }
        _cells.Clear();
        _virtualStart = 0;
        _virtualEnd = 0;

        if (!_virtualScrollRegistered && _scrollRect != null)
        {
            _scrollRect.onValueChanged.AddListener(_ => RefreshVirtualWindow(false));
            _virtualScrollRegistered = true;
        }

        RefreshVirtualWindow(true);

        if (_scrollRect != null)
        {
            var deferGO = _scrollRect.gameObject;
            var deferred = deferGO.AddComponent<DeferredRefreshBehaviour>();
            deferred.Init(this);
        }
    }

    private sealed class DeferredRefreshBehaviour : MonoBehaviour
    {
        private UIScrollList? _owner;
        public void Init(UIScrollList owner) => _owner = owner;

        private System.Collections.IEnumerator Start()
        {
            yield return null;
            _owner?.RefreshVirtualWindow(true);
            Destroy(this);
        }
    }

    private void RefreshVirtualWindow(bool force)
    {
        if (_scrollRect == null || _scrollRect.viewport == null) return;

        int totalFiltered = _filteredIndices.Count;
        float viewportHeight = _scrollRect.viewport.rect.height;

        int visibleCount = viewportHeight > 1f
        ? Mathf.CeilToInt(viewportHeight / RowStride) + 1
        : 20;
        int totalWithBuffer = visibleCount + VirtualBuffer * 2;

        float contentHeight = totalFiltered * RowStride;
        float scrollOffset = (1f - _scrollRect.verticalNormalizedPosition) * Mathf.Max(0, contentHeight - viewportHeight);
        int newStart = Mathf.Max(0, Mathf.FloorToInt(scrollOffset / RowStride) - VirtualBuffer);
        int newEnd = Mathf.Min(totalFiltered, newStart + totalWithBuffer);

        if (!force && newStart == _virtualStart && newEnd == _virtualEnd) return;
        _virtualStart = newStart;
        _virtualEnd = newEnd;

        int neededCells = _virtualEnd - _virtualStart;

        while (_cells.Count > neededCells)
        {
            var excess = _cells[_cells.Count - 1];
            _cells.RemoveAt(_cells.Count - 1);
            excess.SetSelected(false);
            excess.GameObject.SetActive(false);
            if (_cellPool.Count < MaxPoolSize) _cellPool.Push(excess);
            else excess.Dispose();
        }

        while (_cells.Count < neededCells)
        {
            UIListCell cell;

            if (!_compact && _cellPool.Count is > 0)
            {
                cell = _cellPool.Pop();
                cell.GameObject.SetActive(true);
                cell.RectTransform.SetParent(_contentContainer.RectTransform, false);
            }
            else
            {
                cell = _cellFactory();
                cell.SetParent(_contentContainer, false);
                cell.AddLayoutElement(preferredHeight: _cellHeight);
            }

            if (_cellPadding is not null)
            {
                var hlg = cell.GameObject.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null) hlg.padding = _cellPadding;
            }

            cell.SetClipRect(_viewportRect);
            _cells.Add(cell);
        }

        for (int i = 0; i < _cells.Count; i++)
        {
            int filteredIdx = _virtualStart + i;
            int dataIdx = _filteredIndices[filteredIdx];
            var data = _items[dataIdx];
            var cell = _cells[i];

            cell.Rebind(data);
            cell.GameObject.SetActive(true);
            cell.SetSelected(dataIdx == _selectedIndex);

            int capturedDataIdx = dataIdx;
            cell.OnClicked = () =>
            {
                DeselectAll();
                _selectedIndex = capturedDataIdx;
                cell.SetSelected(true);
                UICallbackGuard.Invoke(_onSelect!, capturedDataIdx, data);
            };

            cell.RectTransform.SetSiblingIndex(1 + i);
        }

        float topHeight = _virtualStart * RowStride;
        float bottomHeight = Mathf.Max(0, (totalFiltered - _virtualEnd) * RowStride);
        _topSpacer.preferredHeight = topHeight;
        _bottomSpacer.preferredHeight = bottomHeight;
        _topSpacer.transform.SetAsFirstSibling();
        _bottomSpacer.transform.SetAsLastSibling();
    }

    public void Select(string title, bool triggerEvent = true)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].Title != title) continue;
            if (_filteredIndices.Count > 0 && !_filteredIndices.Contains(i)) continue;
            _selectedIndex = i;
            RefreshVirtualWindow(true);
            if (triggerEvent) UICallbackGuard.Invoke(_onSelect!, i, _items[i]);
            return;
        }
        Deselect();
    }

    public void Deselect()
    {
        DeselectAll();
        _selectedIndex = -1;
    }

    public void UpdateCellIcon(object userData, Sprite icon)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (!ReferenceEquals(_items[i].UserData, userData)) continue;
            _items[i].Icon = icon;

            int filteredIdx = _filteredIndices.IndexOf(i);
            if (filteredIdx >= _virtualStart && filteredIdx < _virtualEnd)
            {
                int cellIdx = filteredIdx - _virtualStart;
                if (cellIdx >= 0 && cellIdx < _cells.Count)
                _cells[cellIdx].SetIcon(icon);
            }
            break;
        }
    }

    public void ScrollTo(int position)
    {
        if (_scrollRect == null || _filteredIndices.Count <= 1) return;

        int filteredIdx = _filteredIndices.IndexOf(position);
        if (filteredIdx < 0) filteredIdx = 0;
        int total = _filteredIndices.Count;
        _scrollRect.verticalNormalizedPosition = 1f - (float)filteredIdx / (total - 1);
    }

    private void RefreshSelectedAccent()
    {
        RefreshVirtualWindow(true);
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        UITheme.OnAccentChanged -= RefreshSelectedAccent;
        foreach (var cell in _cells) cell.Dispose();
        _cells.Clear();
        while (_cellPool.Count is > 0) _cellPool.Pop().Dispose();
        base.Dispose();
    }

    private static RectTransform CreateRaycastBlocker(string name, RectTransform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        var img = go.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0);
        img.material = UIMaterials.NoBloomMaterial;
        img.raycastTarget = true;
        return rt;
    }
}