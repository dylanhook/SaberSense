// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.Catalog;

public readonly struct SaberDisplayInfo
{
    public string? Name { get; }
    public string? Author { get; }
    public Sprite? Cover { get; }
    public bool IsPinned { get; }

    public SaberDisplayInfo(string name, string author, Sprite? cover, bool isPinned)
    {
        Name = name;
        Author = author;
        Cover = cover;
        IsPinned = isPinned;
    }
}

public sealed class FolderItem : ISaberListItem
{
    public string DisplayName { get; }
    public string CreatorName => string.Empty;
    public Sprite? CoverImage => null;
    public bool IsPinned => false;
    public bool IsSPICompatible => true;

    public FolderItem(string folderName) => DisplayName = folderName;
}