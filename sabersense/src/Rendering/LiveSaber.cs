// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using CameraUtils.Core;
using SaberSense.Catalog.Model;
using SaberSense.Configuration;
using SaberSense.Core;
using SaberSense.Core.Utilities;
using SaberSense.Persistence;
using SaberSense.Profiles;
using SaberSense.Rendering.TrailGeometry;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zenject;

namespace SaberSense.Rendering;

internal readonly record struct TrailLayout(
LiveTrail? Primary,
IReadOnlyList<SaberTrailMarker> AuxMarkers)
{
    internal static readonly TrailLayout None = new(null, Array.Empty<SaberTrailMarker>());
}

public class LiveSaber
{
    public const string DisplayName = "SS Saber";

    internal ITrailDriver? TrailHandler { get; set; }
    internal SaberSense.Gameplay.SaberEventDispatcher? EventDispatcher { get; private set; }
    internal readonly SaberProfile Profile;

    internal SaberAssetRenderer? Renderer { get; private set; }

    public readonly Transform CachedTransform;
    public readonly GameObject GameObject;
    public PlayerTransforms? PlayerTransforms { get; internal set; }

    private readonly TrailConfig _trailConfig;
    private readonly PlayerDataModel _playerDataModel;
    private readonly LiveSaberRegistry _activeInstances;
    private readonly ShaderRegistry _shaders;

    private readonly SaberSense.Rendering.Materials.MaterialPoolOwner _poolOwner;

    private TrailController? _trailController;

    private GameObject? _eventTargetRoot;

    private readonly List<ISaberEffect> _effects = new();

    [Inject]
    internal LiveSaber(
    SaberProfile profile,
    SaberSense.Rendering.Materials.MaterialPoolOwner poolOwner,
    SaberAssetRenderer.Factory rendererFactory,
    ModSettings settings,
    List<ISaberEffect> effects,
    PlayerDataModel playerDataModel,
    LiveSaberRegistry saberInstanceList,
    ShaderRegistry shaders,
    IJsonProvider jsonProvider)
    {
        _trailConfig = settings.Trail;
        _playerDataModel = playerDataModel;
        _activeInstances = saberInstanceList;
        _shaders = shaders;
        _poolOwner = poolOwner;

        Profile = profile;
        GameObject = new GameObject(DisplayName);
        DestroySentinel.Attach(GameObject, OnGameObjectDestroyed);

        CachedTransform = GameObject.transform;

        ApplyColorSchemeGlobals();
        ApplyScale();
        SpawnRenderer(rendererFactory, jsonProvider);

        foreach (var effect in effects)
        {
            effect.ProcessSaber(this);
            _effects.Add(effect);
        }

        InjectEventsFromPieces();

        _activeInstances.Register(this);
    }

    public void SetParent(Transform parent) => CachedTransform.SetParent(parent, false);

    public void SetColor(Color color)
    {
        Renderer?.ApplyColor(color);

        TrailHandler?.SetColor(color);
        _trailController?.TintSecondaryTrails(color);

        foreach (var effect in _effects) effect.OnColorChanged(color);
    }

    internal void SwapToHand(SaberHand hand, SaberSense.Rendering.Materials.SharedMaterialPool pool)
    {
        var resolver = new SaberSense.Core.Utilities.MaterialNameResolver();
        var renderers = new List<Renderer>();
        GameObject.GetComponentsInChildren(true, renderers);

        var swaps = new List<(Material old, Material @new)>();

        var unswapped = new HashSet<Material>();

        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            var mats = rend.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string baseName = resolver.Resolve(mats[i]);
                var poolMat = pool.Get(_poolOwner, baseName, hand);
                if (poolMat != null && poolMat != mats[i])
                {
                    swaps.Add((mats[i], poolMat));
                    mats[i] = poolMat;
                    changed = true;
                }
                else if (poolMat == null)
                {
                    unswapped.Add(mats[i]);
                }
            }
            if (changed) rend.sharedMaterials = mats;
        }

        if (Renderer is { } piece)
        {
            foreach (var (old, @new) in swaps)
            piece.SwapTintMaterial(old, @new);

            if (unswapped.Count is > 0)
            piece.ClearUnswappedTintMaterials(unswapped);
        }
    }

    public void SetWhiteStep(float value) => TrailHandler?.SetWhiteStep(value);

    public void SetTrailVisibilityLayer(CameraUtils.Core.VisibilityLayer layer)
    {
        TrailHandler?.SetVisibilityLayer(layer);
        _trailController?.SetVisibilityLayer(layer);
    }

    public void SetMotionBlurVisibilityLayer(CameraUtils.Core.VisibilityLayer layer)
    {
        foreach (var effect in _effects) effect.OnVisibilityLayerChanged(layer);
    }

    public void RefreshMotionBlurColors()
    {
        foreach (var effect in _effects) effect.OnColorChanged(default);
    }

    public void CreateTrail(bool editorMode, global::SaberTrail? fallbackTrail = null)
    {
        _trailController = new(this, _trailConfig);
        _trailController!.Activate(editorMode, fallbackTrail!);
        TrailHandler = _trailController.PrimaryHandler;
    }

    public void CreateMotionBlur(float strength)
    {
        foreach (var effect in _effects)
        if (effect is SaberMotionBlur) return;

        var motionBlur = GameObject.AddComponent<SaberMotionBlur>();
        motionBlur.Strength = strength;

        (float, float)? bounds = null;
        if (TryGetSaberAssetRenderer(out var sar) && sar!.Definition is SaberAssetDefinition def)
        bounds = def.Asset?.ParsedBounds;

        motionBlur.Init(GameObject, bounds, _shaders.InternalColored);
        _effects.Add(motionBlur);
    }

    public void DestroyTrail(bool immediate = false)
    {
        _trailController?.Teardown(immediate);
        _trailController = null;
        TrailHandler = null;
    }

    public void Destroy()
    {
        _activeInstances.Unregister(this);

        GameObject?.TryDestroy();
    }

    public void SetSaberWidth(float width)
    {
        Profile.Scale.Width = SaberScale.Clamp(width);
        ApplyScale();
    }

    public void SetSaberLength(float length)
    {
        Profile.Scale.Length = SaberScale.Clamp(length);
        ApplyScale();
    }

    private void ApplyScale()
    {
        var s = Profile.Scale;
        if (GameObject != null)
        GameObject.transform.localScale = new Vector3(s.Width, s.Width, s.Length);
        foreach (var effect in _effects) effect.OnScaleChanged();
    }

    internal static void WithTransformApplier(LiveSaber? saber, Action<TransformApplier> action)
    {
        if (saber is null) return;
        if (!saber.TryGetSaberAssetRenderer(out var renderer)) return;
        if (renderer!.TransformBlockHandler is { } handler)
        action(handler.Applier);
    }

    internal static void WithLiveTrail(LiveSaber? saber, Action<LiveTrail> action)
    {
        if (saber is null) return;
        var trail = saber.GetTrailLayout().Primary;
        if (trail is not null) action(trail);
    }

    internal bool TryGetSaberAssetRenderer(out SaberAssetRenderer? renderer)
    {
        renderer = Renderer;
        return renderer is not null;
    }

    internal TrailLayout GetTrailLayout()
    {
        if (!TryGetSaberAssetRenderer(out var renderer) || renderer!.LiveTrail is not { } trail)
        return TrailLayout.None;

        var aux = trail.AuxTrails is { Count: > 0 }
        ? (IReadOnlyList<SaberTrailMarker>)trail.AuxTrails.Select(t => t.Trail).ToList()
        : Array.Empty<SaberTrailMarker>();

        return new TrailLayout(trail, aux);
    }

    internal void ActivateForGameplay(Transform parent, global::SaberTrail? fallbackTrail)
    {
        SetParent(parent);
        CreateTrail(editorMode: false, fallbackTrail);
        GameObject.SetLayerRecursively(VisibilityLayer.Saber);
    }

    private void ApplyColorSchemeGlobals()
    {
        if (_playerDataModel?.playerData?.colorSchemesSettings?.GetSelectedColorScheme() is { } scheme)
        {
            Shader.SetGlobalColor(ShaderUtils.LeftHandColorId, scheme.saberAColor);
            Shader.SetGlobalColor(ShaderUtils.RightHandColorId, scheme.saberBColor);
        }
    }

    private void InjectEventsFromPieces()
    {
        if (!TryGetSaberAssetRenderer(out var sar)) return;
        var parseResult = sar!.Definition?.Asset?.ParseResult;
        if (parseResult?.HasEvents != true) return;

        var prefabRoot = sar.Definition?.Asset?.Prefab?.transform.parent?.gameObject;

        if (prefabRoot != null)
        {
            _eventTargetRoot = new GameObject("SS_EventTargets");
            _eventTargetRoot.transform.SetPositionAndRotation(
            prefabRoot.transform.position, prefabRoot.transform.rotation);
            _eventTargetRoot.transform.localScale = prefabRoot.transform.localScale;

            for (int i = 0; i < prefabRoot.transform.childCount; i++)
            {
                var child = prefabRoot.transform.GetChild(i);
                if (child.name is "LeftSaber" or "RightSaber") continue;
                var clone = UnityEngine.Object.Instantiate(child.gameObject, _eventTargetRoot.transform, false);
                clone.name = child.name;
            }
        }

        EventDispatcher = AssetPipeline.Assembly.PrefabComponentInjector.InjectEvents(
        GameObject, parseResult, _eventTargetRoot);
    }

    private void SpawnRenderer(SaberAssetRenderer.Factory rendererFactory, IJsonProvider jsonProvider)
    {
        if (Profile.Equipped is not { } definition) return;

        var renderer = rendererFactory.Create(definition);

        renderer.Customization = Profile.Customization;
        renderer.PoolOwner = _poolOwner;

        renderer.Initialize();
        renderer.AttachTo(CachedTransform);
        Renderer = renderer;

        var customization = Profile.Customization;
        if (customization?.ModifierState is not null && definition.ComponentModifiers is not null)
        customization.ApplyModifierState(definition.ComponentModifiers, jsonProvider);
    }

    private void OnGameObjectDestroyed()
    {
        _trailController?.DisposePrimaryMaterialIfOrphaned(Profile?.Customization?.TrailSettings?.Material);
        DestroyTrail();

        foreach (var effect in _effects) effect.OnTeardown();
        _effects.Clear();
        if (_eventTargetRoot != null)
        {
            UnityEngine.Object.Destroy(_eventTargetRoot);
            _eventTargetRoot = null;
        }
        Renderer?.Dispose();
    }

    internal sealed class Factory : PlaceholderFactory<SaberProfile, SaberSense.Rendering.Materials.MaterialPoolOwner, LiveSaber> { }
}

internal sealed class DestroySentinel : MonoBehaviour
{
    Action? _onDestroy;

    internal static DestroySentinel Attach(GameObject target, Action onDestroy)
    {
        var sentinel = target.AddComponent<DestroySentinel>();
        sentinel._onDestroy = onDestroy;
        return sentinel;
    }

    void OnDestroy() => _onDestroy?.Invoke();
}