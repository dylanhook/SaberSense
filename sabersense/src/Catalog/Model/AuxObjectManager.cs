// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core;
using SaberSense.Rendering;
using UnityEngine;

namespace SaberSense.Catalog.Model;

public sealed class AuxObjectManager : System.IDisposable
{
    private GameObject _sourcePrefab;
    private GameObject? _fallbackRightPrefab;
    private GameObject? _spawnedRoot;
    private (GameObject? Left, GameObject? Right) _children;

    public bool IsSpawned => _spawnedRoot != null;

    public bool IsStale => !_sourcePrefab;

    public GameObject SourcePrefab => _sourcePrefab;

    public AuxObjectManager(GameObject sourcePrefab, GameObject? fallbackRightPrefab)
    {
        _sourcePrefab = sourcePrefab;
        _fallbackRightPrefab = fallbackRightPrefab;
    }

    public GameObject EnsureSpawned()
    {
        if (_spawnedRoot == null)
        _spawnedRoot = Instantiate();
        return _spawnedRoot;
    }

    public GameObject? GetHandObject(SaberHand hand)
    {
        EnsureSpawned();
        return hand == SaberHand.Left ? _children.Left : _children.Right;
    }

    public T GetRootComponent<T>() where T : Component => EnsureSpawned().GetComponent<T>();

    public Transform FindChild(string childName) => EnsureSpawned().transform.Find(childName);

    public void Destroy()
    {
        _spawnedRoot?.TryDestroyImmediate();
        _spawnedRoot = null;
        _children = default;
    }

    public void Dispose() => Destroy();

    private GameObject Instantiate()
    {
        var root = Object.Instantiate(_sourcePrefab);
        root.name = $"{_sourcePrefab.name}_Aux";
        root.SetActive(false);

        _children = DiscoverChildren(root);
        return root;
    }

    private (GameObject? Left, GameObject? Right) DiscoverChildren(GameObject root)
    {
        var left = root.transform.Find("LeftSaber")?.gameObject;
        var right = root.transform.Find("RightSaber")?.gameObject;

        if (right == null && _fallbackRightPrefab != null)
        right = Object.Instantiate(_fallbackRightPrefab, root.transform);

        left?.SetActive(false);
        right?.SetActive(false);

        return (left, right);
    }
}