// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;

namespace SaberSense.Gameplay;

internal enum ViewPresence { None, HmdOnly, DesktopOnly, Both }

internal static class ViewPresenceExtensions
{
    public static bool Hmd(this ViewPresence p)
    => p is ViewPresence.HmdOnly or ViewPresence.Both;
    public static bool Desktop(this ViewPresence p)
    => p is ViewPresence.DesktopOnly or ViewPresence.Both;
    public static bool Any(this ViewPresence p)
    => p != ViewPresence.None;

    public static ViewPresence FromFlags(bool hmd, bool desk) => (hmd, desk) switch
    {
        (true, true) => ViewPresence.Both,
        (true, false) => ViewPresence.HmdOnly,
        (false, true) => ViewPresence.DesktopOnly,
        _ => ViewPresence.None
    };
}

internal readonly struct VisibilityPolicy
{
    private readonly ViewPresence[] _features;

    public ViewPresence this[ViewFeature f] => _features[(int)f];

    public bool SmoothingActive { get; }
    public bool MotionBlurActive { get; }

    public ViewPresence SmoothedSaber { get; }
    public ViewPresence SmoothedTrail { get; }
    public ViewPresence UnsmoothedSaber { get; }
    public ViewPresence UnsmoothedTrail { get; }
    public bool NeedsSmoothedCopy => SmoothedSaber.Any() || SmoothedTrail.Any();
    public bool NeedsUnsmoothedCopy => UnsmoothedSaber.Any() || UnsmoothedTrail.Any();

    public VisibilityPolicy(ViewVisibilityService viewVis, ModSettings settings)
    {
        var count = System.Enum.GetValues(typeof(ViewFeature)).Length;
        _features = new ViewPresence[count];
        for (int i = 0; i < count; i++)
        {
            var f = (ViewFeature)i;
            _features[i] = ViewPresenceExtensions.FromFlags(
            viewVis.IsVisible(f, ViewType.Hmd),
            viewVis.IsVisible(f, ViewType.Desktop));
        }

        SmoothingActive = (settings?.SmoothingStrength ?? 0f) > 0f
        && settings?.SmoothingEnabled == true;
        MotionBlurActive = settings?.MotionBlur?.Enabled == true
        && (settings?.MotionBlur?.Strength ?? 0f) > 0f;

        var saber = _features[(int)ViewFeature.Sabers];
        var trail = _features[(int)ViewFeature.Trails];
        var smooth = _features[(int)ViewFeature.MotionSmoothing];

        SmoothedSaber = SmoothingActive
        ? ViewPresenceExtensions.FromFlags(
        saber.Hmd() && smooth.Hmd(),
        saber.Desktop() && smooth.Desktop())
        : ViewPresence.None;
        SmoothedTrail = SmoothingActive
        ? ViewPresenceExtensions.FromFlags(
        trail.Hmd() && smooth.Hmd(),
        trail.Desktop() && smooth.Desktop())
        : ViewPresence.None;

        UnsmoothedSaber = ViewPresenceExtensions.FromFlags(
        saber.Hmd() && !SmoothedSaber.Hmd(),
        saber.Desktop() && !SmoothedSaber.Desktop());
        UnsmoothedTrail = ViewPresenceExtensions.FromFlags(
        trail.Hmd() && !SmoothedTrail.Hmd(),
        trail.Desktop() && !SmoothedTrail.Desktop());
    }
}