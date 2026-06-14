// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.AssetPipeline.Formats.Saber;

[DisallowMultipleComponent]
internal sealed class SaberDescriptor : MonoBehaviour
{
    internal string SaberName = "saber";
    internal string AuthorName = "author";
    internal string Description = string.Empty;
    internal Sprite? CoverImage;
}