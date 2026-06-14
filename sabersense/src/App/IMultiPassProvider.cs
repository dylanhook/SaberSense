// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

namespace SaberSense.App;

internal interface IMultiPassProvider
{
    bool IsMultiPassEnabled { get; }
}

internal sealed class PluginMultiPassProvider : IMultiPassProvider
{
    public bool IsMultiPassEnabled => Plugin.MultiPassEnabled;
}