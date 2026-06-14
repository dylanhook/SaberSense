// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using IPA.Utilities.Async;
using SaberSense.Catalog;
using SaberSense.Catalog.Model;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using SaberSense.Rendering.Materials;
using SaberSense.Rendering.TrailGeometry;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Rendering;

internal sealed class SaberAssetRenderer(
SaberAssetDefinition definition,
List<IPartFinalizer> postProcessors,
TextureCacheRegistry textureRegistry,
Persistence.IJsonProvider jsonProvider,
IModLogger log,
SharedMaterialPool materialPool)
: PieceRenderer(definition, postProcessors)
{
    public LiveTrail? LiveTrail { get; private set; }

    private readonly Newtonsoft.Json.JsonSerializer _json = jsonProvider.Json;
    private readonly IModLogger _log = log.ForSource(nameof(SaberAssetRenderer));
    private readonly MaterialNameResolver _nameResolver = new();

    public Profiles.SaberCustomization? Customization { get; set; }

    public MaterialPoolOwner PoolOwner { get; set; } = MaterialPoolOwner.Menu;

    private SetSaberGlowColor[] _glowComponents = [];
    private SetSaberFakeGlowColor[] _fakeGlowComponents = [];

    private int[][] _glowPropertyIds = [];

    public override void Initialize()
    {
        base.Initialize();

        _glowComponents = GameObject.GetComponentsInChildren<SetSaberGlowColor>(true);
        _fakeGlowComponents = GameObject.GetComponentsInChildren<SetSaberFakeGlowColor>(true);

        _glowPropertyIds = new int[_glowComponents.Length][];
        for (var i = 0; i < _glowComponents.Length; i++)
        {
            var pairs = _glowComponents[i]._propertyTintColorPairs;
            if (pairs is null)
            {
                _glowPropertyIds[i] = [];
                continue;
            }

            var ids = new int[pairs.Length];
            for (var j = 0; j < pairs.Length; j++)
            ids[j] = Shader.PropertyToID(pairs[j].property);
            _glowPropertyIds[i] = ids;
        }

        var settings = Customization?.TrailSettings ?? ((SaberAssetDefinition)Definition).TrailSettings;

        if (settings is null)
        _log.Warn($"No trail settings for {Definition.AssignedHand} saber");

        ResolveTrails(GameObject, settings!);
    }

    public override void ApplyColor(Color color)
    {
        base.ApplyColor(color);

        if (Definition is not SaberAssetDefinition { ForceColorable: true }) return;

        var overrides = Customization?.MaterialOverrides;
        var nameResolver = _nameResolver.BeginScope();

        for (var i = 0; i < _glowComponents.Length; i++)
        {
            var glow = _glowComponents[i];
            if (glow._meshRenderer == null || glow._propertyTintColorPairs is null) continue;

            string? matName = null;
            if (overrides?.Count is > 0 && glow._meshRenderer.sharedMaterial != null)
            matName = nameResolver.Resolve(glow._meshRenderer.sharedMaterial);

            if (!RendererBlocks.TryGetValue(glow._meshRenderer, out var block)) continue;

            glow._meshRenderer.GetPropertyBlock(block);

            var pairs = glow._propertyTintColorPairs;
            for (var j = 0; j < pairs.Length; j++)
            {
                var pair = pairs[j];

                if (matName is not null && overrides!.TryGetValue(matName, out var ovrObj) && ovrObj[pair.property] is not null)
                continue;

                block.SetColor(_glowPropertyIds[i][j], color * pair.tintColor);
            }

            glow._meshRenderer.SetPropertyBlock(block);
        }

        foreach (var fakeGlow in _fakeGlowComponents)
        {
            if (fakeGlow._parametric3SliceSprite != null)
            {
                fakeGlow._parametric3SliceSprite.color = color;
                fakeGlow._parametric3SliceSprite.Refresh();
            }
        }
    }

    public void ResolveTrails(GameObject root, TrailSettings settings)
    {
        if (root == null || settings is null) return;

        var trailComponents = SaberComponentDiscovery.GetTrails(root)!.ToArray();

        SaberTrailMarker BuildSyntheticTrail(Material? mat, float tipZ, float baseZ, int segmentCount)
        {
            var marker = root.AddComponent<SaberTrailMarker>();
            marker.Length = segmentCount;

            var tipObj = root.CreateGameObject("SS_FallbackTip");
            var baseObj = root.CreateGameObject("SS_FallbackBase");
            tipObj.transform.localPosition = new Vector3(0, 0, tipZ);
            baseObj.transform.localPosition = new Vector3(0, 0, baseZ);

            marker.PointStart = baseObj.transform;
            marker.PointEnd = tipObj.transform;
            marker.TrailMaterial = mat;
            return marker;
        }

        if (trailComponents is not { Length: > 0 })
        trailComponents = [BuildSyntheticTrail(null, 1f, 0f, 12)];

        var primaryTrail = trailComponents[0];
        List<SaberTrailMarker>? extras = null;

        if (settings.OriginTrails is { Count: > 1 })
        {
            extras = [];
            for (var i = 1; i < trailComponents.Length; i++)
            Object.DestroyImmediate(trailComponents[i]);

            for (var i = 1; i < settings.OriginTrails.Count; i++)
            {
                var origin = settings.OriginTrails[i];
                if (origin.PointStart == null || origin.PointEnd == null) continue;

                extras.Add(BuildSyntheticTrail(
                origin.TrailMaterial,
                origin.PointEnd.localPosition.z,
                origin.PointStart.localPosition.z,
                origin.Length));
            }
        }
        else if (trailComponents.Length is > 1)
        {
            extras = trailComponents.Skip(1).ToList();
        }

        var existingValid = settings.Material is { IsValid: true };
        _log.Info($"ResolveTrails hand={Definition.AssignedHand} existingMatValid={existingValid} existingId={settings.Material?.Material?.GetInstanceID()} freshId={primaryTrail.TrailMaterial?.GetInstanceID()}");
        if (!existingValid)
        {
            var freshMat = primaryTrail.TrailMaterial;
            if (freshMat != null)
            {
                settings.Material = new(new Material(freshMat));
                if (freshMat.TryGetMainTexture(out var tex))
                settings.NativeTextureWrap = tex.wrapMode;

                if (settings.DeferredMaterialJson is not null)
                {
                    ApplyMaterialOverrides(settings.Material.Material!, settings.DeferredMaterialJson, Definition.AssignedHand);
                    settings.DeferredMaterialJson = null;
                }
            }
            else
            {
                _log.Warn("Trail material is missing on the instantiated prefab.");
            }
        }

        var start = primaryTrail.PointStart!;
        var end = primaryTrail.PointEnd!;
        var reversed = start.localPosition.z > end.localPosition.z;
        if (reversed) (start, end) = (end, start);

        LiveTrail?.Destroy();
        LiveTrail = LiveTrail.Create(settings, start, end, reversed, extras);
    }

    protected override void CollectColorableMaterials(List<Material> results, List<Material> allOwned)
    {
        var renderers = new List<Renderer>();
        GameObject.GetComponentsInChildren(true, renderers);

        var saberDefinition = Definition as SaberAssetDefinition;
        var overrides = Customization?.MaterialOverrides;
        var nameResolver = _nameResolver.BeginScope();
        var hand = saberDefinition?.AssignedHand ?? SaberHand.Left;
        var forceColorable = saberDefinition?.ForceColorable ?? false;

        foreach (var rend in renderers)
        {
            if (rend == null) continue;

            var mats = rend.sharedMaterials;
            bool materialsChanged = false;
            for (var i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;

                bool isColorable = forceColorable
                ? mats[i].HasProperty(ShaderUtils.TintColorId)
                : ShaderUtils.SupportsSaberColoring(mats[i]);
                string baseName = nameResolver.Resolve(mats[i]);
                Newtonsoft.Json.Linq.JObject? ovrObj = null;
                bool hasOverrides = overrides is not null && overrides.TryGetValue(baseName, out ovrObj);

                bool isNew = !materialPool.Contains(PoolOwner, baseName, hand);
                mats[i] = materialPool.GetOrClone(PoolOwner, baseName, mats[i], hand);
                materialsChanged = true;

                if (isColorable)
                results.Add(mats[i]);

                if (isNew && hasOverrides)
                ApplyMaterialOverrides(mats[i], ovrObj!, hand);
            }

            if (materialsChanged)
            rend.sharedMaterials = mats;
        }
    }

    private void ApplyMaterialOverrides(Material mat, Newtonsoft.Json.Linq.JObject overrides, SaberHand hand)
    {
        var asyncTextures = MaterialPropertyApplier.ApplyAll(mat, overrides, hand, _json, textureRegistry);
        if (asyncTextures is null) return;

        foreach (var (propId, texName) in asyncTextures)
        {
            var capturedMat = mat;
            var capturedPropId = propId;
            ErrorBoundary.FireAndForget(ResolveAndApplyTextureAsync(capturedMat, capturedPropId, texName), _log, "TextureOverride");
        }
    }

    private async Task ResolveAndApplyTextureAsync(Material mat, int propId, string texName)
    {
        if (textureRegistry is null) return;
        var cached = await textureRegistry.ResolveAnyAsync(texName);
        if (cached?.Texture != null && mat != null)
        {
            await UnityMainThreadTaskScheduler.Factory.StartNew(() =>
            {
                mat?.SetTexture(propId, cached.Texture);
            });
        }
    }

    protected override GameObject SpawnPiece()
    {
        (Definition as SaberAssetDefinition)?.ReparentTrails();
        var prefab = Definition!.AuxObjects!.GetHandObject(Definition.AssignedHand)
        ?? throw new System.InvalidOperationException(
        $"[SaberAssetRenderer] Prefab for '{Definition.Asset?.RelativePath ?? "?"}' " +
        $"(hand={Definition.AssignedHand}) is null - the asset bundle was destroyed " +
        "during a scene transition. EnsureAssetsValidAsync should have been called upstream.");

        var instance = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        instance.SetActive(true);

        TransformBlockHandler = new SaberAssetTransformHandler(instance, Customization?.Transform ?? new());
        foreach (var pp in PostProcessors) pp.ProcessPart(instance);

        return instance;
    }

    internal sealed class Factory : Zenject.PlaceholderFactory<SaberAssetDefinition, SaberAssetRenderer> { }
}