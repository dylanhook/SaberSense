// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;
using System.Collections.Generic;

namespace SaberSense.Catalog;

internal sealed class PinTracker
{
    private readonly InternalConfig _internalConfig;
    private HashSet<string> _index = [];

    public PinTracker(InternalConfig internalConfig)
    {
        _internalConfig = internalConfig;
        Rebuild();
    }

    public bool Contains(string path) => _index.Contains(path);

    public void Toggle(string path)
    {
        if (!_index.Remove(path))
        {
            _index.Add(path);
            _internalConfig.PinnedSabers.Add(path);
        }
        else
        {
            _internalConfig.PinnedSabers.Remove(path);
        }
        _internalConfig.Save();
    }

    public void Rebuild() => _index = [.. _internalConfig.PinnedSabers];
}