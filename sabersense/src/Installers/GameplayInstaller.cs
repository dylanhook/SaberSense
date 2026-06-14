// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;
using SaberSense.Gameplay;
using SaberSense.Profiles;

using SiraUtil.Sabers;
using Zenject;

namespace SaberSense.Installers;

internal sealed class GameplayInstaller : Installer
{
    private readonly ModSettings _settings;
    private readonly SaberLoadout _loadout;

    GameplayInstaller(ModSettings settings, SaberLoadout loadout)
    => (_settings, _loadout) = (settings, loadout);

    public override void InstallBindings()
    {
        Container.BindInterfacesTo<GameplaySessionInitializer>().AsSingle();

        if (_settings.WarningMarkerEnabled)
        {
            var prefab = new UnityEngine.GameObject("WarningMarkerPrefab");
            prefab.AddComponent<WarningMarkerBehavior>();
            prefab.SetActive(false);

            Container.BindMemoryPool<WarningMarkerBehavior, WarningMarkerBehavior.Pool>()
            .WithInitialSize(5)
            .FromComponentInNewPrefab(prefab)
            .UnderTransformGroup("SaberSense_WarningMarkers");

            UnityEngine.Object.Destroy(prefab);

            Container.BindInterfacesAndSelfTo<WarningMarkerManager>().AsSingle();
        }

        if (_settings.HidePlatform)
        {
            Container.BindInterfacesTo<PlatformHider>().AsSingle();
        }

        if (!_settings.IsActive || _loadout.IsEmpty)
        {
            Container.BindInterfacesAndSelfTo<WorldModulationController>().AsSingle();
            Container.Bind<ViewVisibilityService>().AsSingle();
            return;
        }

        Container.BindInstance(SaberModelRegistration.Create<SaberSenseModelController>(250));
        Container.BindInterfacesAndSelfTo<SaberReplacer>().AsSingle();

        if (_settings.EnableEventManager)
        Container.BindInterfacesAndSelfTo<SaberEventRouter>().AsTransient();
        Container.BindInterfacesAndSelfTo<WorldModulationController>().AsSingle();
        Container.Bind<ViewVisibilityService>().AsSingle();

        if (!Container.HasBinding<ObstacleSaberSparkleEffectManager>())
        {
            Container.Bind<ObstacleSaberSparkleEffectManager>()
            .FromMethod(ctx =>
            Container.TryResolve<PlayerSpaceConvertor>()
            ?.GetComponentInChildren<ObstacleSaberSparkleEffectManager>()!)
            .AsSingle();
        }
    }
}