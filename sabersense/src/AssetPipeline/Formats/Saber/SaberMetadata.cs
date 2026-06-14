// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.AssetPipeline.Formats.Saber;

public sealed class SaberMetadata
{
    public string Name { get; }
    public string Author { get; }
    public string Description { get; }
    public Sprite? CoverImage { get; }

    public SaberMetadata(string name, string author, string description, Sprite? coverImage)
    {
        Name = name ?? "Custom Saber";
        Author = author ?? "Unknown";
        Description = description ?? string.Empty;
        CoverImage = coverImage;
    }

    public static SaberMetadata Unknown { get; } = new("Custom Saber", "Unknown", string.Empty, null);
}