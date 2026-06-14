// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.AssetPipeline;

namespace SaberSense.Catalog.Model;

internal interface IAssetParser
{
    SaberAssetEntry? ParseAsset(LoadedBundle bundle);
}