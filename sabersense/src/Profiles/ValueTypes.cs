// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.Profiles;

[Newtonsoft.Json.JsonObject]
public sealed class SaberScale
{
    public const float MinScale = 0f;

    public const float MaxScale = 10f;

    public static float Clamp(float value) => Mathf.Clamp(value, MinScale, MaxScale);

    public static SaberScale Unit => new();
    public float Length { get; set; } = 1f;
    public float Width { get; set; } = 1f;
}