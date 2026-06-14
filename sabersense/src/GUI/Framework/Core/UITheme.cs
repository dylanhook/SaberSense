// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

public static class UITheme
{
    public static readonly Color DefaultAccent = new(0.62f, 0.79f, 0.16f, 1f);

    public static Color Accent { get; set; } = DefaultAccent;
    public static Color AccentHover => new(
    Mathf.Min(Accent.r + 0.10f, 1f),
    Mathf.Min(Accent.g + 0.10f, 1f),
    Mathf.Min(Accent.b + 0.10f, 1f), 1f);
    public static Color AccentPressed => new(
    Mathf.Max(Accent.r - 0.10f, 0f),
    Mathf.Max(Accent.g - 0.10f, 0f),
    Mathf.Max(Accent.b - 0.06f, 0f), 1f);

    private static readonly List<Graphic> _accentGraphics = [];

    public static void TrackAccent(Graphic g) { if (g != null) _accentGraphics.Add(g); }
    public static void UntrackAccent(Graphic g) => _accentGraphics.Remove(g);

    public static void ClearAccentTracking() => _accentGraphics.Clear();

    public static event System.Action? OnAccentChanged;

    public static void SetAccent(Color color)
    {
        Accent = color;
        UIGradient.InvalidateAccent();

        for (int i = _accentGraphics.Count - 1; i >= 0; i--)
        {
            if (_accentGraphics[i] == null) { _accentGraphics.RemoveAt(i); continue; }
            _accentGraphics[i].color = color;
        }

        OnAccentChanged?.Invoke();
    }

    public static void SetAccentLive(Color color)
    {
        Accent = color;
        for (int i = _accentGraphics.Count - 1; i >= 0; i--)
        {
            if (_accentGraphics[i] == null) { _accentGraphics.RemoveAt(i); continue; }
            _accentGraphics[i].color = color;
        }
        OnAccentChanged?.Invoke();
    }

    public static readonly Color Surface = new Color32(20, 20, 20, 255);
    public static readonly Color SurfaceLight = new Color32(35, 35, 35, 255);
    public static readonly Color SurfaceHover = new Color32(45, 45, 45, 255);
    public static readonly Color SurfacePressed = new Color32(25, 25, 25, 255);
    public static readonly Color SurfaceDark = new Color32(12, 12, 12, 255);
    public static readonly Color SurfaceSubtle = new Color32(16, 16, 16, 255);
    public static readonly Color SurfaceInner = new Color32(17, 17, 17, 255);
    public static readonly Color SurfaceDeep = new Color32(15, 15, 15, 255);
    public static readonly Color SurfaceCell = new Color32(40, 40, 40, 255);
    public static readonly Color InnerBorder = new Color32(50, 50, 50, 255);
    public static readonly Color ScrollHandle = new Color32(65, 65, 65, 255);

    public static readonly Color ListboxBg = new Color32(35, 35, 35, 255);
    public static readonly Color ListboxSelected = new Color32(26, 26, 26, 255);
    public static readonly Color ListboxHover = new Color32(45, 45, 45, 255);
    public static readonly Color ListboxText = new Color32(200, 200, 200, 255);

    public static readonly Color TextPlaceholder = new Color32(120, 120, 120, 255);

    public static readonly Color TextPrimary = new Color32(220, 220, 220, 255);
    public static readonly Color TextLabel = new Color32(203, 203, 203, 255);
    public static readonly Color TextSecondary = new Color32(200, 200, 200, 255);
    public static readonly Color TextHeader = new Color32(230, 230, 230, 255);
    public static readonly Color TextMuted = new Color32(150, 150, 150, 255);
    public static readonly Color TextVersion = new Color32(100, 100, 100, 255);
    public static readonly Color TextDisabled = new Color32(80, 80, 80, 255);
    public static readonly Color TextExperimental = new Color32(180, 180, 100, 255);
    public static readonly Color TextKeybind = new Color32(107, 107, 107, 255);
    public static readonly Color TextKeybindActive = new Color32(255, 0, 0, 255);

    public static readonly Color NavIcon = new Color32(90, 90, 90, 255);
    public static readonly Color NavIconHover = new Color32(150, 150, 150, 255);
    public static readonly Color NavIconActive = new Color32(210, 210, 210, 255);

    public static readonly Color Border = new Color32(10, 10, 10, 255);
    public static readonly Color BorderBlack = new Color32(0, 0, 0, 255);
    public static readonly Color Divider = new Color32(48, 48, 48, 255);

    public static readonly Color Success = new(0.18f, 0.80f, 0.44f, 1f);
    public static readonly Color Warning = new(1.0f, 0.72f, 0.20f, 1f);
    public static readonly Color Error = new(0.95f, 0.28f, 0.32f, 1f);
    public static readonly Color CloseButton = new(0.8f, 0.2f, 0.2f, 1f);

    public const float AnimFast = 0.1f;
    public const float AnimNormal = 0.2f;
    public const float AnimSlow = 0.35f;

    public const float FontSmall = 2.5f;
    public const float FontNormal = 3.5f;
    public const float FontLarge = 5f;
    public const float FontTitle = 6f;

    public const float SpacingSmall = 1f;
    public const float SpacingNormal = 2f;
    public const float SpacingLarge = 4f;

    public const float TabPadLeft = 4f;
    public const float TabPadRight = 4f;
    public const float TabPadTop = 2f;
    public const float TabPadBottom = 2f;

    public const float ColumnGap = 2f;

    public const float GroupGap = 2f;

    public const float HeaderHeight = 4f;

    public const float ActionRowHeight = 4.5f;

    public const float ButtonRowHeight = 5f;

    public const float LabelHeight = 3.5f;

    public const float SliderRowHeight = 6f;

    public const float DropdownRowHeight = 8f;

    public const float SectionLabelHeight = 4f;

    public const float RowInnerSpacing = 0.5f;

    public const float SeparatorHeight = 0.166f;

    public const float SwatchWidth = 6f;

    public const float SwatchHeight = 3f;

    public const float AccentDotWidth = 1.5f;

    public const float AccentDotHeight = 1.5f;

    public const float AccentBarWidth = 1f;

    public const float NavWidth = 14f;

    public const float NavCellHeight = 14f;

    public const float NavIconSize = 7f;

    public const int PanelPad = 1;

    public const int PreviewPad = 1;

    public const float PreviewSpacing = 1f;

    public const float HeaderSpacing = 1f;

    public const float PreviewHeaderSpacing = 1.5f;

    public const float PreviewOptionsHeight = 18f;

    public const float VersionLabelWidth = 8f;

    public const float BorderThickness = 0.15f;
}