// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Customization;
using SaberSense.Gameplay;
using SaberSense.GUI;
using SaberSense.GUI.Menu;
using SaberSense.Patches;
using Zenject;

namespace SaberSense.Installers;

internal sealed class MenuInstaller : Installer
{
    public override void InstallBindings()
    {
        Container.BindInterfacesAndSelfTo<Customization.SaberEditor>().AsSingle()
        .OnInstantiated<Customization.SaberEditor>((_, editor) => HarmonyBridge.Editor = editor);
        Container.BindInterfacesAndSelfTo<SaberSenseMenuButton>().AsSingle()
        .OnInstantiated<SaberSenseMenuButton>((_, btn) => HarmonyBridge.MenuButton = btn);
        Container.Bind<PreviewSession>().AsSingle();
        Container.Bind<EditScope>().AsSingle();
        Container.Bind<GripAttachment>().AsSingle();

        Container.BindInterfacesAndSelfTo<TrailVisualizationRenderer>().AsSingle();
        Container.Bind<ViewVisibilityService>().AsSingle();
        Container.BindInterfacesAndSelfTo<WorldModulationController>().AsSingle();

        Container.BindInterfacesAndSelfTo<MenuCameraRegistrator>().AsSingle();

        Container.Bind<Rendering.Materials.MaterialSyncService>().AsSingle();
        Container.Bind<Rendering.Materials.MaterialOverrideService>().AsSingle();

        Container.BindInterfacesAndSelfTo<Rendering.Materials.OriginalMaterialCache>().AsSingle();
        Container.Bind<Loadout.SaberCatalogService>().AsSingle();

        Container.BindInterfacesAndSelfTo<Catalog.CoverGenerationService>().AsSingle();

        Container.Bind<MenuControllerFactory>().AsSingle();
        Container.Bind<TrailMaterialSynchronizer>().AsSingle();
        Container.Bind<MenuEventWiring>().AsSingle();
    }
}