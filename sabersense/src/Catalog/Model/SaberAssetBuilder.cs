// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.AssetPipeline;
using SaberSense.AssetPipeline.Formats.Saber;
using SaberSense.Core;
using SaberSense.Core.Logging;
using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SaberSense.Catalog.Model;

internal sealed class SaberAssetBuilder : IAssetParser
{
    private readonly SaberAssetDefinition.Factory _factory;
    private readonly PinTracker _pins;
    private readonly IModLogger _log;

    public SaberAssetBuilder(SaberAssetDefinition.Factory factory, PinTracker pins, IModLogger log)
    {
        _factory = factory;
        _pins = pins;
        _log = log.ForSource(nameof(SaberAssetBuilder));
    }

    public SaberAssetEntry? ParseAsset(LoadedBundle storeAsset)
    {
        var prefabs = LocateHandPrefabs(storeAsset.Prefab.transform);
        if (prefabs.Left == null)
        {
            _log.Warn($"Saber asset has no LeftSaber child: {storeAsset.RelativePath}");
            return null;
        }

        GameObject rootInstance = storeAsset.Prefab;
        var leftSaber = prefabs.Left;
        var rightSaber = prefabs.Right ?? CreateMirroredSaber(leftSaber, rootInstance.transform);

        var storeAssetLeft = new LoadedBundle(storeAsset.RelativePath, leftSaber, storeAsset.Bundle);
        var storeAssetRight = new LoadedBundle(storeAsset.RelativePath, rightSaber, storeAsset.Bundle);
        storeAssetLeft.ParsedBounds = storeAsset.ParsedBounds;
        storeAssetRight.ParsedBounds = storeAsset.ParsedBounds;
        storeAssetLeft.ParseResult = storeAsset.ParseResult;
        storeAssetRight.ParseResult = storeAsset.ParseResult;
        var leftDefinition = _factory.Create(storeAssetLeft);
        var rightDefinition = _factory.Create(storeAssetRight);

        var descriptor = rootInstance.GetComponent<SaberDescriptor>();
        if (descriptor == null)
        {
            descriptor = rootInstance.AddComponent<SaberDescriptor>();
            descriptor.SaberName = storeAsset.BaseName;
            descriptor.AuthorName = "Unknown";
        }
        leftDefinition.SaberDescriptor = rightDefinition.SaberDescriptor = descriptor;

        leftDefinition.AssignedHand = SaberHand.Left;
        rightDefinition.AssignedHand = SaberHand.Right;
        var entry = SaberAssetEntry.Create(leftDefinition, rightDefinition, rootInstance);

        leftDefinition.OwnerEntry = entry;
        rightDefinition.OwnerEntry = entry;

        entry.IsSPICompatible = storeAsset.IsSPICompatible;
        entry.SetPinned(_pins.Contains(storeAsset.RelativePath));
        return entry;
    }

    private readonly record struct HandPrefabs(GameObject? Left, GameObject? Right);

    private HandPrefabs LocateHandPrefabs(Transform root)
    {
        GameObject? left = null, right = null;
        var queue = new System.Collections.Generic.Queue<Transform>();
        queue.Enqueue(root);
        while (queue.Count is > 0 && (left is null || right is null))
        {
            var current = queue.Dequeue();
            if (current.name.Equals("LeftSaber", StringComparison.OrdinalIgnoreCase))
            left = current.gameObject;
            else if (current.name.Equals("RightSaber", StringComparison.OrdinalIgnoreCase))
            right = current.gameObject;
            for (int i = 0; i < current.childCount; i++)
            queue.Enqueue(current.GetChild(i));
        }
        return new(left, right);
    }

    private static GameObject CreateMirroredSaber(GameObject source, Transform parent)
    {
        var container = new GameObject("RightSaber").transform;
        container.parent = parent;

        var clone = Object.Instantiate(source, container, false);
        clone.transform.SetLocalPositionAndRotation(Vector3.zero, clone.transform.localRotation);
        clone.transform.localScale = new Vector3(-1, 1, 1);
        clone.name = "SaberSense_MirroredRight";

        container.gameObject.SetActive(false);
        return container.gameObject;
    }
}