// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using HMUI;
using SaberSense.Catalog;
using SaberSense.Configuration;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using SaberSense.GUI.Framework.Core;
using SaberSense.GUI.Menu.Popups;
using SaberSense.GUI.Menu.Tabs;
using SaberSense.Rendering.Materials;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using VRUIControls;
using Zenject;

namespace SaberSense.GUI.Menu;

public class SaberSenseMenuView : MonoBehaviour
{
    private const int ReferencePixelsPerUnit = 10;
    private const float CanvasScaleFactor = 3.44f;
    private const float CanvasWidth = 120f;
    private const float CanvasHeight = 80f;
    private const float OutlineLayerInset = 0.15f;

    public Action? OnCloseRequested;

    private GameObject? _canvasGO;
    private GameObject[] _tabs = null!;
    private readonly NavBarBuilder _navBar = new();
    private PhysicsRaycasterWithCache _physicsRaycaster = null!;
    private SaberCatalog _catalog = null!;

    private MaterialEditorPopup _materialEditor = null!;
    private ChooseTrailPopup _chooseTrailPopup = null!;
    private MessagePopup _messagePopup = null!;

    private MenuControllerFactory _factory = null!;
    private TrailMaterialSynchronizer _trailSync = null!;
    private MenuEventWiring _eventWiring = null!;
    private IModLogger _log = null!;
    private IPA.Loader.PluginMetadata _metadata = null!;
    private ShaderRegistry _shaderRegistry = null!;

    private SaberSense.Rendering.Shaders.ShaderIntrospector _shaderCache = null!;
    private TextureCacheRegistry _textureRegistry = null!;
    private OriginalMaterialCache _originalCache = null!;
    private ModSettings _settings = null!;

    private MenuBundle _bundle = null!;

    [Inject]
    internal void Construct(
    MenuControllerFactory factory,
    TrailMaterialSynchronizer trailSync,
    MenuEventWiring eventWiring,
    IModLogger log,
    [Inject(Id = nameof(SaberSense))] IPA.Loader.PluginMetadata metadata,
    ShaderRegistry shaderRegistry,
    SaberSense.Rendering.Shaders.ShaderIntrospector shaderCache,
    TextureCacheRegistry textureRegistry,
    OriginalMaterialCache originalCache,
    ModSettings settings)
    {
        _factory = factory;
        _trailSync = trailSync;
        _eventWiring = eventWiring;
        _log = log.ForSource(nameof(SaberSenseMenuView));
        _metadata = metadata;
        _shaderRegistry = shaderRegistry;
        _shaderCache = shaderCache;
        _textureRegistry = textureRegistry;
        _originalCache = originalCache;
        _settings = settings;
    }

    public void Init(PhysicsRaycasterWithCache physicsRaycaster, SaberCatalog catalog)
    {
        _physicsRaycaster = physicsRaycaster;
        _catalog = catalog;
    }

    public void Start()
    {
        UIMaterials.Initialize(_shaderRegistry);
        var dirManager = new FolderNavigator(_catalog.ExternalSearchPaths);
        _bundle = _factory.CreateAll(_catalog, dirManager, _log);
        var canvasRect = SetupCanvas();
        BuildLayout(canvasRect);
        _eventWiring.Wire(
        _bundle.Preview,
        _bundle.Catalog,
        _bundle.Tabs,
        _catalog!,
        this);
    }

    private RectTransform SetupCanvas()
    {
        _canvasGO = new GameObject("SaberSenseCanvas");
        var canvasRect = _canvasGO.AddComponent<RectTransform>();
        _canvasGO.transform.SetParent(this.transform, false);
        _canvasGO.transform.localPosition = Vector3.zero;

        _materialEditor = new MaterialEditorPopup(canvasRect, _shaderCache, _textureRegistry[TextureCategory.Trail], _originalCache, _log,
        onClose: () => { },
        onPropertyChanged: mat => _trailSync.OnTrailPropertyChanged(mat),
        onCommit: mat => _trailSync.OnTrailCommit(mat));
        _messagePopup = new MessagePopup("Notice", canvasRect);
        _chooseTrailPopup = new ChooseTrailPopup(canvasRect, _catalog, _messagePopup);

        var canvas = _canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = UIZLayer.MainCanvas;
        var canvasScaler = _canvasGO.AddComponent<CanvasScaler>();
        canvasScaler.referencePixelsPerUnit = ReferencePixelsPerUnit;
        canvasScaler.scaleFactor = CanvasScaleFactor;
        if (_physicsRaycaster != null)
        {
            var vrgr = _canvasGO.AddComponent<VRGraphicRaycaster>();
            VRRaycasterHelper.SetPhysicsRaycaster(vrgr, _physicsRaycaster);
        }
        _canvasGO.AddComponent<CanvasRenderer>();
        _canvasGO.AddComponent<CurvedCanvasSettings>().SetRadius(0f);
        canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);

        var blockerGO = new GameObject("RaycastBlocker");
        blockerGO.transform.SetParent(canvasRect, false);
        var blockerR = blockerGO.AddComponent<RectTransform>();
        blockerR.anchorMin = Vector2.zero;
        blockerR.anchorMax = Vector2.one;
        blockerR.sizeDelta = Vector2.zero;
        var blockerImg = blockerGO.AddComponent<Image>();
        blockerImg.color = new Color(0, 0, 0, 0);
        blockerImg.raycastTarget = true;

        UILayoutFactory.BuildKaabaOutline(canvasRect, "Border");

        return canvasRect;
    }

    private void BuildLayout(RectTransform canvasRect)
    {
        var panelBg = new UIImage("PanelBg").SetColor(UITheme.Surface);
        panelBg.RectTransform.SetParent(canvasRect, false);
        panelBg.SetAnchors(Vector2.zero, Vector2.one);
        float inset = OutlineLayerInset * 6;
        panelBg.RectTransform.offsetMin = new Vector2(inset, inset);
        panelBg.RectTransform.offsetMax = new Vector2(-inset, -inset);
        NavBarBuilder.BuildRainbowBar(panelBg.RectTransform);

        _bundle.Preview.BuildPreviewWindow(canvasRect, _physicsRaycaster, _settings);
        _bundle.Console.BuildConsoleWindow(canvasRect, _physicsRaycaster);
        _bundle.SplitPopup.CanvasRoot = canvasRect;
        var modifierTab = _bundle.Tabs.OfType<IModifierTab>().FirstOrDefault();
        _bundle.SplitPopup.OnPropertyChanged = () => { modifierTab?.RefreshMaterials(); };

        var mainContainer = new VBox("MainContainer").SetParent(panelBg.RectTransform);
        mainContainer.SetAnchors(Vector2.zero, Vector2.one);
        mainContainer.RectTransform.sizeDelta = Vector2.zero;
        mainContainer.RectTransform.anchoredPosition = Vector2.zero;
        Destroy(mainContainer.GameObject.GetComponent<ContentSizeFitter>());
        mainContainer.SetPadding(UITheme.PanelPad, UITheme.PanelPad, UITheme.PanelPad, 0).SetSpacing(0f);

        BuildHeader(mainContainer.RectTransform);

        var contentArea = new HBox("ContentArea").SetParent(mainContainer.RectTransform).SetAlignment(TextAnchor.UpperLeft);
        Destroy(contentArea.GameObject.GetComponent<ContentSizeFitter>());
        contentArea.SetSpacing(0f).AddLayoutElement(flexibleHeight: 1);
        _navBar.Build(contentArea.RectTransform, _bundle.Tabs);

        var tabContainer = new VBox("TabContainer").SetParent(contentArea.RectTransform).SetAlignment(TextAnchor.UpperLeft);
        Destroy(tabContainer.GameObject.GetComponent<ContentSizeFitter>());
        tabContainer.LayoutGroup.childForceExpandHeight = true;
        tabContainer.SetPadding(0, 0, 0, 0).SetSpacing(0f);
        tabContainer.AddLayoutElement(flexibleWidth: 1, flexibleHeight: 1);

        var tabContext = new MenuTabContext
        {
            Parent = tabContainer.RectTransform,
            CanvasRoot = canvasRect,
            MessagePopup = _messagePopup,
            PreviewWindowGO = _bundle.Preview.WindowGO!,
            MaterialEditor = _materialEditor,
            ChooseTrailPopup = _chooseTrailPopup,
        };
        var tabs = _bundle.Tabs;
        _tabs = new GameObject[tabs.Count];
        for (int i = 0; i < tabs.Count; i++)
        _tabs[i] = tabs[i].Build(tabContext);

        _navBar.SetTabs(_tabs);
        _navBar.SwitchTab(0);
    }

    private void BuildHeader(RectTransform parent)
    {
        var headerRow = new HBox("HeaderRow").SetParent(parent);
        headerRow.SetSpacing(UITheme.HeaderSpacing).AddLayoutElement(minHeight: UITheme.HeaderHeight, preferredHeight: UITheme.HeaderHeight, flexibleHeight: 0);
        var titleDot = new UIImage("TitleDot").SetColor(UITheme.Accent).SetParent(headerRow.RectTransform)
        .AddLayoutElement(preferredWidth: UITheme.AccentBarWidth, preferredHeight: UITheme.LabelHeight);
        UITheme.TrackAccent(titleDot.ImageComponent);
        new UILabel("Header", "SaberSense").SetFontSize(UITheme.FontNormal).SetColor(UITheme.TextPrimary)
        .SetAlignment(TMPro.TextAlignmentOptions.Left).SetParent(headerRow.RectTransform).AddLayoutElement(flexibleWidth: 1);
        new UILabel("Version", $"v{_metadata?.HVersion?.ToString() ?? "?"}").SetFontSize(UITheme.FontSmall).SetColor(UITheme.TextVersion)
        .SetParent(headerRow.RectTransform).AddLayoutElement(preferredWidth: UITheme.VersionLabelWidth);
        var closeBtn = new BaseButton("X", false).SetParent(headerRow.RectTransform)
        .AddLayoutElement(preferredWidth: UITheme.HeaderHeight, minWidth: UITheme.HeaderHeight, flexibleWidth: 0);
        closeBtn.OnClick = () => { OnCloseRequested?.Invoke(); Destroy(_canvasGO); };
        closeBtn.Label.SetFontSize(UITheme.FontSmall).SetColor(UITheme.CloseButton);
    }

    private void Update()
    {
        _bundle?.Preview?.SaberPreview?.Tick();
        _bundle?.Preview?.Tick();
    }

    private static void OnDisable() { }

    private void OnDestroy()
    {
        try
        {
            UITheme.ClearAccentTracking();
            VectorSpriteGenerator.ClearCache();
            UIGradient.ClearCache();
            UICollapsibleSection.ResetExpandedState();
            UIFocusManager.Instance.Reset();
            _eventWiring?.Dispose();
            _bundle?.TrailPreviewer?.Destroy();
            _bundle?.Preview?.Dispose();
            _bundle?.Console?.Dispose();
            _bundle?.Selection?.Dispose();
            if (_bundle?.Tabs is { } tabs)
            foreach (var tab in tabs) tab.Dispose();
            _materialEditor?.Close();
            _bundle?.TextureBuilder?.Cleanup();
            _chooseTrailPopup?.Exit();
        }
        catch (Exception ex)
        {
            _log?.Error($"OnDestroy failed: {ex}");
        }
    }
}