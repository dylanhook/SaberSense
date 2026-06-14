// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.BundleFormat;
using System.Collections.Generic;

namespace SaberSense.AssetPipeline.Formats.Saber;

public sealed partial class SaberBundleParser
{
    private BundleLookupTables BuildLookupTables(AssetsFileReader assetsReader)
    {
        var scriptMap = new Dictionary<long, string>();
        var gameObjectNames = new Dictionary<long, string>();
        var transformToGameObject = new Dictionary<long, long>();
        var materialNames = new Dictionary<long, string>();
        var transforms = new Dictionary<long, ParsedTransform>();
        var meshAABBs = new Dictionary<long, ParsedAABB>();
        var goToMesh = new Dictionary<long, long>();
        var goToTransform = new Dictionary<long, long>();
        var objectTypeIds = new Dictionary<long, int>();

        int gameObjectCount = 0, transformCount = 0;

        foreach (var info in assetsReader.Objects)
        {
            var type = assetsReader.GetType(info);
            if (type is null) continue;

            objectTypeIds[info.PathId] = type.TypeId;

            switch (type.TypeId)
            {
                case 115: ReadNamedObject(assetsReader, info, scriptMap, "m_ClassName"); break;
                case 1: gameObjectCount++; ReadNamedObject(assetsReader, info, gameObjectNames, "m_Name"); break;
                case 4: transformCount++; ReadTransform(assetsReader, info, transforms, transformToGameObject, goToTransform); break;
                case 43: ReadMesh(assetsReader, info, meshAABBs); break;
                case 33: ReadMeshComponent(assetsReader, info, goToMesh, preventOverwrite: false); break;
                case 137: ReadMeshComponent(assetsReader, info, goToMesh, preventOverwrite: true); break;
                case 21: ReadNamedObject(assetsReader, info, materialNames, "m_Name"); break;
            }
        }

        foreach (var (transformPathId, goPathId) in transformToGameObject)
        {
            if (gameObjectNames.TryGetValue(goPathId, out var name))
            gameObjectNames[transformPathId] = name;
        }

        _log?.Debug($"Phase 3: scripts={scriptMap.Count} gameObjects={gameObjectCount} transforms={transformCount}");

        return new()
        {
            ScriptMap = scriptMap,
            GameObjectNames = gameObjectNames,
            MaterialNames = materialNames,
            Transforms = transforms,
            MeshAABBs = meshAABBs,
            GoToMesh = goToMesh,
            GoToTransform = goToTransform,
            TransformToGameObject = transformToGameObject,
            ObjectTypeIds = objectTypeIds
        };
    }

    private static void ReadNamedObject(AssetsFileReader reader, ObjectInfo info, Dictionary<long, string> map, string fieldName)
    {
        var obj = reader.ReadObject(info);
        if (obj is null) return;
        var value = obj.GetString(fieldName);
        if (!string.IsNullOrEmpty(value))
        map[info.PathId] = value;
    }

    private static void ReadTransform(AssetsFileReader reader, ObjectInfo info,
    Dictionary<long, ParsedTransform> transforms,
    Dictionary<long, long> transformToGameObject,
    Dictionary<long, long> goToTransform)
    {
        var obj = reader.ReadObject(info);
        if (obj is null) return;

        var goRef = obj.GetChild("m_GameObject");
        long goPathId = 0;
        if (goRef is not null)
        {
            goPathId = goRef.GetLong("m_PathID");
            if (goPathId is not 0)
            {
                transformToGameObject[info.PathId] = goPathId;
                goToTransform[goPathId] = info.PathId;
            }
        }

        var localPos = obj.GetChild("m_LocalPosition");
        var localRot = obj.GetChild("m_LocalRotation");
        var localScl = obj.GetChild("m_LocalScale");
        var father = obj.GetChild("m_Father");

        transforms[info.PathId] = new()
        {
            Position = new UnityEngine.Vector3(
            localPos?.GetFloat("x") ?? 0f,
            localPos?.GetFloat("y") ?? 0f,
            localPos?.GetFloat("z") ?? 0f),
            Rotation = new UnityEngine.Quaternion(
            localRot?.GetFloat("x") ?? 0f,
            localRot?.GetFloat("y") ?? 0f,
            localRot?.GetFloat("z") ?? 0f,
            localRot?.GetFloat("w") ?? 1f),
            Scale = new UnityEngine.Vector3(
            localScl?.GetFloat("x") ?? 1f,
            localScl?.GetFloat("y") ?? 1f,
            localScl?.GetFloat("z") ?? 1f),
            ParentPathId = father?.GetLong("m_PathID") ?? 0,
            GameObjectPathId = goPathId
        };
    }

    private static void ReadMesh(AssetsFileReader reader, ObjectInfo info, Dictionary<long, ParsedAABB> meshAABBs)
    {
        var obj = reader.ReadObject(info);
        if (obj is null) return;
        var aabb = obj.GetChild("m_LocalAABB");
        if (aabb is null) return;
        var center = aabb.GetChild("m_Center");
        var extent = aabb.GetChild("m_Extent");
        if (center is null || extent is null) return;
        meshAABBs[info.PathId] = new()
        {
            Center = new UnityEngine.Vector3(center.GetFloat("x"), center.GetFloat("y"), center.GetFloat("z")),
            Extent = new UnityEngine.Vector3(extent.GetFloat("x"), extent.GetFloat("y"), extent.GetFloat("z"))
        };
    }

    private static void ReadMeshComponent(AssetsFileReader reader, ObjectInfo info, Dictionary<long, long> goToMesh, bool preventOverwrite)
    {
        var obj = reader.ReadObject(info);
        if (obj is null) return;
        var meshRef = obj.GetChild("m_Mesh");
        var goRef = obj.GetChild("m_GameObject");
        if (meshRef is null || goRef is null) return;
        var meshId = meshRef.GetLong("m_PathID");
        var goId = goRef.GetLong("m_PathID");
        if (meshId is not 0 && goId is not 0 && (!preventOverwrite || !goToMesh.ContainsKey(goId)))
        goToMesh[goId] = meshId;
    }
}