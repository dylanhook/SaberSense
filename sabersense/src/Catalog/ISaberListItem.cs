// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.Catalog;

public interface ISaberListItem
{
    string DisplayName { get; }

    bool IsPinned { get; }

    string CreatorName { get; }

    Sprite? CoverImage { get; }

    bool IsSPICompatible { get; }
}