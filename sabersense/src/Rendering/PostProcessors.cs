// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using CameraUtils.Core;
using SaberSense.Configuration;
using SaberSense.Core.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Rendering;

public interface IPartFinalizer
{
    void ProcessPart(GameObject partObject);
}

public interface ISaberEffect
{
    void ProcessSaber(LiveSaber saberObject);

    void OnColorChanged(Color color);

    void OnScaleChanged();

    void OnVisibilityLayerChanged(VisibilityLayer layer);

    void OnTeardown();
}

internal abstract class SaberEffectBase : ISaberEffect
{
    public abstract void ProcessSaber(LiveSaber saber);

    public virtual void OnColorChanged(Color color) { }

    public virtual void OnScaleChanged() { }

    public virtual void OnVisibilityLayerChanged(VisibilityLayer layer) { }

    public virtual void OnTeardown() { }
}

internal sealed class DefaultSaberFinalizer : SaberEffectBase
{
    private const int SaberLayer = 12;

    private readonly ModSettings _settings;

    internal DefaultSaberFinalizer(ModSettings settings) => _settings = settings;

    public override void ProcessSaber(LiveSaber saber)
    {
        var root = saber.GameObject;

        var renderers = new List<Renderer>();
        var colliders = new List<Collider>();
        var audioSources = new List<AudioSource>();

        root.SetLayer<Renderer>(SaberLayer);

        root.GetComponentsInChildren(true, colliders);
        foreach (var col in colliders) col.enabled = false;

        root.GetComponentsInChildren(true, audioSources);
        foreach (var src in audioSources) src.volume *= _settings.AudioGain;

        root.GetComponentsInChildren(true, renderers);
        foreach (var rend in renderers) rend.sortingOrder = 3;
    }
}