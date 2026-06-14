// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SaberSense.Catalog;

public sealed class FolderNavigator
{
    private static readonly char Sep = Path.DirectorySeparatorChar;
    private const string ParentMarker = "<";

    public string BreadcrumbPath => _currentNode.FullPath;

    public bool AtTopLevel => _currentNode.Parent is null;

    private readonly FolderNode _root;
    private FolderNode _currentNode;

    public FolderNavigator(IReadOnlyList<string> folderPaths)
    {
        _root = BuildTree(folderPaths);
        _currentNode = _root;
    }

    public void GoBack()
    {
        if (_currentNode.Parent is not null)
        _currentNode = _currentNode.Parent;
    }

    public void Navigate(string folderName)
    {
        if (folderName == ParentMarker)
        {
            GoBack();
            return;
        }

        if (string.IsNullOrWhiteSpace(folderName))
        throw new ArgumentException("Folder name cannot be empty.", nameof(folderName));

        if (!_currentNode.Children.TryGetValue(folderName, out var child))
        throw new InvalidOperationException(
        $"Folder '{folderName}' does not exist under '{_currentNode.Name}'.");

        _currentNode = child;
    }

    public List<ISaberListItem> Process(IEnumerable<ISaberListItem> items)
    {
        var currentPath = _currentNode.FullPath;
        var result = FilterItemsForPath(items, currentPath).ToList();

        var childNames = _currentNode.Children.Keys
        .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
        .ToList();

        for (var i = childNames.Count - 1; i >= 0; i--)
        result.Insert(0, new FolderItem(childNames[i]));

        if (!AtTopLevel)
        result.Insert(0, new FolderItem(ParentMarker));

        return result;
    }

    private static IEnumerable<ISaberListItem> FilterItemsForPath(IEnumerable<ISaberListItem> items, string dir)
    {
        foreach (var item in items)
        {
            switch (item)
            {
                case AssetPreview preview when preview.SubFolder == dir:
                yield return item;
                break;
                case SaberAssetEntry entry when entry.LeftPiece?.Asset.DirectoryName == dir:
                yield return item;
                break;
                case AssetPreview:
                case SaberAssetEntry:
                break;
                default:
                yield return item;
                break;
            }
        }
    }

    private static FolderNode BuildTree(IReadOnlyList<string> paths)
    {
        var root = new FolderNode("", null);

        foreach (var path in paths)
        {
            var segments = path.Split(Sep, StringSplitOptions.RemoveEmptyEntries);
            var current = root;

            foreach (var segment in segments)
            {
                if (!current.Children.TryGetValue(segment, out var child))
                {
                    child = new(segment, current);
                    current.Children[segment] = child;
                }
                current = child;
            }
        }

        return root;
    }

    private sealed class FolderNode
    {
        public string Name { get; }
        public FolderNode? Parent { get; }
        public SortedDictionary<string, FolderNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string FullPath { get; }

        public FolderNode(string name, FolderNode? parent)
        {
            Name = name;
            Parent = parent;
            FullPath = parent is null || string.IsNullOrEmpty(parent.FullPath)
            ? name
            : parent.FullPath + Sep + name;
        }
    }
}