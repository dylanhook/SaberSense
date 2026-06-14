// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.AssetPipeline;
using SaberSense.AssetPipeline.Formats.Saber;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using SaberSense.Rendering;
using System.Linq;
using UnityEngine;
using Zenject;

namespace SaberSense.Catalog.Model;
public class SaberAssetDefinition : PieceDefinition
{
    public TrailSettings? TrailSettings
    {
        get
        {
            if (_trailDef is null)
            {
                var trailSettings = ExtractTrail(false);
                if (trailSettings is null)
                {
                    _hasTrail = false;
                    return null;
                }

                _trailDef = trailSettings;
            }

            return _trailDef;
        }
    }

    public bool HasTrail
    {
        get => _hasTrail ??= ValidateTrail();
    }

    internal SaberDescriptor? SaberDescriptor { get; set; }

    public bool ForceColorable { get; internal set; }

    private bool _trailReparented;
    private bool? _hasTrail;
    private TrailSettings? _trailDef;

    internal void ResetTrailCache() { _trailDef = null; _hasTrail = null; }

    internal SaberAssetDefinition(LoadedBundle storeAsset, IModLogger log) : base(storeAsset, log)
    {
    }

    internal void ComputeSaberExtent(TrailSettings target)
    {
        var bounds = SaberBoundsCalculator.ComputeZBounds(Asset?.ParsedBounds, Prefab);
        if (bounds is not var (minZ, maxZ)) return;

        target.SaberExtent = Mathf.Max(0.01f, maxZ - minZ);

        float trailEndZ = target.TrailEndZ > 0f ? target.TrailEndZ : maxZ;
        target.MaxTrailWidth = Mathf.Max(0.01f, trailEndZ - minZ);

        bool fromParser = Asset?.ParsedBounds.HasValue ?? false;
        ModLogger.ForSource("SaberExtent").Debug($"extent={target.SaberExtent:F3} maxTrailWidth={target.MaxTrailWidth:F3} (trailEndZ={trailEndZ:F3}, minZ={minZ:F3}, maxZ={maxZ:F3}, parsed={fromParser}) for '{Asset?.BaseName}'");
    }

    public string Name => Asset?.BaseName ?? "Custom Saber";
    public string Author => SaberDescriptor?.AuthorName ?? "Unknown";

    public override SaberDisplayInfo GetDisplayInfo() =>
    new(SaberDescriptor?.SaberName ?? "Custom Saber", SaberDescriptor?.AuthorName ?? "Unknown", SaberDescriptor?.CoverImage, false);

    public TrailSettings? ExtractTrail(bool addTrailOrigin)
    {
        var trail = SaberComponentDiscovery.GetTrails(Prefab).FirstOrDefault();

        if (trail?.TrailMaterial == null)
        return null;

        var nativeWrap = trail.TrailMaterial.TryGetMainTexture(out var tex)
        ? tex.wrapMode
        : default;

        ReparentTrails();

        var settings = new TrailSettings(
        MaterialHandle.Borrow(trail.TrailMaterial),
        trail.Length,
        trail.GetWidth(),
        Vector3.zero,
        0, nativeWrap,
        addTrailOrigin ? Asset.RelativePath : "");

        var bounds = SaberBoundsCalculator.ComputeZBounds(Asset?.ParsedBounds, Prefab);
        if (bounds is not var (minZ, maxZ)) return settings;

        settings.SaberExtent = Mathf.Max(0.01f, maxZ - minZ);

        float trailEndZ = trail.PointEnd != null ? trail.PointEnd.localPosition.z : maxZ;
        settings.TrailEndZ = trailEndZ;
        settings.MaxTrailWidth = Mathf.Max(0.01f, trailEndZ - minZ);

        bool fromParser = Asset?.ParsedBounds.HasValue ?? false;
        ModLogger.ForSource("ExtractTrail").Debug($"extent={settings.SaberExtent:F3} maxTrailWidth={settings.MaxTrailWidth:F3} (trailEndZ={trailEndZ:F3}, minZ={minZ:F3}, parsed={fromParser}) for '{Asset?.BaseName}'");

        return settings;
    }

    private bool ValidateTrail() =>
    Prefab && Prefab.GetComponentInChildren<SaberTrailMarker>(true) is { TrailMaterial: not null };

    public void ReparentTrails()
    {
        if (_trailReparented) return;
        _trailReparented = true;

        if (Prefab.GetComponent<SaberTrailMarker>() is not { } trail) return;

        var root = Prefab.transform;
        trail.PointStart!.SetParent(root, true);
        trail.PointEnd!.SetParent(root, true);
    }

    internal sealed class Factory : PlaceholderFactory<LoadedBundle, SaberAssetDefinition>
    { }
}