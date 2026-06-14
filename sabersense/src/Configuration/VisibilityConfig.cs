// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Gameplay;
using SaberSense.GUI.Framework.Core;
using System.Collections.Generic;

namespace SaberSense.Configuration;

internal sealed class VisibilityConfig : BindableSettings
{
    private List<int> _desktop = ViewFeatureRegistry.GetDefaults(ViewType.Desktop);
    public List<int> Desktop { get => _desktop; set => SetField(ref _desktop, value); }

    private List<int> _hmd = ViewFeatureRegistry.GetDefaults(ViewType.Hmd);
    public List<int> Hmd { get => _hmd; set => SetField(ref _hmd, value); }
}