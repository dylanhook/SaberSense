// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Catalog.Model;
using SaberSense.Core;
using SaberSense.Customization;
using SaberSense.Profiles;
using SaberSense.Rendering.Materials;
using SaberSense.Rendering.Shaders;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.GUI.Menu.Controllers;

internal sealed class MaterialEditingController
{
    private readonly MaterialOverrideService _overrideService;
    private readonly MaterialSyncService _syncService;
    private readonly OriginalMaterialCache _originalCache;
    private readonly ShaderIntrospector _shaderCache;
    private readonly SaberLoadout _loadout;
    private readonly PreviewSession _previewSession;
    private readonly PlayerDataModel _playerDataModel;
    private readonly EditScope _editScope;

    public MaterialEditingController(
    MaterialOverrideService overrideService,
    MaterialSyncService syncService,
    OriginalMaterialCache originalCache,
    ShaderIntrospector shaderCache,
    SaberLoadout loadout,
    PreviewSession previewSession,
    PlayerDataModel playerDataModel,
    EditScope editScope)
    {
        _overrideService = overrideService;
        _syncService = syncService;
        _originalCache = originalCache;
        _shaderCache = shaderCache;
        _loadout = loadout;
        _previewSession = previewSession;
        _playerDataModel = playerDataModel;
        _editScope = editScope;
    }

    public void Snapshot(string matName, Material mat)
    {
        _overrideService.Snapshot(matName, mat);
        RefreshPropertyBlocks();
    }

    public void Snapshot(string matName, Material mat, SaberHand sourceHand)
    {
        _overrideService.Snapshot(matName, mat, sourceHand);
        RefreshPropertyBlocks();
    }

    public void SnapshotSplit(string matName, string propName,
    JToken value, SaberHand hand)
    {
        _overrideService.SnapshotSplit(matName, propName, value, hand);
        RefreshPropertyBlocks();
    }

    public Material? FindLiveMaterial(string matName)
    => _syncService.FindLiveMaterial(matName);

    public Material? FindMaterialOnHand(string matName, SaberHand hand)
    => _syncService.FindMaterialOnHand(matName, hand)
    ?? _previewSession?.MaterialPool?.Get(MaterialPoolOwner.Menu, matName, hand);

    public void ApplyToBothHands(Material mat, string matName, System.Action<Material> apply)
    {
        apply(mat);
        var leftMat = FindMaterialOnHand(matName, SaberHand.Left);
        var rightMat = FindMaterialOnHand(matName, SaberHand.Right);
        if (leftMat != null && leftMat != mat) apply(leftMat);
        if (rightMat != null && rightMat != mat) apply(rightMat);
    }

    public void SnapshotOriginals(SaberAssetDefinition def, string saberPath)
    => _originalCache.Snapshot(def, saberPath);

    public Color GetOriginalColor(string matName, string propName)
    => _originalCache.GetOriginalColor(matName, propName);

    public float GetOriginalFloat(string matName, string propName)
    => _originalCache.GetOriginalFloat(matName, propName);

    public Texture? GetOriginalTexture(string matName, string propName)
    => _originalCache.GetOriginalTexture(matName, propName);

    public void RestoreOriginals(GameObject saberRoot)
    => _originalCache.RestoreOriginals(saberRoot);

    public IReadOnlyList<ShaderProperty>? GetShaderInfo(Shader shader)
    {
        if (_shaderCache is null || shader == null) return null;
        return _shaderCache[shader];
    }

    public void SplitProperty(SaberAssetEntry entry, string matName, string propName)
    {
        _loadout.Left.Customization?.SplitProperty(matName, propName);
        _loadout.Right.Customization?.SplitProperty(matName, propName);
        RefreshPropertyBlocks();
    }

    public void UnsplitProperty(SaberAssetEntry entry, string matName, string propName)
    {
        _loadout.Left.Customization?.UnsplitProperty(matName, propName);
        _loadout.Right.Customization?.UnsplitProperty(matName, propName);

        SyncUnsplitToRightMaterial(matName, propName);

        RefreshPropertyBlocks();
    }

    private void SyncUnsplitToRightMaterial(string matName, string propName)
    {
        var leftMat = _syncService.FindMaterialOnHand(matName, SaberHand.Left);
        var rightMat = _syncService.FindMaterialOnHand(matName, SaberHand.Right);
        if (leftMat == null || rightMat == null || leftMat == rightMat) return;

        var props = _shaderCache[leftMat.shader];
        if (props is null) return;
        foreach (var prop in props)
        {
            if (prop.Name != propName) continue;
            var value = prop.ReadValue(leftMat);
            if (value is not null)
            prop.WriteTo(rightMat, value);
            break;
        }
    }

    public bool IsPropertySplit(string matName, string propName, SaberCustomization customization)
    {
        return customization is not null && customization.IsPropertySplit(matName, propName);
    }

    public void RefreshPropertyBlocks()
    {
        try
        {
            var scheme = _playerDataModel?.playerData?.colorSchemesSettings?.GetSelectedColorScheme();
            if (scheme == null) return;

            var hand = _previewSession?.FocusedHand ?? SaberHand.Left;

            _previewSession?.Sabers?.Left?.SetColor(scheme.saberAColor);
            _previewSession?.Sabers?.Right?.SetColor(scheme.saberBColor);

            var mirror = _editScope?.PreviewMirror;
            if (mirror != null)
            {
                var mirrorTint = hand == SaberHand.Left ? scheme.saberAColor : scheme.saberBColor;
                mirror.SetColor(mirrorTint);
            }
        }
        catch {  }
    }
}