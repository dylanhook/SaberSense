// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using HarmonyLib;
using SaberSense.Core.Logging;
using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace SaberSense.Patches;

internal static class FontCache
{
    private const string TargetFontName = "Teko-Medium SDF";
    private const int MaxRetryAttempts = 5;

    private static TMP_FontAsset? _mainFont;
    private static int _failureCount;

    public static bool TryFix(TMP_Text text)
    {
        if (text == null) return false;
        if (text.font != null && text.fontSharedMaterial != null) return true;

        if (_failureCount >= MaxRetryAttempts) return false;

        if (_mainFont == null)
        {
            _mainFont = Resources.FindObjectsOfTypeAll<TMP_FontAsset>()
            .FirstOrDefault(t => t.name == TargetFontName);

            if (_mainFont == null)
            {
                _failureCount++;
                return false;
            }

            _failureCount = 0;
        }

        text.font = _mainFont;
        text.fontSharedMaterial = _mainFont.material;
        return true;
    }
}

[HarmonyPatch(typeof(TextMeshProUGUI), "OnEnable")]
internal sealed class TMProFixerUI
{
    static void Postfix(TextMeshProUGUI __instance) => FontCache.TryFix(__instance);
}

[HarmonyPatch(typeof(TextMeshPro), "OnEnable")]
internal sealed class TMProFixer3D
{
    static void Postfix(TextMeshPro __instance) => FontCache.TryFix(__instance);
}

[HarmonyPatch(typeof(TextMeshPro), "OnPreRenderObject")]
internal sealed class TMProFixerPreRender
{
    static bool Prefix(TextMeshPro __instance)
    {
        if (!FontCache.TryFix(__instance))
        return false;

        return true;
    }

    static Exception? Finalizer(Exception __exception, TextMeshPro __instance)
    {
        if (__exception is not null)
        {
            ModLogger.ForSource("TMProFixer").Debug($"Suppressed render error on '{__instance?.name}': {__exception.Message}");
            if (__instance != null)
            __instance.enabled = false;
            return null;
        }
        return __exception;
    }
}