// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Linq;

namespace SaberSense.Configuration;

internal sealed class RuntimeEnvironment
{
    public bool IsDesktopMode { get; set; }

    public static bool IsFpfcActive =>
    Environment.GetCommandLineArgs().Any(x => x.Equals("fpfc", StringComparison.OrdinalIgnoreCase));
}

internal enum SaberPipeline
{
    None,
    SaberSense,
    SaberAsset
}