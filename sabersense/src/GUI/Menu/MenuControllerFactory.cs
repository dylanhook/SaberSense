// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.App;
using SaberSense.Catalog;
using SaberSense.Configuration;
using SaberSense.Core.Logging;
using SaberSense.Core.Messaging;
using SaberSense.Customization;
using SaberSense.GUI.Menu.Builders;
using SaberSense.GUI.Menu.Controllers;
using SaberSense.GUI.Menu.Tabs;
using SaberSense.Persistence;
using SaberSense.Profiles;
using SaberSense.Rendering;
using System.Collections.Generic;

namespace SaberSense.GUI.Menu;

internal sealed class MenuControllerFactory(
PreviewSession previewSession,
ModSettings settings,
SaberEditor editor,
EditScope editScope,
IMessageBroker broker,
Rendering.Materials.MaterialOverrideService overrideService,
Rendering.Materials.MaterialSyncService syncService,
Rendering.Materials.OriginalMaterialCache originalCache,
SaberSense.Rendering.Shaders.ShaderIntrospector shaderCache,
PlayerDataModel playerDataModel,
CoverGenerationService coverService,
LiveSaber.Factory liveSaberCreator,
Loadout.SaberCatalogService catalogService,
TextureCacheRegistry textureRegistry,
Serializer serializer,
SaberSense.GUI.TrailVisualizationRenderer trailPreviewer,
SaberLoadout loadout,
InternalConfig internalConfig,
AppPaths appPaths,
Services.IConfigStore configManager,
DefaultSaberProvider defaultSaberProvider,
LogRingBuffer ringBuffer,
LogFileWriter fileWriter)
{
    private readonly PreviewSession _previewSession = previewSession;
    private readonly ModSettings _settings = settings;
    private readonly SaberEditor _editor = editor;
    private readonly EditScope _editScope = editScope;
    private readonly IMessageBroker _broker = broker;
    private readonly Rendering.Materials.MaterialOverrideService _overrideService = overrideService;
    private readonly Rendering.Materials.MaterialSyncService _syncService = syncService;
    private readonly Rendering.Materials.OriginalMaterialCache _originalCache = originalCache;
    private readonly SaberSense.Rendering.Shaders.ShaderIntrospector _shaderCache = shaderCache;
    private readonly PlayerDataModel _playerDataModel = playerDataModel;
    private readonly CoverGenerationService _coverService = coverService;
    private readonly LiveSaber.Factory _liveSaberCreator = liveSaberCreator;
    private readonly Loadout.SaberCatalogService _catalogService = catalogService;
    private readonly TextureCacheRegistry _textureRegistry = textureRegistry;
    private readonly Serializer _serializer = serializer;
    private readonly SaberSense.GUI.TrailVisualizationRenderer _trailPreviewer = trailPreviewer;
    private readonly SaberLoadout _loadout = loadout;
    private readonly InternalConfig _internalConfig = internalConfig;
    private readonly AppPaths _appPaths = appPaths;
    private readonly Services.IConfigStore _configManager = configManager;
    private readonly DefaultSaberProvider _defaultSaberProvider = defaultSaberProvider;
    private readonly LogRingBuffer _ringBuffer = ringBuffer;
    private readonly LogFileWriter _fileWriter = fileWriter;

    public MenuBundle CreateAll(SaberCatalog catalog, FolderNavigator? dirManager, IModLogger viewLog)
    {
        var selection = new SaberSelectionController(_previewSession, _settings);
        selection.Init(catalog);

        var catalogCtrl = new SaberCatalogController(_catalogService, selection);
        catalogCtrl.Init(dirManager!);

        var transform = new SaberTransformController(_loadout, _previewSession, _editScope);
        var material = new MaterialEditingController(_overrideService, _syncService, _originalCache, _shaderCache, _loadout, _previewSession, _playerDataModel, _editScope);
        var preview = new PreviewController(_previewSession, _trailPreviewer, _editor, _settings, _playerDataModel, _coverService, _liveSaberCreator, _editScope, _broker, _previewSession.MaterialPool);
        var trail = new TrailSettingsController(_loadout, _previewSession, _editScope, _broker);
        var console = new LogConsoleController(_ringBuffer, _fileWriter, _broker);

        var splitPopup = new SplitPopupManager(material, selection, _serializer);
        var toggleBuilder = new TogglePropertyBuilder(material, splitPopup, selection, _serializer, _previewSession);
        var floatBuilder = new FloatPropertyBuilder(material, splitPopup, selection, _serializer, _previewSession);
        var colorBuilder = new ColorPropertyBuilder(material, splitPopup, selection, _serializer, _previewSession);
        var textureBuilder = new TexturePropertyBuilder(material, splitPopup, selection, _serializer, _previewSession, _textureRegistry[TextureCategory.Saber], viewLog);
        var rowBuilder = new MaterialPropertyRowBuilder(material, splitPopup, colorBuilder, floatBuilder, toggleBuilder, textureBuilder, _previewSession);

        var saberTab = new SaberTabView(selection, catalogCtrl, transform,
        preview, _settings, _previewSession, _trailPreviewer, _editor, catalog, _broker, viewLog);
        var trailTab = new TrailTabView(selection,
        _settings, _previewSession, trail, _trailPreviewer, catalog, _editor, _broker);
        var modifierTab = new ModifierTabView(selection, material, rowBuilder,
        _previewSession, _loadout, _broker, _serializer);
        var settingsTab = new SettingsTabView(_settings, _internalConfig, _appPaths, _broker, _defaultSaberProvider, _configManager, trail, transform, _previewSession, _loadout, viewLog);

        IReadOnlyList<Tabs.IMenuTab> tabs = [saberTab, trailTab, modifierTab, settingsTab];

        return new MenuBundle(
        Selection: selection,
        Catalog: catalogCtrl,
        Preview: preview,
        SplitPopup: splitPopup,
        TextureBuilder: textureBuilder,
        Console: console,
        Tabs: tabs,
        TrailPreviewer: _trailPreviewer);
    }
}