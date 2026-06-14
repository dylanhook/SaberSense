// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.App;
using SaberSense.Core.Utilities;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.AssetPipeline;

public readonly record struct PreviewData(
string DisplayName,
string CreatorName,
Sprite? CoverSprite,
bool IsSPICompatible,
long FileSize,
string FileLastModified,
string? ContentHash);

public interface ISaberLoader
{
    string HandledExtension { get; }
    IAsyncEnumerable<SaberRoute> DiscoverAsync(AppPaths dirs);
    Task<LoadedBundle?> LoadAsync(string relativePath);

    Task<PreviewData?> ExtractPreviewAsync(string relativePath);
}

public readonly record struct SaberRoute
{
    public readonly string FullPath;
    public readonly string RelativePath;
    public readonly string SubFolder;

    public SaberRoute(string fullPath)
    {
        FullPath = fullPath;
        RelativePath = AssetPaths.MakeRelative(fullPath);
        SubFolder = AssetPaths.GetSubfolderPath(RelativePath);
    }
}