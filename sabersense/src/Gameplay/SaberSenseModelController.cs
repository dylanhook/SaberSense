// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using CameraUtils.Core;
using IPA.Utilities;
using SaberSense.Configuration;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using SaberSense.Profiles;
using SaberSense.Rendering;
using SaberSense.Rendering.Materials;
using SaberSense.Services;
using SiraUtil.Interfaces;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace SaberSense.Gameplay;

internal sealed class SaberSenseModelController : SaberModelController, IColorable, IPreSaberModelInit
{
    private SaberReplacer _replacer = null!;
    private IModLogger _log = null!;
    private LiveSaber.Factory _liveFactory = null!;
    private SaberLoadout _loadout = null!;
    private IConfigStore _configManager = null!;
    private List<IClashCustomizer> _customizers = null!;
    private ModSettings _settings = null!;
    private ViewVisibilityService _viewVis = null!;
    private IDefaultSaberProvider? _defaultSabers;
    private SaberEventRouter? _eventRouter;

    [Inject]
    public void Construct(
    SaberReplacer replacer,
    IModLogger log,
    LiveSaber.Factory liveFactory,
    SaberLoadout loadout,
    IConfigStore configManager,
    List<IClashCustomizer> customizers,
    ModSettings settings,
    ViewVisibilityService viewVis,
    [InjectOptional] IDefaultSaberProvider? defaultSabers,
    [InjectOptional] SaberEventRouter? eventRouter)
    {
        _replacer = replacer;
        _log = log.ForSource(nameof(SaberSenseModelController));
        _liveFactory = liveFactory;
        _loadout = loadout;
        _configManager = configManager;
        _customizers = customizers;
        _settings = settings;
        _viewVis = viewVis;
        _defaultSabers = defaultSabers;
        _eventRouter = eventRouter;
    }

    private Color? _tint;
    private LiveSaber _activeSaber = null!;
    private readonly List<LiveSaber> _allSabers = [];
    private bool _destroyed;

    private static class Reflectors
    {
        public static readonly FieldAccessor<SaberModelController, ColorManager>.Accessor ColorManager
        = FieldAccessor<SaberModelController, ColorManager>.GetAccessor("_colorManager");
        public static readonly FieldAccessor<SaberModelController, SetSaberGlowColor[]>.Accessor GlowColors
        = FieldAccessor<SaberModelController, SetSaberGlowColor[]>.GetAccessor("_setSaberGlowColors");
        public static readonly FieldAccessor<SaberModelController, SetSaberFakeGlowColor[]>.Accessor FakeGlowColors
        = FieldAccessor<SaberModelController, SetSaberFakeGlowColor[]>.GetAccessor("_setSaberFakeGlowColors");
        public static readonly FieldAccessor<SetSaberGlowColor, ColorManager>.Accessor GlowColorManager
        = FieldAccessor<SetSaberGlowColor, ColorManager>.GetAccessor("_colorManager");
        public static readonly FieldAccessor<SetSaberFakeGlowColor, ColorManager>.Accessor FakeGlowColorManager
        = FieldAccessor<SetSaberFakeGlowColor, ColorManager>.GetAccessor("_colorManager");
        public static readonly FieldAccessor<global::SaberTrail, SaberTrailRenderer>.Accessor TrailRenderer
        = FieldAccessor<global::SaberTrail, SaberTrailRenderer>.GetAccessor("_trailRenderer");
    }

    public Color Color
    {
        get => _tint.GetValueOrDefault();
        set => SetColor(value);
    }

    public void SetColor(Color color)
    {
        _tint = color;
        foreach (var saber in _allSabers)
        saber?.SetColor(color);
    }

    public bool PreInit(Transform parent, Saber saber)
    {
        SpawnAsync(parent, saber);
        return false;
    }

    private void SpawnAsync(Transform parent, Saber saber)
    {
        ErrorBoundary.FireAndForget(SpawnCore(parent, saber), _log, nameof(SpawnAsync));
    }

    private async System.Threading.Tasks.Task SpawnCore(Transform parent, Saber saber)
    {
        _log.Debug($"SpawnCore: saberType={saber.saberType} awaiting ReplacementTask");
        await _replacer.ReplacementTask;
        if (_destroyed) return;
        transform.SetParent(parent, false);

        _log.Debug($"SpawnCore: ensuring assets valid for {saber.saberType}");
        await _configManager.EnsureAssetsValidAsync();
        if (_destroyed) return;

        var profile = GetProfileForSaberType(saber.saberType);
        _log.Debug($"SpawnCore: spawning {saber.saberType} equipped={profile.Equipped is not null} hand={profile.Hand}");
        _activeSaber = SpawnPrimarySaber(saber);

        if (_activeSaber.EventDispatcher is not null)
        _eventRouter?.BindEvents(_activeSaber.EventDispatcher, saber.saberType);

        var vis = new VisibilityPolicy(_viewVis, _settings);
        ApplyViewSplitting(saber, vis);
        SpawnDefaultSaberMirror(parent, saber, vis);
        SetColor(_tint ?? _colorManager.ColorForSaberType(_activeSaber.Profile.Hand.ToSaberType()));
        ConfigureMotionBlur(saber, vis);
        _log.Info("Saber spawned for gameplay");
    }

    private void OnDestroy()
    {
        _destroyed = true;
    }

    private SaberProfile GetProfileForSaberType(SaberType type)
    => type == SaberType.SaberA ? _loadout.Left : _loadout.Right;

    private LiveSaber SpawnPrimarySaber(Saber saber)
    {
        var profile = GetProfileForSaberType(saber.saberType);
        var liveSaber = _liveFactory.Create(profile, MaterialPoolOwner.Gameplay);
        _allSabers.Add(liveSaber);

        if (saber.saberType == SaberType.SaberA)
        foreach (var c in _customizers) c.SetSaber(liveSaber);

        liveSaber.ActivateForGameplay(transform, _saberTrail);
        return liveSaber;
    }

    private void ApplyViewSplitting(Saber saber, in VisibilityPolicy vis)
    {
        LiveSaber? smoothedSaber = null;
        LiveSaber? unsmoothedSaber = null;
        var profile = GetProfileForSaberType(saber.saberType);

        if (vis.NeedsSmoothedCopy && vis.NeedsUnsmoothedCopy)
        {
            _log.Debug($"ApplyViewSplitting: {saber.saberType} -> dual copy (smoothed + unsmoothed)");
            smoothedSaber = _activeSaber;
            unsmoothedSaber = _liveFactory.Create(profile, MaterialPoolOwner.Gameplay);
            _allSabers.Add(unsmoothedSaber);
            if (saber.saberType == SaberType.SaberA) foreach (var c in _customizers) c.SetSaber(unsmoothedSaber);
            unsmoothedSaber.SetParent(transform);
            unsmoothedSaber.CreateTrail(false, _saberTrail);
            unsmoothedSaber.SetColor(_tint ?? _colorManager.ColorForSaberType(unsmoothedSaber.Profile.Hand.ToSaberType()));
        }
        else if (vis.NeedsSmoothedCopy) { _log.Debug($"ApplyViewSplitting: {saber.saberType} -> smoothed only"); smoothedSaber = _activeSaber; }
        else if (vis.NeedsUnsmoothedCopy) { _log.Debug($"ApplyViewSplitting: {saber.saberType} -> unsmoothed only"); unsmoothedSaber = _activeSaber; }

        if (smoothedSaber != null)
        {
            SaberSmoother.InsertAbove(smoothedSaber.CachedTransform, transform, _settings!.SmoothingStrength);
            ApplyVisibility(smoothedSaber.GameObject, vis.SmoothedSaber, vis.SmoothedTrail.Any());
            ApplyTrailVisibility(smoothedSaber, vis.SmoothedTrail);
        }

        if (unsmoothedSaber != null)
        {
            ApplyVisibility(unsmoothedSaber.GameObject, vis.UnsmoothedSaber, vis.UnsmoothedTrail.Any());
            ApplyTrailVisibility(unsmoothedSaber, vis.UnsmoothedTrail);
        }
    }

    private void SpawnDefaultSaberMirror(Transform parent, Saber saber, in VisibilityPolicy vis)
    {
        var saberPresence = vis[ViewFeature.Sabers];
        bool defaultSaberHmd = !saberPresence.Hmd();
        bool defaultSaberDesk = !saberPresence.Desktop();
        if (!defaultSaberHmd && !defaultSaberDesk) { _log.Debug($"SpawnDefaultSaberMirror: {saber.saberType} -> not needed (custom visible in both views)"); return; }
        if (_defaultSabers?.VanillaSaberPrefab == null) { _log.Debug($"SpawnDefaultSaberMirror: {saber.saberType} -> no default saber prefab"); return; }

        var profile = GetProfileForSaberType(saber.saberType);
        if (profile.TryGetSaberAsset(out var def) &&
        def?.Asset?.RelativePath == Catalog.DefaultSaberProvider.DefaultSaberPath)
        {
            _log.Debug($"SpawnDefaultSaberMirror: {saber.saberType} -> skipping (equipped IS default)");
            return;
        }
        _log.Debug($"SpawnDefaultSaberMirror: {saber.saberType} -> spawning mirror (hmd={defaultSaberHmd} desk={defaultSaberDesk})");

        var defaultSaberGO = GameObject.Instantiate(_defaultSabers!.VanillaSaberPrefab!, parent, false);
        var defaultSaberController = defaultSaberGO.GetComponent<SaberModelController>();

        Reflectors.ColorManager(ref defaultSaberController) = this._colorManager;

        var glowColors = Reflectors.GlowColors(ref defaultSaberController);
        if (glowColors != null) foreach (var gc in glowColors) if (gc != null) { var gcRef = gc; Reflectors.GlowColorManager(ref gcRef) = this._colorManager; }

        var fakeGlowColors = Reflectors.FakeGlowColors(ref defaultSaberController);
        if (fakeGlowColors != null) foreach (var fgc in fakeGlowColors) if (fgc != null) { var fgcRef = fgc; Reflectors.FakeGlowColorManager(ref fgcRef) = this._colorManager; }

        defaultSaberController.Init(parent, saber, _colorManager.ColorForSaberType(saber.saberType));
        defaultSaberGO.SetActive(true);
        ApplyVisibility(defaultSaberGO, defaultSaberHmd, defaultSaberDesk);

        var trailPresence = vis[ViewFeature.Trails];
        var st = defaultSaberGO.GetComponent<global::SaberTrail>();
        if (st != null)
        {
            var tr = Reflectors.TrailRenderer(ref st);
            if (tr != null) ApplyVisibility(tr.gameObject, defaultSaberHmd && !trailPresence.Hmd(), defaultSaberDesk && !trailPresence.Desktop());
        }
    }

    private void ConfigureMotionBlur(Saber saber, in VisibilityPolicy vis)
    {
        var blurPresence = vis[ViewFeature.MotionBlur];
        if (!vis.MotionBlurActive || !blurPresence.Any()) return;

        _activeSaber.CreateMotionBlur(_settings.MotionBlur.Strength);
        _activeSaber.RefreshMotionBlurColors();
        ApplyMotionBlurVisibility(_activeSaber, blurPresence);

        foreach (var s in _allSabers)
        {
            if (s != null && s != _activeSaber)
            {
                s.CreateMotionBlur(_settings.MotionBlur.Strength);
                s.RefreshMotionBlurColors();
                ApplyMotionBlurVisibility(s, blurPresence);
            }
        }
    }

    private static void ApplyVisibility(GameObject go, ViewPresence presence, bool keepActiveForTrails = false)
    => ViewVisibilityService.ApplyVisibility(go, presence.Hmd(), presence.Desktop(), keepActiveForTrails, VisibilityLayer.Saber);

    private static void ApplyVisibility(GameObject go, bool hmd, bool desk, bool keepActiveForTrails = false)
    => ViewVisibilityService.ApplyVisibility(go, hmd, desk, keepActiveForTrails, VisibilityLayer.Saber);

    private static void ApplyTrailVisibility(LiveSaber saber, ViewPresence trail)
    {
        if (saber == null) return;

        if (trail == ViewPresence.Both)
        {
            saber.SetTrailVisibilityLayer(VisibilityLayer.Saber);
            return;
        }

        if (trail == ViewPresence.None)
        {
            saber.DestroyTrail(true);
            return;
        }

        var layer = trail.Hmd() ? VisibilityLayer.HmdOnlyAndReflected : VisibilityLayer.DesktopOnlyAndReflected;
        saber.SetTrailVisibilityLayer(layer);
    }

    private static void ApplyMotionBlurVisibility(LiveSaber saber, ViewPresence blur)
    {
        if (saber == null) return;

        if (blur == ViewPresence.Both)
        {
            saber.SetMotionBlurVisibilityLayer(VisibilityLayer.Saber);
            return;
        }

        if (blur == ViewPresence.None) return;

        var layer = blur.Hmd() ? VisibilityLayer.HmdOnlyAndReflected : VisibilityLayer.DesktopOnlyAndReflected;
        saber.SetMotionBlurVisibilityLayer(layer);
    }
}