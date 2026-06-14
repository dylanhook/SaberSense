// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.App;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SaberSense.AssetPipeline;

internal static class SaberRouteDiscovery
{
    public static async IAsyncEnumerable<SaberRoute> ByExtension(AppPaths dirs, string searchPattern)
    {
        await Task.CompletedTask;
        foreach (var file in dirs.SaberRoot.EnumerateFiles(searchPattern, SearchOption.AllDirectories))
        yield return new SaberRoute(file.FullName);
    }
}