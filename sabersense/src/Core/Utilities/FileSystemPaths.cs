// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using IPA.Utilities;
using System.IO;

namespace SaberSense.Core.Utilities;

public static class AssetPaths
{
    public static string? PrefixToStrip { get; internal set; }

    public static string ResolveFull(string relative)
    {
        var full = Path.GetFullPath(Path.Combine(UnityGame.InstallPath, relative));
        var root = UnityGame.InstallPath;
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, System.StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(full, root, System.StringComparison.OrdinalIgnoreCase))
        throw new System.InvalidOperationException($"Path traversal blocked: {relative}");
        return full;
    }

    public static string MakeRelative(string absolute)
    {
        var root = UnityGame.InstallPath;
        if (absolute.StartsWith(root, System.StringComparison.OrdinalIgnoreCase))
        return absolute[(root.Length + 1)..];
        return absolute;
    }

    public static string GetSubfolderPath(string relativePath)
    {
        var normalized = RemoveRootPrefix(relativePath);
        var segments = normalized.Split(Path.DirectorySeparatorChar);

        if (segments.Length is < 3)
        return string.Empty;

        return string.Join(
        Path.DirectorySeparatorChar.ToString(),
        segments, 1, segments.Length - 2);
    }

    public static string RemoveRootPrefix(string path)
    {
        if (string.IsNullOrEmpty(PrefixToStrip))
        return path;

        return path.StartsWith(PrefixToStrip!, System.StringComparison.OrdinalIgnoreCase)
        ? path[PrefixToStrip!.Length..]
        : path;
    }
}