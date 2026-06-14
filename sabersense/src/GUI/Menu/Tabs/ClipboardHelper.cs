// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;

namespace SaberSense.GUI.Menu.Tabs;

internal static class ClipboardHelper
{
    private static readonly System.Reflection.PropertyInfo? _prop;

    static ClipboardHelper()
    {
        var guiUtilityType = typeof(UnityEngine.Object).Assembly.GetType("UnityEngine.GUIUtility")
        ?? System.Type.GetType("UnityEngine.GUIUtility, UnityEngine.IMGUIModule");
        _prop = guiUtilityType?.GetProperty("systemCopyBuffer",
        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
    }

    public static void SetText(string text)
    {
        if (_prop is not null) _prop.SetValue(null, text);
        else ModLogger.ForSource("ClipboardHelper").Warn("Could not access systemCopyBuffer.");
    }

    public static string GetText()
    {
        if (_prop is not null) return _prop.GetValue(null) as string ?? "";
        ModLogger.ForSource("ClipboardHelper").Warn("Could not access systemCopyBuffer.");
        return "";
    }
}