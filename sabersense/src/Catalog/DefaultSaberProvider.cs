// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.AssetPipeline;
using SaberSense.AssetPipeline.Formats.Saber;
using SaberSense.Catalog.Model;
using SaberSense.Configuration;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Gameplay;
using SaberSense.Rendering;
using System;
using System.Linq;
using UnityEngine;
using Zenject;
using static BGLib.UnityExtension.AddressablesExtensions;
using Object = UnityEngine.Object;

namespace SaberSense.Catalog;

internal sealed class DefaultSaberProvider(
ModSettings settings,
SaberAssetDefinition.Factory defFactory,
SaberCatalog catalog,
PinTracker pins,
IModLogger log) : IInitializable, IDisposable, IDefaultSaberProvider
{
    private const string SaberModelAddress = "Assets/Prefabs/Sabers/BasicSaberModel.prefab";
    private const int DefaultTrailLength = 20;

    public const string DefaultSaberPath = "$$DefaultSaber$$";

    public SaberAssetEntry? Entry { get; private set; }
    public AssetPreview? Preview { get; private set; }
    public GameObject? DefaultSaberPrefab { get; private set; }
    public GameObject? VanillaSaberPrefab { get; private set; }

    private readonly ModSettings _settings = settings;
    private readonly SaberAssetDefinition.Factory _defFactory = defFactory;
    private readonly SaberCatalog _catalog = catalog;
    private readonly PinTracker _pins = pins;
    private readonly IModLogger _log = log.ForSource(nameof(DefaultSaberProvider));

    public void Initialize()
    {
        try
        {
            var prefabs = LoadContent<GameObject>(SaberModelAddress);
            var sourcePrefab = prefabs?.FirstOrDefault();
            if (sourcePrefab == null)
            {
                _log.Warn("Failed to load vanilla saber model from Addressables.");
                return;
            }

            VanillaSaberPrefab = sourcePrefab;
            DefaultSaberPrefab = BuildStructuredPrefab(sourcePrefab);
            BuildEntry(DefaultSaberPrefab);

            _catalog.RegisterDefaultSaberEntry(Entry!);

            if (_settings.ShowDefaultSaber)
            _catalog.ShowDefaultSaberPreview(Preview!);
        }
        catch (Exception ex)
        {
            _log.Error($"Initialization failed: {ex}");
        }
    }

    private static GameObject BuildStructuredPrefab(GameObject sourcePrefab)
    {
        var root = new GameObject("SS_DefaultSaberRoot");
        root.SetActive(false);
        Object.DontDestroyOnLoad(root);

        var leftChild = Object.Instantiate(sourcePrefab, root.transform);
        leftChild.name = "LeftSaber";
        leftChild.SetActive(false);
        NormalizeChild(leftChild);

        var rightChild = Object.Instantiate(sourcePrefab, root.transform);
        rightChild.name = "RightSaber";
        rightChild.SetActive(false);
        NormalizeChild(rightChild);

        return root;
    }

    private static void NormalizeChild(GameObject child)
    {
        foreach (var rend in child.GetComponentsInChildren<Renderer>(true))
        {
            if (rend == null) continue;
            var mats = rend.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null)
                {
                    mats[i] = Object.Instantiate(mats[i]);
                    changed = true;
                }
            }
            if (changed) rend.sharedMaterials = mats;
        }

        Material? trailMaterial = null;
        var vanillaTrail = child.GetComponentInChildren<global::SaberTrail>(true);
        if (vanillaTrail != null)
        {
            var trailRendererPrefab = vanillaTrail._trailRendererPrefab;
            if (trailRendererPrefab != null)
            {
                var meshRenderer = trailRendererPrefab.GetComponent<MeshRenderer>();
                if (meshRenderer != null)
                trailMaterial = meshRenderer.sharedMaterial;
            }
        }

        const float bladeBottomZ = 0f;
        const float bladeTopZ = 1f;

        var marker = child.AddComponent<SaberTrailMarker>();
        marker.Length = DefaultTrailLength;

        var tipObj = new GameObject("SS_TrailTip");
        tipObj.transform.SetParent(child.transform, false);
        tipObj.transform.localPosition = new Vector3(0, 0, bladeTopZ);

        var baseObj = new GameObject("SS_TrailBase");
        baseObj.transform.SetParent(child.transform, false);
        baseObj.transform.localPosition = new Vector3(0, 0, bladeBottomZ);

        marker.PointEnd = tipObj.transform;
        marker.PointStart = baseObj.transform;

        if (trailMaterial != null)
        {
            trailMaterial = Object.Instantiate(trailMaterial);
            trailMaterial.color = Color.white;
        }
        marker.TrailMaterial = trailMaterial!;

        var descriptor = child.AddComponent<SaberDescriptor>();
        descriptor.SaberName = "Default Saber";
        descriptor.AuthorName = "Beat Games";

        foreach (var glow in child.GetComponentsInChildren<SetSaberGlowColor>(true))
        glow.enabled = false;
        foreach (var fakeGlow in child.GetComponentsInChildren<SetSaberFakeGlowColor>(true))
        fakeGlow.enabled = false;

        if (child.GetComponent<SaberModelController>() is { } mc) Object.DestroyImmediate(mc);
        if (vanillaTrail != null) Object.DestroyImmediate(vanillaTrail);
    }

    private void BuildEntry(GameObject root)
    {
        var leftChild = root.transform.Find("LeftSaber").gameObject;
        var rightChild = root.transform.Find("RightSaber").gameObject;

        var leftBundle = new LoadedBundle(DefaultSaberPath, leftChild, null, AssetOrigin.Generated) { ParsedBounds = (0f, 1f) };
        var leftDef = _defFactory.Create(leftBundle);
        leftDef.SaberDescriptor = leftChild.GetComponent<SaberDescriptor>();
        leftDef.AssignedHand = SaberHand.Left;
        leftDef.ForceColorable = true;

        var rightBundle = new LoadedBundle(DefaultSaberPath, rightChild, null, AssetOrigin.Generated) { ParsedBounds = (0f, 1f) };
        var rightDef = _defFactory.Create(rightBundle);
        rightDef.SaberDescriptor = rightChild.GetComponent<SaberDescriptor>();
        rightDef.AssignedHand = SaberHand.Right;
        rightDef.ForceColorable = true;

        Entry = SaberAssetEntry.Create(leftDef, rightDef, root);
        leftDef.OwnerEntry = Entry;
        rightDef.OwnerEntry = Entry;

        Entry.IsSPICompatible = true;
        Entry.SetPinned(_pins.Contains(DefaultSaberPath));

        Preview = new(DefaultSaberPath, Entry);
    }

    public void Register()
    {
        if (Entry is not null && Preview is not null)
        _catalog.ShowDefaultSaberPreview(Preview);
    }

    public void Unregister()
    {
        _catalog.HideDefaultSaberPreview();
    }

    public void Dispose()
    {
        if (DefaultSaberPrefab != null)
        {
            Object.Destroy(DefaultSaberPrefab);
            DefaultSaberPrefab = null;
        }
    }
}