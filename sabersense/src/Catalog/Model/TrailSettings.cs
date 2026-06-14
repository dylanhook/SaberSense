// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaberSense.Rendering;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Catalog.Model;

public readonly record struct TrailDimensions(int Length, float Width);

public sealed class TrailSettings
{
    public TrailDimensions OriginalDimensions { get; private set; }

    public Vector3 PositionOffset { get; set; }
    public int TrailLength { get; set; }
    public float TrailWidth { get; set; }

    public float SaberExtent { get; set; } = 1f;

    public float MaxTrailWidth { get; set; } = 1f;

    public float TrailEndZ { get; set; } = 1f;
    public float WhiteBlend { get; set; }
    public bool TextureClamped { get; set; }
    public bool Reversed { get; set; }
    public string? OriginAssetPath { get; set; }

    [JsonIgnore] public MaterialHandle? Material { get; set; }
    [JsonIgnore] public List<SaberTrailMarker>? OriginTrails { get; set; }

    [JsonIgnore] public JObject? DeferredMaterialJson { get; set; }
    public TextureWrapMode? NativeTextureWrap { get; set; }

    public TrailSettings(
    MaterialHandle material,
    int trailLength,
    float trailWidth,
    Vector3 positionOffset = default,
    float whiteBlend = 0f,
    TextureWrapMode? nativeWrap = null,
    string originPath = "")
    {
        Material = material;
        TrailLength = trailLength;
        TrailWidth = trailWidth;
        OriginalDimensions = new(trailLength, trailWidth);
        PositionOffset = positionOffset;
        WhiteBlend = whiteBlend;
        NativeTextureWrap = nativeWrap;
        OriginAssetPath = originPath;
        SaberExtent = 1f;
    }

    public TrailSettings() { }

    public float WidthPercent
    {
        get => MaxTrailWidth > 0f ? (TrailWidth / MaxTrailWidth) * 100f : 0f;
        set => TrailWidth = MaxTrailWidth > 0f ? (value / 100f) * MaxTrailWidth : 0f;
    }

    private const int MaxTrailFrames = 40;

    public float LengthPercent
    {
        get => (TrailLength / (float)MaxTrailFrames) * 100f;
        set => TrailLength = Mathf.Clamp(Mathf.RoundToInt((value / 100f) * MaxTrailFrames), 0, MaxTrailFrames);
    }

    public float OffsetPercent
    {
        get => SaberExtent > 0f ? (PositionOffset.z / SaberExtent) * 100f : 0f;
        set
        {
            var pos = PositionOffset;
            pos.z = SaberExtent > 0f ? (value / 100f) * SaberExtent : 0f;
            PositionOffset = pos;
        }
    }

    public TrailSettings CloneFrom(TrailSettings source)
    {
        CopyUserSettings(source);

        SaberExtent = source.SaberExtent;
        MaxTrailWidth = source.MaxTrailWidth;
        TrailEndZ = source.TrailEndZ;

        return this;
    }

    public TrailSettings CloneUserSettings(TrailSettings source)
    {
        CopyUserSettings(source);
        return this;
    }

    private static readonly System.Action<TrailSettings, TrailSettings>[] UserFieldCopies =
    [
    (dest, src) => dest.PositionOffset = src.PositionOffset,
    (dest, src) => dest.TrailWidth = src.TrailWidth,
    (dest, src) => dest.TrailLength = src.TrailLength,
    (dest, src) => dest.OriginalDimensions = src.OriginalDimensions,
    (dest, src) => dest.WhiteBlend = src.WhiteBlend,
    (dest, src) => dest.OriginAssetPath = src.OriginAssetPath,
    (dest, src) => dest.TextureClamped = src.TextureClamped,
    (dest, src) => dest.Reversed = src.Reversed,
    (dest, src) => dest.NativeTextureWrap = src.NativeTextureWrap,
    ];

    private void CopyUserSettings(TrailSettings source)
    {
        foreach (var copy in UserFieldCopies)
        copy(this, source);

        Material ??= new(null);
        if (source.Material?.Material != null)
        {
            var newMat = new Material(source.Material.Material);
            if (Material.IsOwned)
            Material.Material?.TryDestroyImmediate();
            Material.Material = newMat;
        }
    }

    public TrailSettings Clone() => new TrailSettings().CloneFrom(this);
}