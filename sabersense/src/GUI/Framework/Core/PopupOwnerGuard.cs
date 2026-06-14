// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.GUI.Framework.Core;

internal sealed class PopupOwnerGuard : MonoBehaviour
{
    private readonly List<GameObject> _registered = [];

    public void Register(GameObject go)
    {
        if (go != null && !_registered.Contains(go))
        _registered.Add(go);
    }

    public void Unregister(GameObject go) => _registered.Remove(go);

    private void OnDestroy()
    {
        foreach (var go in _registered)
        if (go != null) Destroy(go);
    }
}