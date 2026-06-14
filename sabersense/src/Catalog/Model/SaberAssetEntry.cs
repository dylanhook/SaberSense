// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core;
using System;
using UnityEngine;

namespace SaberSense.Catalog.Model;

public sealed class SaberAssetEntry : IDisposable, ISaberListItem
{
    public readonly AuxObjectManager AuxObjects;

    private readonly SaberAssetDefinition? _left;
    private readonly SaberAssetDefinition? _right;
    private SaberDisplayInfo _displayInfo;

    public string DisplayName => _displayInfo.Name ?? string.Empty;
    public string CreatorName => _displayInfo.Author ?? string.Empty;
    public Sprite? CoverImage => _displayInfo.Cover;
    public bool IsPinned => _displayInfo.IsPinned;
    public bool IsSPICompatible { get; set; } = true;

    public bool IsAssetStale => AuxObjects.IsStale;

    public SaberAssetDefinition? LeftPiece => _left;

    public SaberAssetDefinition? RightPiece => _right ?? _left;

    public SaberAssetDefinition? this[SaberHand hand] => hand == SaberHand.Left ? LeftPiece : RightPiece;

    private SaberAssetEntry(
    SaberAssetDefinition? left,
    SaberAssetDefinition? right,
    GameObject auxPrefab)
    {
        _left = left;
        _right = right;

        AuxObjects = new(auxPrefab, right?.Asset.Prefab);

        if (_left is not null)
        {
            _left.OwnerEntry = this;
            _left.AuxObjects = AuxObjects;
            _displayInfo = _left.GetDisplayInfo();
        }

        if (_right is not null)
        {
            _right.OwnerEntry = this;
            _right.AuxObjects = AuxObjects;
        }
    }

    public static SaberAssetEntry Create(
    SaberAssetDefinition? left,
    SaberAssetDefinition? right,
    GameObject auxPrefab) => new(left, right, auxPrefab);

    public void Dispose()
    {
        AuxObjects?.Destroy();

        try
        {
            if (_left is not null)
            {
                _left.Asset.Unload();
                _left.Dispose();
            }
        }
        finally
        {
            if (_right is not null)
            {
                _right.Asset.Unload();
                _right.Dispose();
            }
        }
    }

    public void SyncPiece(PieceDefinition editedPiece)
    {
        if (editedPiece != _left && editedPiece != _right)
        throw new System.ArgumentException("editedPiece must be either the Left or Right piece of this entry.", nameof(editedPiece));

        var counterpart = _left == editedPiece ? _right : _left;
        counterpart?.CloneStateFrom(editedPiece);
    }

    public void DestroyAuxObjects() => AuxObjects.Destroy();

    public void SetPinned(bool state) => _displayInfo = new(
    _displayInfo.Name!, _displayInfo.Author!, _displayInfo.Cover, state);
}