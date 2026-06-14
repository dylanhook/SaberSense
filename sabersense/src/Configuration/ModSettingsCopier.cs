// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Framework.Core;

namespace SaberSense.Configuration;

internal static class ModSettingsCopier
{
    public static void CopyAll(ModSettings source, ModSettings target)
    {
        AssertCoversAllProperties();

        target.IsActive = source.IsActive;
        target.KeepSabersOnFocusLoss = source.KeepSabersOnFocusLoss;

        target.RandomizeSaber = source.RandomizeSaber;
        target.AnimateSelection = source.AnimateSelection;
        target.MaxGlobalWidth = source.MaxGlobalWidth;
        target.ShowGameplayButton = source.ShowGameplayButton;
        target.ShowDefaultSaber = source.ShowDefaultSaber;
        target.AudioGain = source.AudioGain;
        target.ActionKeyButton = source.ActionKeyButton;
        target.PauseKeyEnabled = source.PauseKeyEnabled;
        target.PauseKeyButton = source.PauseKeyButton;
        target.EnableEventManager = source.EnableEventManager;
        target.WarningMarkerEnabled = source.WarningMarkerEnabled;
        target.WarningTypes = new(source.WarningTypes);
        target.WarningLayerFilter = new(source.WarningLayerFilter);
        target.HidePlatform = source.HidePlatform;
        target.FloorCalibrationEnabled = source.FloorCalibrationEnabled;
        target.FloorCalibrationY = source.FloorCalibrationY;

        target.AccentColor = source.AccentColor;

        target.SmoothingEnabled = source.SmoothingEnabled;
        target.SmoothingStrength = source.SmoothingStrength;
        target.ActivePipeline = source.ActivePipeline;

        target.TransformSelections = new(source.TransformSelections);
        target.GrabSelections = new(source.GrabSelections);

        CopyTrail(source.Trail, target.Trail);
        CopyMotionBlur(source.MotionBlur, target.MotionBlur);
        CopyWorldMod(source.WorldMod, target.WorldMod);
        CopyVisibility(source.Visibility, target.Visibility);
        CopyEditor(source.Editor, target.Editor);
        CopySwingExtrapolation(source.SwingExtrapolation, target.SwingExtrapolation);
    }

    private static void CopyTrail(TrailConfig s, TrailConfig t)
    {
        t.CurveSmoothnessPercent = s.CurveSmoothnessPercent;
        t.CaptureSamplesPerSecond = s.CaptureSamplesPerSecond;
        t.VertexColorOnly = s.VertexColorOnly;
        t.OverrideTrailSortOrder = s.OverrideTrailSortOrder;
        t.LocalSpaceTrails = s.LocalSpaceTrails;
    }

    private static void CopyMotionBlur(MotionBlurConfig s, MotionBlurConfig t)
    {
        t.Enabled = s.Enabled;
        t.Strength = s.Strength;
    }

    private static void CopyWorldMod(WorldModConfig s, WorldModConfig t)
    {
        t.Enabled = s.Enabled;
        t.Modes = new(s.Modes);
        t.Strength = s.Strength;
        t.MenuOnly = s.MenuOnly;
        t.OverrideColor = s.OverrideColor;
        t.RainColor = s.RainColor;
        t.SnowColor = s.SnowColor;
        t.NetworkColor = s.NetworkColor;
    }

    private static void CopyVisibility(VisibilityConfig s, VisibilityConfig t)
    {
        t.Desktop = new(s.Desktop);
        t.Hmd = new(s.Hmd);
    }

    private static void CopyEditor(EditorConfig s, EditorConfig t)
    {
        t.PreviewSaber = s.PreviewSaber;
        t.Rotation = s.Rotation;
        t.RotationSpeed = s.RotationSpeed;
        t.Bloom = s.Bloom;
        t.DisplayTrails = s.DisplayTrails;
        t.SaberPreviewMode = s.SaberPreviewMode;
        t.SortMode = s.SortMode;
    }

    private static void CopySwingExtrapolation(SwingExtrapolationConfig s, SwingExtrapolationConfig t)
    {
        t.Enabled = s.Enabled;
        t.Strength = s.Strength;
        t.GameOnly = s.GameOnly;
    }

    #if DEBUG

    private static bool _verified;
    #endif

    [System.Diagnostics.Conditional("DEBUG")]
    private static void AssertCoversAllProperties()
    {
        #if DEBUG
        if (_verified) return;
        _verified = true;

        var source = new ModSettings();
        var target = new ModSettings();
        MutateEveryLeaf(source);
        CopyAll(source, target);
        AssertLeavesEqual(
        source, target,
        (owner, prop) =>
        $"ModSettingsCopier.CopyAll does not propagate '{LeafName(owner, prop)}' - " +
        "a new setting was added without a matching CopyAll line, so it would save to " +
        "disk (reflective ToJson) but silently drop on in-memory restore.");

        var dirty = new ModSettings();
        var defaults = new ModSettings();
        MutateEveryLeaf(dirty);
        dirty.ResetToDefaults();
        AssertLeavesEqual(
        defaults, dirty,
        (owner, prop) =>
        $"ModSettings.ResetToDefaults does not restore '{LeafName(owner, prop)}' to its " +
        "factory default - a new setting was added without being covered by the reset path.");
        #endif
    }

    #if DEBUG

    private static System.Collections.Generic.IEnumerable<System.Reflection.PropertyInfo> SettableProps(System.Type type) =>
    System.Linq.Enumerable.Where(
    type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance),
    p => p.CanRead && p.CanWrite
    && p.GetGetMethod(false) is not null
    && p.GetSetMethod(false) is not null);

    private static bool IsSubConfig(System.Reflection.PropertyInfo prop) =>
    typeof(BindableSettings).IsAssignableFrom(prop.PropertyType);

    private static string LeafName(object owner, System.Reflection.PropertyInfo prop) =>
    owner is ModSettings ? prop.Name : $"{owner.GetType().Name}.{prop.Name}";

    private static void MutateEveryLeaf(object root)
    {
        foreach (var prop in SettableProps(root.GetType()))
        {
            if (IsSubConfig(prop))
            {
                var child = prop.GetValue(root);
                if (child is not null) MutateEveryLeaf(child);
                continue;
            }

            prop.SetValue(root, MutatedValue(prop.PropertyType, prop.GetValue(root)));
        }
    }

    private static object MutatedValue(System.Type type, object? current)
    {
        if (type == typeof(bool)) return !(bool)current!;
        if (type == typeof(int)) return (int)current! + 1;
        if (type == typeof(float)) return (float)current! + 1f;
        if (type == typeof(double)) return (double)current! + 1d;
        if (type == typeof(UnityEngine.Color))
        {
            var c = (UnityEngine.Color)current!;

            return new UnityEngine.Color(1f - c.r, 1f - c.g, 1f - c.b, 1f - c.a);
        }
        if (type.IsEnum)
        {
            foreach (var v in System.Enum.GetValues(type))
            if (!object.Equals(v, current)) return v!;
            return current!;
        }
        if (type == typeof(System.Collections.Generic.List<int>))
        {
            return new System.Collections.Generic.List<int> { 31337 };
        }

        throw new System.InvalidOperationException(
        $"ModSettingsCopier coverage self-check has no mutation rule for property type " +
        $"'{type.FullName}' - extend MutatedValue/LeafValuesEqual when adding a new leaf type.");
    }

    private static void AssertLeavesEqual(
    object expected,
    object actual,
    System.Func<object, System.Reflection.PropertyInfo, string> message)
    {
        foreach (var prop in SettableProps(expected.GetType()))
        {
            if (IsSubConfig(prop))
            {
                var ec = prop.GetValue(expected);
                var ac = prop.GetValue(actual);
                if (ec is null || ac is null)
                throw new System.InvalidOperationException(
                $"ModSettingsCopier coverage self-check found a null sub-config for " +
                $"'{LeafName(expected, prop)}'.");
                AssertLeavesEqual(ec, ac, message);
                continue;
            }

            if (!LeafValuesEqual(prop.GetValue(expected), prop.GetValue(actual)))
            throw new System.InvalidOperationException(message(expected, prop));
        }
    }

    private static bool LeafValuesEqual(object? a, object? b)
    {
        if (a is null || b is null) return ReferenceEquals(a, b);

        if (a is System.Collections.Generic.List<int> la && b is System.Collections.Generic.List<int> lb)
        return System.Linq.Enumerable.SequenceEqual(la, lb);

        return a.Equals(b);
    }
    #endif
}