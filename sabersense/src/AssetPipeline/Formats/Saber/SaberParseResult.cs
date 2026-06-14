// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;

namespace SaberSense.AssetPipeline.Formats.Saber;

public sealed class SaberParseResult
{
    public SaberMetadata Metadata { get; }

    public IReadOnlyList<TrailData> Trails { get; }

    internal IReadOnlyDictionary<long, string> PathIdToGameObjectName { get; }

    internal IReadOnlyDictionary<long, string> PathIdToMaterialName { get; }

    public CoverImageData? CoverImage { get; }

    public IReadOnlyList<ModifierPayload> Modifiers { get; }

    public IReadOnlyList<SpringBoneEntry> SpringBones { get; }

    public IReadOnlyList<SpringColliderEntry> SpringColliders { get; }

    public (float minZ, float maxZ)? ParsedBounds { get; }

    internal IReadOnlyDictionary<long, long>? TransformToGameObjectPathId { get; }

    public EventParseData? Events { get; }

    public bool HasEvents => Events?.HasEvents == true;

    public SaberParseResult(
    SaberMetadata metadata,
    IReadOnlyList<TrailData> trails,
    IReadOnlyDictionary<long, string> pathIdToGameObjectName,
    IReadOnlyDictionary<long, string> pathIdToMaterialName,
    CoverImageData? coverImage = null,
    IReadOnlyList<ModifierPayload>? modifiers = null,
    (float minZ, float maxZ)? parsedBounds = null,
    IReadOnlyList<SpringBoneEntry>? springBones = null,
    IReadOnlyList<SpringColliderEntry>? springColliders = null,
    IReadOnlyDictionary<long, long>? transformToGameObjectPathId = null,
    EventParseData? events = null)
    {
        Metadata = metadata;
        Trails = trails;
        PathIdToGameObjectName = pathIdToGameObjectName;
        PathIdToMaterialName = pathIdToMaterialName;
        CoverImage = coverImage;
        Modifiers = modifiers ?? [];
        SpringBones = springBones ?? [];
        SpringColliders = springColliders ?? [];
        ParsedBounds = parsedBounds;
        TransformToGameObjectPathId = transformToGameObjectPathId;
        Events = events;
    }
}

public sealed class CoverImageData
{
    public int Width { get; }
    public int Height { get; }

    public int Format { get; }
    public byte[] RawData { get; }

    public CoverImageData(int width, int height, int format, byte[] rawData)
    {
        Width = width;
        Height = height;
        Format = format;
        RawData = rawData;
    }
}