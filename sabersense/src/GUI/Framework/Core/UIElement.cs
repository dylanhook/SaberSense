// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using UnityEngine;

namespace SaberSense.GUI.Framework.Core;

public abstract class UIElement : IDisposable
{
    private bool _disposed;

    public GameObject GameObject { get; private set; }

    public RectTransform RectTransform { get; private set; }

    public bool IsDisposed => _disposed;

    protected UIElement(string name)
    {
        GameObject = new GameObject(name);
        RectTransform = GameObject.AddComponent<RectTransform>();

        RectTransform.anchorMin = Vector2.zero;
        RectTransform.anchorMax = Vector2.one;
        RectTransform.sizeDelta = Vector2.zero;
        RectTransform.anchoredPosition = Vector2.zero;
    }

    public virtual void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (GameObject != null)
        UnityEngine.Object.Destroy(GameObject);
    }
}