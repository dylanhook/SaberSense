// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Utilities;
using System;
using UnityEngine;

namespace SaberSense.Rendering;

public class MaterialHandle : IDisposable
{
    public Material? Material
    {
        get => _material;
        set => _material = value;
    }
    private Material? _material;

    public bool IsValid => Material != null;

    public bool IsOwned => _isOwned;

    private Material? _snapshot;
    private bool _disposed;
    private readonly bool _isOwned;

    private const int OverrideRenderQueue = 3100;
    private const int OverrideZWriteOff = 0;
    private const int OverrideZTestAlways = 8;

    private int? _savedRenderQueue;
    private int? _savedZWrite;
    private int? _savedZTest;
    private bool _sortOrderOverridden;

    public MaterialHandle(Material? source) : this(source, true) { }

    private MaterialHandle(Material? source, bool isOwned)
    {
        _material = source;
        _isOwned = isOwned;
        _snapshot = source != null ? new Material(source) : null;
    }

    public static MaterialHandle Borrow(Material source) => new(source, false);

    public virtual void Revert()
    {
        if (_snapshot == null) return;

        if (_isOwned) _material?.TryDestroyImmediate();
        Material = new Material(_snapshot);
    }

    public void RefreshSnapshot(bool disposeOldSnapshot = true)
    {
        if (disposeOldSnapshot) _snapshot?.TryDestroyImmediate();
        _snapshot = new Material(Material);
    }

    public void ApplySortOrder()
    {
        if (_material is not { } mat) return;

        if (!_sortOrderOverridden)
        {
            _savedRenderQueue = mat.renderQueue;
            if (mat.HasProperty(ShaderUtils.ZWriteId)) _savedZWrite = mat.GetInt(ShaderUtils.ZWriteId);
            if (mat.HasProperty(ShaderUtils.ZTestId)) _savedZTest = mat.GetInt(ShaderUtils.ZTestId);
            _sortOrderOverridden = true;
        }

        mat.renderQueue = OverrideRenderQueue;
        if (mat.HasProperty(ShaderUtils.ZWriteId)) mat.SetInt(ShaderUtils.ZWriteId, OverrideZWriteOff);
        if (mat.HasProperty(ShaderUtils.ZTestId)) mat.SetInt(ShaderUtils.ZTestId, OverrideZTestAlways);
    }

    public void RevertSortOrder()
    {
        if (!_sortOrderOverridden) return;

        if (_material is { } mat)
        {
            if (_savedRenderQueue.HasValue) mat.renderQueue = _savedRenderQueue.Value;
            if (_savedZWrite.HasValue && mat.HasProperty(ShaderUtils.ZWriteId)) mat.SetInt(ShaderUtils.ZWriteId, _savedZWrite.Value);
            if (_savedZTest.HasValue && mat.HasProperty(ShaderUtils.ZTestId)) mat.SetInt(ShaderUtils.ZTestId, _savedZTest.Value);
        }

        _savedRenderQueue = null;
        _savedZWrite = null;
        _savedZTest = null;
        _sortOrderOverridden = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_isOwned) _material?.TryDestroyImmediate();
        _material = null;
        _snapshot?.TryDestroyImmediate();
        _snapshot = null;
    }
}

internal sealed class RendererBoundMaterialHandle : MaterialHandle
{
    private readonly Renderer _renderer;
    private readonly int _slotIndex;

    public RendererBoundMaterialHandle(Material material, Renderer renderer, int slotIndex)
    : base(material)
    {
        _renderer = renderer;
        _slotIndex = slotIndex;
    }

    public override void Revert()
    {
        base.Revert();
        if (_renderer == null) return;
        var materials = _renderer.sharedMaterials;
        if (_slotIndex >= materials.Length) return;
        materials[_slotIndex] = Material;
        _renderer.sharedMaterials = materials;
    }
}