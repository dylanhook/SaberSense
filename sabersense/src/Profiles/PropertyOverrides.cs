// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Persistence;

namespace SaberSense.Profiles;

public sealed class TransformOverrides
{
    private const string KeyScale = "scale";
    private const string KeyOffset = "offset";
    private const string KeyRotation = "rotation";

    public float Scale { get; set; } = 1f;
    public float Offset { get; set; }
    public float RotationDeg { get; set; }

    public void ReadFrom(JObject obj, IJsonProvider jsonProvider)
    {
        Scale = SaberScale.Clamp(obj.Value<float?>(KeyScale) ?? 1f);
        Offset = obj.Value<float?>(KeyOffset) ?? 0f;
        RotationDeg = obj.Value<float?>(KeyRotation) ?? 0f;
    }

    public JToken WriteTo(IJsonProvider jsonProvider)
    {
        return new JObject
        {
            [KeyScale] = Scale,
            [KeyOffset] = Offset,
            [KeyRotation] = RotationDeg
        };
    }
}