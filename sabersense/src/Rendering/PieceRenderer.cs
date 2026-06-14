// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog.Model;
using SaberSense.Core.Utilities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Rendering;

internal class PieceRenderer : IDisposable
{
    public SaberAssetTransformHandler? TransformBlockHandler { get; protected set; }

    public GameObject GameObject { get; private set; } = null!;
    public Transform CachedTransform { get; private set; } = null!;
    public readonly PieceDefinition Definition;

    protected readonly List<IPartFinalizer> PostProcessors;

    private List<Material>? _tintMaterials;

    private List<Material>? _ownedMaterials;

    protected readonly Dictionary<Renderer, MaterialPropertyBlock> RendererBlocks = [];

    protected PieceRenderer(PieceDefinition definition, List<IPartFinalizer> postProcessors)
    {
        PostProcessors = postProcessors;
        Definition = definition;
    }

    public virtual void Initialize()
    {
        GameObject = SpawnPiece();
        CachedTransform = GameObject.transform;
        Definition.ComponentModifiers.BindToInstance(GameObject);

        _tintMaterials = [];
        _ownedMaterials = [];
        CollectColorableMaterials(_tintMaterials, _ownedMaterials);

        var renderers = new List<Renderer>();
        GameObject.GetComponentsInChildren(true, renderers);
        foreach (var rend in renderers)
        {
            if (rend != null)
            RendererBlocks[rend] = new();
        }
    }

    protected virtual GameObject SpawnPiece() => new GameObject("BasePiece");

    protected virtual void CollectColorableMaterials(List<Material> results, List<Material> allClones) { }

    public virtual void ApplyColor(Color color)
    {
        if (_tintMaterials is null) return;

        foreach (var mat in _tintMaterials)
        {
            if (mat == null) continue;

            if (mat.HasProperty(ShaderUtils.CustomColorToggleId)
            && mat.GetFloat(ShaderUtils.CustomColorToggleId) < 0.5f)
            continue;
            mat.SetColor(ShaderUtils.TintColorId, color);
        }
    }

    internal void SwapTintMaterial(Material oldMat, Material newMat)
    {
        if (_tintMaterials is null || oldMat == null || newMat == null) return;
        for (int i = 0; i < _tintMaterials.Count; i++)
        {
            if (ReferenceEquals(_tintMaterials[i], oldMat))
            _tintMaterials[i] = newMat;
        }
    }

    internal void ClearUnswappedTintMaterials(HashSet<Material> unswapped)
    {
        if (_tintMaterials is null || unswapped is null) return;
        _tintMaterials.RemoveAll(m => m != null && unswapped.Contains(m));
    }

    public void AttachTo(Transform parent) => CachedTransform.SetParent(parent, false);

    public virtual void Dispose()
    {
        if (_ownedMaterials is not null)
        {
            foreach (var mat in _ownedMaterials) mat.TryDestroy();
            _ownedMaterials = null;
        }
        _tintMaterials = null;
        RendererBlocks.Clear();
    }

    public void DestroyGameObject() => GameObject.TryDestroy();
}