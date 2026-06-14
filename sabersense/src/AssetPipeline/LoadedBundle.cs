// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Utilities;
using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SaberSense.AssetPipeline;

public enum AssetOrigin
{
    Disk,

    External,

    Generated
}

public sealed class LoadedBundle : IDisposable
{
    public readonly AssetBundle? Bundle;
    public readonly string FileExtension;
    public readonly string FileName;
    public readonly string BaseName;
    public readonly string RelativePath;
    public readonly string DirectoryName;
    public readonly AssetOrigin Origin;
    public bool ExistsOnDisk => Origin == AssetOrigin.Disk;
    public GameObject Prefab { get; internal set; }
    public bool IsSPICompatible { get; set; } = true;

    public string? ContentHash { get; set; }

    public (float minZ, float maxZ)? ParsedBounds { get; set; }

    public Formats.Saber.SaberParseResult? ParseResult { get; set; }

    public bool IsPrefabStale => Prefab == null;

    public LoadedBundle(string relativePath, GameObject prefab, AssetBundle? bundle, AssetOrigin origin = AssetOrigin.Disk)
    {
        RelativePath = relativePath;
        Origin = origin;
        FileName = Path.GetFileName(RelativePath);
        BaseName = Path.GetFileNameWithoutExtension(FileName);
        FileExtension = Path.GetExtension(FileName);
        DirectoryName = AssetPaths.GetSubfolderPath(relativePath);
        Prefab = prefab;
        Bundle = bundle;
    }

    public void Unload()
    {
        if (Bundle != null) Bundle.Unload(false);
        if (Prefab) Object.Destroy(Prefab);
    }

    public void Dispose() => Unload();
}