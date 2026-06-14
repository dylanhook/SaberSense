// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.App;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SaberSense.Catalog;

internal sealed class TextureCacheRegistry : IDisposable
{
    private readonly Dictionary<TextureCategory, TextureCache> _caches;
    private readonly IModLogger _log;

    public TextureCacheRegistry(AppPaths dirs, IModLogger log)
    {
        _log = log.ForSource(nameof(TextureCacheRegistry));
        _caches = new()
        {
            [TextureCategory.Trail] = new(dirs, "Trail", log),
            [TextureCategory.Saber] = new(dirs, "Saber", log),
        };
    }

    public TextureCache this[TextureCategory category] => _caches[category];

    public async Task<CachedTexture?> ResolveAnyAsync(string name)
    {
        foreach (var cache in _caches.Values)
        {
            try
            {
                await cache.WaitForFinishAsync();
                var result = await cache.RetrieveTexture(name);
                if (result is not null) return result;
            }
            catch (Exception ex)
            {
                _log.Warn($"cache lookup failed: {ex.Message}");
            }
        }
        return null;
    }

    public UnityEngine.Texture2D? FindByName(string textureName)
    {
        if (string.IsNullOrEmpty(textureName)) return null;
        foreach (var cache in _caches.Values)
        {
            var result = cache.FindByName(textureName);
            if (result != null) return result;
        }
        return null;
    }

    public void Dispose()
    {
        foreach (var cache in _caches.Values)
        cache.Dispose();
    }
}