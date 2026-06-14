// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Rendering.Shaders;
using System;

namespace SaberSense.GUI.Menu.Builders;

internal enum MaterialControlKind { Toggle, Slider, Color, Texture, Skip }

internal readonly record struct MaterialControl(MaterialControlKind Kind, float Min = 0f, float Max = 0f);

internal static class MaterialEditorPolicy
{
    public static MaterialControl Classify(ShaderProperty prop)
    {
        if (prop.HasAttribute("MaterialToggle") || prop.Name == "_CustomColors")
        return new(MaterialControlKind.Toggle);

        return prop.Kind switch
        {
            PropertyKind.Range => new(MaterialControlKind.Slider, prop.RangeMin ?? 0f, prop.RangeMax ?? 1f),
            PropertyKind.Float => new(MaterialControlKind.Slider, 0f, 10f),
            PropertyKind.Color => new(MaterialControlKind.Color),
            PropertyKind.Texture => new(MaterialControlKind.Texture),
            PropertyKind.Vector => new(MaterialControlKind.Skip),
            _ => throw new NotSupportedException($"Unhandled PropertyKind {prop.Kind} classifying '{prop.Name}'.")
        };
    }
}