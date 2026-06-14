// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.AssetPipeline;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Persistence;
using System;
using UnityEngine;

namespace SaberSense.Catalog.Model;

public class PieceDefinition : IDisposable
{
    public readonly LoadedBundle Asset;

    public readonly SaberSense.Behaviors.ComponentModifierRegistry ComponentModifiers;

    public SaberAssetEntry? OwnerEntry { get; set; }

    public GameObject Prefab => Asset.Prefab;

    public AuxObjectManager? AuxObjects { get; internal set; }

    public SaberHand AssignedHand { get; internal set; }

    protected PieceDefinition(LoadedBundle asset, IModLogger log)
    {
        Asset = asset;
        ComponentModifiers = new SaberSense.Behaviors.ComponentModifierRegistry(asset.Prefab, log);
    }

    public virtual void Dispose() { }

    public virtual SaberDisplayInfo GetDisplayInfo() => default;

    public virtual void CloneStateFrom(PieceDefinition source)
    {
        if (ComponentModifiers is not null && source.ComponentModifiers is not null)
        ComponentModifiers.SyncFrom(source.ComponentModifiers);
    }

    public virtual JToken WriteTo(Serializer serializer)
    {
        var json = new JObject { { "Path", Asset.RelativePath } };
        if (!string.IsNullOrEmpty(Asset.ContentHash))
        json.Add("ContentHash", Asset.ContentHash);

        return json;
    }
}