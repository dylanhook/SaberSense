// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

internal sealed class UILogConsole : UIElement
{
    private const int ThrottleFrames = 3;
    private const float AutoScrollThreshold = 0.02f;

    private static readonly Color BgColor = new Color32(10, 10, 10, 255);

    private static readonly string[] LevelColors =
    {
        "<color=#707070>",
        "<color=#B0B0B0>",
        "<color=#FFB833>",
        "<color=#F24752>"
    };

    private static readonly string[] HeaderColors =
    {
        "<color=#484848>",
        "<color=#606060>",
        "<color=#806020>",
        "<color=#802828>"
    };

    private static readonly string[] SourceColors =
    {
        "<color=#585858>",
        "<color=#808080>",
        "<color=#A08030>",
        "<color=#A03838>"
    };

    private static readonly string[] LevelTags =
    {
        "DBG",
        "INF",
        "WRN",
        "ERR"
    };

    private const int MaxStringBuilderChars = 65_000;
    private ScrollRect _scrollRect;
    private TextMeshProUGUI _tmp;
    private readonly StringBuilder _sb = new(4096);
    private readonly ConcurrentQueue<LogEntry> _offThreadQueue = new();
    private int _dirtyFrame = -1;
    private bool _pendingFlush;
    private Coroutine? _flushCoroutine;
    private bool _autoScroll = true;
    private readonly System.Threading.Thread _mainThread = System.Threading.Thread.CurrentThread;

    public UILogConsole(string name = "LogConsole") : base(name)
    {
        var bgImg = GameObject.AddComponent<Image>();
        bgImg.material = UIMaterials.NoBloomMaterial;
        bgImg.color = BgColor;
        bgImg.raycastTarget = true;

        var viewport = new GameObject("Viewport");
        var viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.SetParent(RectTransform, false);
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(1, 1);
        viewportRect.offsetMax = new Vector2(-2, -1);

        var maskImg = viewport.AddComponent<Image>();
        maskImg.color = Color.white;
        maskImg.material = UIMaterials.NoBloomMaterial;
        maskImg.raycastTarget = true;
        var mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportRect, false);
        var contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = Vector2.one;
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.sizeDelta = Vector2.zero;

        var contentLayout = contentGO.AddComponent<VerticalLayoutGroup>();
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.padding = new RectOffset(1, 1, 1, 1);

        var sizeFitter = contentGO.AddComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var textGO = new GameObject("LogText");
        textGO.transform.SetParent(contentRect, false);
        var textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;

        _tmp = textGO.AddComponent<TextMeshProUGUI>();
        _tmp.fontSize = 1.6f;
        _tmp.color = UITheme.TextSecondary;
        _tmp.alignment = TextAlignmentOptions.TopLeft;
        _tmp.overflowMode = TextOverflowModes.Truncate;
        _tmp.enableWordWrapping = true;
        _tmp.richText = true;
        _tmp.raycastTarget = false;
        _tmp.text = "";

        var textLayout = textGO.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1;

        _scrollRect = GameObject.AddComponent<VRSafeScrollRect>();
        _scrollRect.viewport = viewportRect;
        _scrollRect.content = contentRect;
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 5f;
        _scrollRect.onValueChanged.AddListener(OnScrollChanged);

        var scrollbarGO = new GameObject("Scrollbar");
        scrollbarGO.transform.SetParent(RectTransform, false);
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

        var guard = GameObject.AddComponent<ContentGuard>();
        guard.Init(_scrollRect, viewportRect, contentRect);
    }

    public void Rebuild(List<LogEntry> entries)
    {
        while (_offThreadQueue.TryDequeue(out _)) { }

        _sb.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            if (i > 0) AppendSeparator();
            AppendEntryToBuilder(entries[i]);
        }
        _pendingFlush = false;
        _tmp.SetText(_sb);
        ScheduleAutoScroll();
    }

    public void AppendEntry(LogEntry entry)
    {
        if (!ReferenceEquals(System.Threading.Thread.CurrentThread,
        _mainThread))
        {
            _offThreadQueue.Enqueue(entry);
            return;
        }

        DrainOffThreadQueue();

        if (_sb.Length is > 0) AppendSeparator();
        AppendEntryToBuilder(entry);

        int frame = Time.frameCount;
        if (frame - _dirtyFrame < ThrottleFrames)
        {
            _pendingFlush = true;
            ScheduleDeferredFlush();
            return;
        }

        FlushToTMP(frame);
    }

    public void Clear()
    {
        while (_offThreadQueue.TryDequeue(out _)) { }
        _pendingFlush = false;
        _sb.Clear();
        _tmp.SetText("");
    }

    private void DrainOffThreadQueue()
    {
        while (_offThreadQueue.TryDequeue(out var queued))
        {
            if (_sb.Length is > 0) AppendSeparator();
            AppendEntryToBuilder(queued);
            _pendingFlush = true;
        }
    }

    private void FlushToTMP(int frame)
    {
        _dirtyFrame = frame;
        _pendingFlush = false;

        if (_sb.Length > MaxStringBuilderChars)
        {
            int trimTo = _sb.Length - MaxStringBuilderChars;

            for (int i = trimTo; i < _sb.Length - 4; i++)
            {
                if (_sb[i] == '-' && _sb[i + 1] == '-' && _sb[i + 2] == '-' && _sb[i + 3] == '-')
                {
                    while (i > 0 && _sb[i - 1] != '\n') i--;
                    trimTo = i;
                    break;
                }
            }
            _sb.Remove(0, trimTo);
        }

        _tmp.SetText(_sb);
        ScheduleAutoScroll();
    }

    private void ScheduleDeferredFlush()
    {
        if (_flushCoroutine != null) return;
        var mono = _scrollRect != null ? _scrollRect.GetComponent<MonoBehaviour>() : null;
        if (mono != null && mono.isActiveAndEnabled)
        _flushCoroutine = mono.StartCoroutine(DeferredFlush());
    }

    private System.Collections.IEnumerator DeferredFlush()
    {
        for (int i = 0; i < ThrottleFrames; i++)
        yield return null;

        _flushCoroutine = null;
        DrainOffThreadQueue();
        if (_pendingFlush)
        FlushToTMP(Time.frameCount);
    }

    private void AppendEntryToBuilder(LogEntry entry)
    {
        int lvl = (int)entry.Level;

        _sb.Append(HeaderColors[lvl]);
        _sb.Append(entry.Timestamp.ToString("HH:mm:ss"));
        _sb.Append("  ");
        _sb.Append(LevelTags[lvl]);
        _sb.Append("</color>");

        if (!string.IsNullOrEmpty(entry.Source))
        {
            _sb.Append("  ");
            _sb.Append(SourceColors[lvl]);
            _sb.Append(entry.Source);
            _sb.Append("</color>");
        }
        _sb.Append('\n');

        _sb.Append(LevelColors[lvl]);
        _sb.Append(entry.Message);
        _sb.Append("</color>\n");
    }

    private void AppendSeparator()
    {
        _sb.Append("<color=#1A1A1A>--------------------</color>\n");
    }

    private void OnScrollChanged(Vector2 pos)
    {
        _autoScroll = pos.y <= AutoScrollThreshold;
    }

    private void ScheduleAutoScroll()
    {
        if (!_autoScroll || _scrollRect == null) return;

        var mono = _scrollRect.GetComponent<MonoBehaviour>();
        if (mono != null && mono.isActiveAndEnabled)
        mono.StartCoroutine(DeferredScrollToBottom());
    }

    private System.Collections.IEnumerator DeferredScrollToBottom()
    {
        yield return null;
        if (_scrollRect != null)
        _scrollRect.verticalNormalizedPosition = 0f;
    }
}