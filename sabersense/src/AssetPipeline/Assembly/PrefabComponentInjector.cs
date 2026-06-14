// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.AssetPipeline.Formats.Saber;
using SaberSense.Gameplay;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.AssetPipeline.Assembly;

internal static class PrefabComponentInjector
{
    public static SaberDescriptor? InjectDescriptor(GameObject root, SaberMetadata metadata)
    => DescriptorInjector.InjectDescriptor(root, metadata);

    public static void InjectTrails(
    GameObject root,
    IReadOnlyList<TrailData> trails,
    SaberParseResult parseResult,
    AssetBundle? bundle = null,
    IReadOnlyList<Material>? bundleOwnedMaterials = null)
    => TrailInjector.InjectTrails(root, trails, parseResult, bundle, bundleOwnedMaterials);

    public static void InjectModifiers(
    GameObject root,
    IReadOnlyList<ModifierPayload> payloads,
    SaberParseResult parseResult)
    => ModifierInjector.InjectModifiers(root, payloads, parseResult);

    public static void InjectSpringBones(GameObject root, SaberParseResult parseResult)
    => SpringBoneInjector.InjectSpringBones(root, parseResult);

    public static void MirrorAnimations(GameObject root)
    => AnimationInjector.MirrorAnimations(root);

    public static SaberEventDispatcher? InjectEvents(
    GameObject root, SaberParseResult parseResult, GameObject? eventTargetContainer = null)
    => EventInjector.InjectEvents(root, parseResult, eventTargetContainer);
}