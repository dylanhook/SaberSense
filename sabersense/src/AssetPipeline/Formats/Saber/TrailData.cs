// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.AssetPipeline.Formats.Saber;

public sealed class TrailData
{
    public Transform? PointStart { get; set; }

    public Transform? PointEnd { get; set; }

    public Material? TrailMaterial { get; set; }

    public int ColorType { get; init; }

    public Color TrailColor { get; init; } = Color.white;

    public Color MultiplierColor { get; init; } = Color.white;

    public int Length { get; init; } = 20;

    internal long PointStartPathId { get; init; }
    internal long PointEndPathId { get; init; }
    internal long TrailMaterialPathId { get; init; }

    internal float? ParsedPointEndZ { get; set; }
    internal float? ParsedPointStartZ { get; set; }

    public float GetWidth()
    {
        if (PointStart == null || PointEnd == null) return 0f;
        return Mathf.Abs(PointStart.localPosition.z - PointEnd.localPosition.z);
    }
}