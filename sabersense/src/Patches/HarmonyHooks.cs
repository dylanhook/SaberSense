// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using HarmonyLib;
using HMUI;
using SaberSense.Core.Logging;
using System;
using System.Collections.Generic;

namespace SaberSense.Patches;

[HarmonyPatch(typeof(FlowCoordinator), "Activate")]
internal sealed class FlowActivationHook
{
    public static void Prefix() => FlowHookHelper.SafeDeactivateEditor(nameof(FlowActivationHook));
}

[HarmonyPatch(typeof(FlowCoordinator), "Deactivate")]
internal sealed class FlowDeactivationHook
{
    public static void Prefix() => FlowHookHelper.SafeDeactivateEditor(nameof(FlowDeactivationHook));
}

internal static class FlowHookHelper
{
    public static void SafeDeactivateEditor(string source)
    {
        try
        {
            HarmonyBridge.Editor?.DeactivateEditor();
        }
        catch (Exception ex) { ModLogger.ForSource(source).Debug($"Editor already disposed during scene transition: {ex.Message}"); }
    }
}

internal static class SabersTabPatch
{
    private const string TabLabel = "SaberSense";

    private static readonly System.Reflection.FieldInfo? TextsField =
    AccessTools.Field(typeof(TextSegmentedControl), "_texts");

    private static readonly System.Reflection.FieldInfo? CellsField =
    AccessTools.Field(typeof(SegmentedControl), "_cells");

    internal static int? TabSlot;

    [HarmonyPatch(typeof(GameplaySetupViewController), "RefreshContent")]
    internal static class InjectTab
    {
        public static void Postfix(TextSegmentedControl ____selectionSegmentedControl)
        {
            if (TabSlot is null) return;

            if (TextsField is null)
            {
                ModLogger.ForSource("SabersTabPatch").Warn("_texts field not found on TextSegmentedControl -- game update may have renamed it");
                return;
            }

            var existing = (IList<string>)TextsField.GetValue(____selectionSegmentedControl);

            if (existing is not null && existing.Contains(TabLabel)) return;

            var list = new List<string>(existing ?? []) { TabLabel };
            TabSlot = list.Count - 1;
            ____selectionSegmentedControl.SetTexts(list);
        }
    }

    [HarmonyPatch(typeof(GameplaySetupViewController), "SetActivePanel")]
    internal static class InterceptSelection
    {
        public static bool Prefix(
        int panelIdx,
        int ____activePanelIdx,
        TextSegmentedControl ____selectionSegmentedControl)
        {
            if (TabSlot is null || panelIdx != TabSlot.Value) return true;

            var menuButton = HarmonyBridge.MenuButton;
            if (menuButton is null)
            {
                ModLogger.ForSource("SabersTabPatch").Warn("MenuButton not ready - ignoring tab click");
                return true;
            }

            ____selectionSegmentedControl.SelectCellWithNumber(____activePanelIdx);
            menuButton.ShowMenu();

            if (CellsField is null)
            {
                ModLogger.ForSource("SabersTabPatch").Warn("_cells field not found on SegmentedControl -- game update may have renamed it");
                return false;
            }

            var cells = (List<SegmentedControlCell>)CellsField.GetValue(____selectionSegmentedControl);
            if (TabSlot.Value < cells.Count)
            cells[TabSlot.Value].ClearHighlight(SelectableCell.TransitionType.Instant);

            return false;
        }
    }
}