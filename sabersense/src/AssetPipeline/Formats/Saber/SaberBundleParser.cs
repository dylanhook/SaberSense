// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.BundleFormat;
using SaberSense.Core.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.AssetPipeline.Formats.Saber;

public sealed partial class SaberBundleParser
{
    private readonly IModLogger _log;

    public SaberBundleParser(IModLogger log)
    {
        _log = log.ForSource(nameof(SaberBundleParser));
    }

    public (SaberMetadata Metadata, CoverImageData? CoverImage)? ParsePreviewOnly(string filePath)
    {
        try
        {
            static bool IsAssetsFile(string name) =>
            !name.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) &&
            !name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase);

            var bundleContent = BundleReader.ExtractBundleContent(filePath, IsAssetsFile);
            if (bundleContent.Count is 0) return null;

            byte[]? assetsData = null;
            foreach (var pair in bundleContent)
            {
                assetsData = pair.Value;
                break;
            }
            if (assetsData is null) return null;

            var assetsReader = new AssetsFileReader();
            assetsReader.Load(assetsData);

            var scriptMap = new Dictionary<long, string>();
            foreach (var info in assetsReader.Objects)
            {
                var type = assetsReader.GetType(info);
                if (type is null) continue;
                if (type.TypeId is 115)
                ReadNamedObject(assetsReader, info, scriptMap, "m_ClassName");
            }

            SaberMetadata? metadata = null;
            long coverSpritePathId = 0;

            foreach (var info in assetsReader.Objects)
            {
                var type = assetsReader.GetType(info);
                if (type is null) continue;

                int typeId = type.TypeId;
                if (typeId is not 114 and >= 0) continue;

                var obj = assetsReader.ReadObject(info);
                if (obj is null) continue;

                var scriptRef = obj.GetChild("m_Script");
                if (scriptRef is null) continue;

                var scriptPathId = scriptRef.GetLong("m_PathID");
                if (!scriptMap.TryGetValue(scriptPathId, out var className)) continue;

                if (className is "SaberDescriptor")
                {
                    var (desc, coverPId) = ReadSaberDescriptor(obj, new ParseDiagnostics(_log.ForSource("Parse")));
                    metadata = desc;
                    coverSpritePathId = coverPId;
                    break;
                }
            }

            CoverImageData? coverData = null;
            if (coverSpritePathId is not 0)
            {
                coverData = TryExtractCoverImage(assetsReader, coverSpritePathId, bundleContent, silent: true);

                if (coverData is null)
                {
                    var resSContent = BundleReader.ExtractBundleContent(filePath,
                    name => name.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase));

                    if (resSContent.Count > 0)
                    {
                        foreach (var kv in resSContent)
                        bundleContent[kv.Key] = kv.Value;

                        coverData = TryExtractCoverImage(assetsReader, coverSpritePathId, bundleContent);
                    }
                }
            }

            return (metadata ?? SaberMetadata.Unknown, coverData);
        }
        catch (Exception ex)
        {
            _log?.Warn($"Failed to parse preview for '{filePath}': {ex.Message}");
            return null;
        }
    }

    public SaberParseResult? Parse(string filePath)
    {
        try
        {
            var bundleContent = BundleReader.ExtractBundleContent(filePath);
            if (bundleContent.Count is 0) return null;

            byte[]? assetsData = null;
            foreach (var pair in bundleContent)
            {
                if (!pair.Key.EndsWith(".resS", StringComparison.OrdinalIgnoreCase) &&
                !pair.Key.EndsWith(".resource", StringComparison.OrdinalIgnoreCase))
                {
                    assetsData = pair.Value;
                    break;
                }
            }
            if (assetsData is null) return null;

            var assetsReader = new AssetsFileReader();
            assetsReader.Load(assetsData);

            var tables = BuildLookupTables(assetsReader);

            var diagnostics = new ParseDiagnostics(_log.ForSource("Parse"));
            SaberMetadata? metadata = null;
            var trails = new List<TrailData>();
            long coverSpritePathId = 0;
            var modifierPayloads = new List<ModifierPayload>();
            var springBones = new List<SpringBoneEntry>();
            var springColliders = new List<SpringColliderEntry>();
            var eventManagers = new List<EventManagerEntry>();
            var comboFilters = new List<ComboFilterEntry>();
            var nthComboFilters = new List<EveryNthComboEntry>();
            var accuracyFilters = new List<AccuracyFilterEntry>();

            foreach (var info in assetsReader.Objects)
            {
                var type = assetsReader.GetType(info);
                if (type is null) continue;

                int typeId = type.TypeId;
                if (typeId is not 114 and >= 0) continue;

                var obj = assetsReader.ReadObject(info);
                if (obj is null) continue;

                var scriptRef = obj.GetChild("m_Script");
                if (scriptRef is null) continue;

                var scriptPathId = scriptRef.GetLong("m_PathID");
                if (!tables.ScriptMap.TryGetValue(scriptPathId, out var className)) continue;

                switch (className)
                {
                    case "SaberDescriptor":
                    var (desc, coverPId) = ReadSaberDescriptor(obj, diagnostics);
                    metadata = desc;
                    coverSpritePathId = coverPId;
                    break;

                    case "CustomTrail":
                    var trailData = ReadCustomTrail(obj, diagnostics);
                    trails.Add(trailData);
                    break;

                    case "SaberModifierCollection":
                    var modPayload = ReadModifierCollection(obj);
                    if (modPayload is not null)
                    modifierPayloads.Add(modPayload);
                    break;

                    case "DynamicBone":
                    springBones.Add(ReadDynamicBone(obj, diagnostics));
                    break;

                    case "DynamicBoneCollider":
                    springColliders.Add(ReadDynamicBoneCollider(obj, info.PathId));
                    break;

                    case "EventManager":
                    var evtMgr = ReadEventManager(obj);
                    if (evtMgr.HasAnyCalls)
                    eventManagers.Add(evtMgr);
                    break;

                    case "ComboReachedEvent":
                    comboFilters.Add(ReadComboFilter(obj));
                    break;

                    case "EveryNthComboFilter":
                    nthComboFilters.Add(ReadEveryNthComboFilter(obj));
                    break;

                    case "AccuracyReachedEvent":
                    accuracyFilters.Add(ReadAccuracyFilter(obj));
                    break;
                }
            }

            foreach (var trail in trails)
            {
                if (trail.PointEndPathId is not 0 && tables.Transforms.TryGetValue(trail.PointEndPathId, out var endXf))
                trail.ParsedPointEndZ = endXf.Position.z;
                if (trail.PointStartPathId is not 0 && tables.Transforms.TryGetValue(trail.PointStartPathId, out var startXf))
                trail.ParsedPointStartZ = startXf.Position.z;
            }

            var parsedBounds = ComputeParsedBounds(
            tables.Transforms, tables.MeshAABBs, tables.GoToMesh, tables.GoToTransform);
            if (parsedBounds.HasValue)
            _log?.Debug($"Parsed bounds: minZ={parsedBounds.Value.minZ:F3} maxZ={parsedBounds.Value.maxZ:F3}");

            CoverImageData? coverData = null;
            if (coverSpritePathId is not 0)
            {
                coverData = TryExtractCoverImage(assetsReader, coverSpritePathId, bundleContent);
            }

            if (diagnostics.MissedFieldCount is > 0)
            _log?.Debug($"Parse completed for '{filePath}' with {diagnostics.MissedFieldCount} required field(s) missed (read as defaults)");

            return new(
            metadata ?? SaberMetadata.Unknown,
            trails,
            tables.GameObjectNames,
            tables.MaterialNames,
            coverData,
            modifierPayloads,
            parsedBounds,
            springBones,
            springColliders,
            tables.TransformToGameObject,
            (eventManagers.Count is > 0 || comboFilters.Count is > 0 || nthComboFilters.Count is > 0 || accuracyFilters.Count is > 0)
            ? new()
            {
                EventManagers = eventManagers,
                ComboFilters = comboFilters,
                NthComboFilters = nthComboFilters,
                AccuracyFilters = accuracyFilters,
                PathIdToTypeId = tables.ObjectTypeIds
            }
            : null);
        }
        catch (Exception ex)
        {
            _log?.Warn($"Failed to parse '{filePath}': {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    private sealed class ParseDiagnostics(IModLogger log)
    {
        private readonly IModLogger _log = log;

        public int MissedFieldCount { get; private set; }

        public string RequireString(SerializedObject obj, string ownerType, string fieldName, string fallback)
        {
            if (obj.TryGetString(fieldName, out var value))
            return value;
            ReportMiss(ownerType, fieldName);
            return fallback;
        }

        public int RequireInt(SerializedObject obj, string ownerType, string fieldName, int fallback)
        {
            if (obj.TryGetInt(fieldName, out var value))
            return value;
            ReportMiss(ownerType, fieldName);
            return fallback;
        }

        public float RequireFloat(SerializedObject obj, string ownerType, string fieldName, float fallback)
        {
            if (obj.TryGetFloat(fieldName, out var value))
            return value;
            ReportMiss(ownerType, fieldName);
            return fallback;
        }

        private void ReportMiss(string ownerType, string fieldName)
        {
            MissedFieldCount++;
            _log.Debug($"Required field '{ownerType}.{fieldName}' absent or mistyped; using default value");
        }
    }

    private struct ParsedTransform
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public long ParentPathId;
        public long GameObjectPathId;
    }

    private struct ParsedAABB
    {
        public Vector3 Center;
        public Vector3 Extent;
    }

    private struct BundleLookupTables
    {
        public Dictionary<long, string> ScriptMap;

        public Dictionary<long, string> GameObjectNames;

        public Dictionary<long, string> MaterialNames;

        public Dictionary<long, ParsedTransform> Transforms;

        public Dictionary<long, ParsedAABB> MeshAABBs;

        public Dictionary<long, long> GoToMesh;

        public Dictionary<long, long> GoToTransform;

        public Dictionary<long, long> TransformToGameObject;

        public Dictionary<long, int> ObjectTypeIds;
    }
}