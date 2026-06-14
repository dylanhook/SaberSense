// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Rendering;
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.GUI.Menu.Tabs;

internal interface IMenuTab : IDisposable
{
    string Title { get; }

    string IconPath { get; }

    GameObject Build(MenuTabContext ctx);
}

internal interface IRefreshableTab
{
    void Refresh();
}

internal interface ISaberSelectorTab : IRefreshableTab
{
    void OnSaberPreviewInstantiated(LiveSaber liveSaber);

    void UpdateCellIcon(object userData, Sprite icon);

    Task ShowSabersAsync(bool scrollToTop = false);
}

internal interface ITrailTab : IRefreshableTab
{
}

internal interface IModifierTab
{
    void RefreshModifiers();

    void RefreshMaterials();
}