// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.GUI.Menu.Builders;
using SaberSense.GUI.Menu.Controllers;
using SaberSense.GUI.Menu.Tabs;
using System.Collections.Generic;

namespace SaberSense.GUI.Menu;

internal sealed record MenuBundle(

SaberSelectionController Selection,
SaberCatalogController Catalog,
PreviewController Preview,
SplitPopupManager SplitPopup,
TexturePropertyBuilder TextureBuilder,
LogConsoleController Console,

IReadOnlyList<IMenuTab> Tabs,

SaberSense.GUI.TrailVisualizationRenderer TrailPreviewer
);