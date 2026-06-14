// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using IPA.Loader;
using SaberSense.App;
using SaberSense.AssetPipeline;
using SaberSense.AssetPipeline.Formats.Saber;
using SaberSense.AssetPipeline.Formats.Whacker;
using SaberSense.Catalog;
using SaberSense.Catalog.Model;
using SaberSense.Configuration;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Core.Utilities;
using SaberSense.Gameplay;
using SaberSense.Persistence;
using SaberSense.Profiles;
using SaberSense.Rendering;
using SaberSense.Rendering.Shaders;
using Zenject;
using Logger = IPA.Logging.Logger;

namespace SaberSense.Installers;

internal sealed class CoreInstaller : Installer
{
    private readonly PluginMetadata _metadata;
    private readonly IModLogger _rootLog;
    private readonly IModLogger _log;
    private readonly LogFileWriter _fileWriter;
    private readonly LogRingBuffer _ringBuffer;

    CoreInstaller(Logger logger, PluginMetadata metadata, IModLogger log,
    LogFileWriter fileWriter, LogRingBuffer ringBuffer)
    {
        _metadata = metadata;
        _rootLog = log;
        _log = log.ForSource(nameof(CoreInstaller));
        _fileWriter = fileWriter;
        _ringBuffer = ringBuffer;
    }

    public override void InstallBindings()
    {
        Container.BindInstance<IModLogger>(_rootLog).AsSingle();
        Container.BindInstance(_fileWriter).AsSingle();
        Container.BindInstance(_ringBuffer).AsSingle();

        var rtOptions = new RuntimeEnvironment
        {
            IsDesktopMode = RuntimeEnvironment.IsFpfcActive
        };
        Container.BindInstance(rtOptions).AsSingle();
        var paths = new AppPaths();
        Container.BindInstance(paths).AsSingle();
        Container.BindInstance(_metadata).WithId(nameof(SaberSense)).AsCached();

        var internalConfig = new InternalConfig(paths, _log);
        internalConfig.Load();
        Container.BindInstance(internalConfig).AsSingle();

        var settings = new ModSettings();
        ModSettingsSideEffects.Bind(settings);
        Container.BindInstance(settings).AsSingle();

        var broker = new MessageBroker(_log);
        _ringBuffer.SetBroker(broker);

        Container.BindInterfacesAndSelfTo<MessageBroker>().FromInstance(broker).AsSingle();
        Container.BindInterfacesAndSelfTo<Serializer>().AsSingle();
        Container.Bind<ShaderIntrospector>().AsSingle();
        Container.Bind<ShaderRegistry>().AsSingle().WithArguments(_log);

        Container.Bind<SaberAssetBuilder>().AsSingle();
        Container.Bind<SaberBundleParser>().AsSingle().WithArguments(_log);
        Container.Bind<TextureCacheRegistry>().AsSingle();
        Container.Bind<ISaberLoader>().To<SaberBundleLoader>().AsSingle();
        Container.Bind<ISaberLoader>().To<WhackerBundleLoader>().AsSingle();
        Container.BindInterfacesAndSelfTo<SaberCatalog>().AsSingle()
        .OnInstantiated<SaberCatalog>(OnCatalogReady);
        Container.BindInterfacesAndSelfTo<DefaultSaberProvider>().AsSingle();
        Container.Bind<SaberProfile>().WithId(SaberHand.Left).AsCached().WithArguments(SaberHand.Left);
        Container.Bind<SaberProfile>().WithId(SaberHand.Right).AsCached().WithArguments(SaberHand.Right);
        Container.Bind<LiveSaberRegistry>().AsSingle();
        Container.Bind<SaberLoadout>().AsSingle();
        Container.BindInterfacesAndSelfTo<HotReloadWatcher>().AsSingle();
        Container.Bind<SelectionRandomizer>().AsSingle();
        Container.BindInterfacesAndSelfTo<ClashMaterialOverlay>().AsSingle();
        Container.Bind<PinTracker>().AsSingle();
        Container.Bind<SessionController>().AsSingle();
        Container.Bind<SaberSense.Loadout.LoadoutCoordinator>().AsSingle();

        Container.Bind<IMultiPassProvider>().To<PluginMultiPassProvider>().AsSingle();

        Container.Bind<SaberSense.Loadout.SaberCompatibilityPolicy>().AsSingle();
        Container.Bind<SaberSense.Loadout.AssetRecoveryService>().AsSingle();

        Container.BindInterfacesAndSelfTo<SaberSense.Services.ConfigManager>().AsSingle()
        .OnInstantiated<SaberSense.Services.ConfigManager>(OnConfigManagerReady);

        Container.BindInterfacesAndSelfTo<SaberSense.Rendering.Materials.SharedMaterialPool>().AsSingle();

        Container.BindFactory<SaberProfile, SaberSense.Rendering.Materials.MaterialPoolOwner, LiveSaber, LiveSaber.Factory>();
        Container.Bind<ISaberEffect>().To(typeof(DefaultSaberFinalizer)).AsSingle();
        Container.BindFactory<SaberAssetDefinition, SaberAssetRenderer, SaberAssetRenderer.Factory>();
        Container.BindFactory<LoadedBundle, SaberAssetDefinition, SaberAssetDefinition.Factory>();
    }

    private void OnCatalogReady(InjectContext ctx, SaberCatalog catalog)
    {
        ErrorBoundary.FireAndForget(catalog.PreparePreviewsAsync(), _log, nameof(OnCatalogReady));
    }

    private void OnConfigManagerReady(InjectContext ctx, SaberSense.Services.ConfigManager configManager)
    {
        ErrorBoundary.FireAndForget(configManager.InitializeLoadoutAsync(), _log, nameof(OnConfigManagerReady));
    }
}