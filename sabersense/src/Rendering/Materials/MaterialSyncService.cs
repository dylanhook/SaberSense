// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core;
using SaberSense.Core.Utilities;
using SaberSense.Customization;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Rendering.Materials;

internal sealed class MaterialSyncService
{
    private readonly PreviewSession _previewSession;
    private readonly SharedMaterialPool _pool;
    private readonly List<Renderer> _searchRenderers = [];
    private readonly MaterialNameResolver _nameResolver = new();

    public MaterialSyncService(PreviewSession previewSession, SharedMaterialPool pool)
    {
        _previewSession = previewSession;
        _pool = pool;
    }

    public Material? FindLiveMaterial(string materialName)
    {
        var renderer = _previewSession?.ActiveRenderer;
        if (renderer?.GameObject == null) return null;

        _searchRenderers.Clear();
        _nameResolver.Reset();
        renderer.GameObject.GetComponentsInChildren(true, _searchRenderers);

        foreach (var rend in _searchRenderers)
        {
            if (rend == null) continue;
            var mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string baseName = _nameResolver.Resolve(mats[i]);
                if (baseName == materialName) return mats[i];
            }
        }
        return null;
    }

    public Material? FindMaterialOnHand(string materialName, SaberHand hand)
    {
        var poolMat = _pool?.Get(MaterialPoolOwner.Menu, materialName, hand);
        if (poolMat != null) return poolMat;

        var saber = _previewSession?.Sabers[hand];
        if (saber?.CachedTransform == null) return null;

        _searchRenderers.Clear();
        saber.CachedTransform.GetComponentsInChildren(true, _searchRenderers);

        var resolver = _nameResolver.BeginScope();
        foreach (var rend in _searchRenderers)
        {
            if (rend == null) continue;
            var mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string baseName = resolver.Resolve(mats[i]);
                if (baseName == materialName) return mats[i];
            }
        }

        foreach (var rend in _searchRenderers)
        {
            if (rend == null) continue;
            var mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string? rawName = StripSuffix(mats[i].name);
                if (rawName == materialName) return mats[i];
            }
        }
        return null;
    }

    private static string? StripSuffix(string? name)
    {
        if (name is null) return null;
        int idx = name.IndexOf(" (Instance)");
        if (idx >= 0) name = name[..idx];
        idx = name.IndexOf(" (Clone)");
        if (idx >= 0) name = name[..idx];
        return name.Trim();
    }
}