// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Core.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;
using VRUIControls;
using Zenject;
using Object = UnityEngine.Object;

namespace SaberSense.GUI;

internal sealed class SaberSenseMenuButton : IInitializable, IDisposable
{
    private static readonly Vector3 MenuWorldPosition = new(0f, 1.1f, 2.6f);
    private static readonly Vector3 MenuWorldScale = new(0.02f, 0.02f, 0.02f);

    private readonly HMUI.HierarchyManager _hierarchyManager;
    private readonly PhysicsRaycasterWithCache _physicsRaycaster;
    private readonly SaberCatalog _catalog;
    private readonly DiContainer _container;
    private readonly SaberSense.Configuration.ModSettings _settings;
    private readonly IModLogger _log;
    private BeatSaberMarkupLanguage.MenuButtons.MenuButton? _menuButton;
    private GameObject? _menuViewInstance;
    private bool _isOpen;
    private readonly List<GameObject> _deactivatedScreens = [];

    public SaberSenseMenuButton(
    HMUI.HierarchyManager hierarchyManager,
    PhysicsRaycasterWithCache physicsRaycaster,
    SaberCatalog catalog,
    SaberSense.Configuration.ModSettings settings,
    DiContainer container,
    IModLogger log)
    {
        _hierarchyManager = hierarchyManager;
        _physicsRaycaster = physicsRaycaster;
        _catalog = catalog;
        _settings = settings;
        _container = container;
        _log = log.ForSource(nameof(SaberSenseMenuButton));
    }

    public void Initialize()
    {
        SaberSense.Patches.SabersTabPatch.TabSlot = _settings.ShowGameplayButton ? 4 : null;
        _menuButton = new BeatSaberMarkupLanguage.MenuButtons.MenuButton("SaberSense", "Get Good, Get SaberSense", ShowMenu);
        BeatSaberMarkupLanguage.MenuButtons.MenuButtons.Instance.RegisterButton(_menuButton);
    }

    public void Dispose()
    {
        try
        {
            if (Patches.HarmonyBridge.MenuButton == this)
            Patches.HarmonyBridge.MenuButton = null;
            Patches.SabersTabPatch.TabSlot = null;
            if (_menuButton is not null)
            BeatSaberMarkupLanguage.MenuButtons.MenuButtons.Instance.UnregisterButton(_menuButton);
        }
        catch (System.Exception ex)
        {
            _log?.Error($"Dispose failed: {ex}");
        }
    }

    public void ShowMenu()
    {
        if (_isOpen) return;
        _isOpen = true;

        if (_hierarchyManager.gameObject.GetComponent<HMUI.ScreenSystem>() is { } screenSystem)
        {
            _deactivatedScreens.Clear();
            DismissScreen(screenSystem.leftScreen);
            DismissScreen(screenSystem.mainScreen);
            DismissScreen(screenSystem.rightScreen);
            DismissScreen(screenSystem.bottomScreen);
            DismissScreen(screenSystem.topScreen);
        }

        _menuViewInstance = new GameObject("SaberSenseMenuHost");
        var hostTransform = _menuViewInstance.transform;
        hostTransform.SetParent(_hierarchyManager.transform, false);
        hostTransform.localPosition = MenuWorldPosition;
        hostTransform.localScale = MenuWorldScale;

        var menuView = _menuViewInstance.AddComponent<SaberSense.GUI.Menu.SaberSenseMenuView>();
        menuView.Init(_physicsRaycaster, _catalog);
        _container.Inject(menuView);
        menuView.OnCloseRequested = Close;
    }

    private void Close()
    {
        if (!_isOpen) return;
        _isOpen = false;

        if (_menuViewInstance)
        {
            Object.Destroy(_menuViewInstance);
            _menuViewInstance = null;
        }

        foreach (var screenObj in _deactivatedScreens)
        {
            if (screenObj) screenObj.SetActive(true);
        }
        _deactivatedScreens.Clear();
    }

    private void DismissScreen(HMUI.Screen screen)
    {
        if (screen?.gameObject is { activeSelf: true } go)
        {
            _deactivatedScreens.Add(go);
            go.SetActive(false);
        }
    }
}