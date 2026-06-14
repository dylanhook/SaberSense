// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core;
using SaberSense.Core.Utilities;
using SaberSense.Rendering;
using SaberSense.Rendering.TrailGeometry;
using System;

namespace SaberSense.Customization;

internal sealed class EditScope
{
    private readonly PreviewSession _session;

    public bool Linked { get; set; } = true;

    public LiveSaber? PreviewMirror { get; set; }

    public SaberHand FocusedHand => _session.FocusedHand;

    public EditScope(PreviewSession session)
    {
        _session = session;
    }

    public void Apply(Action<LiveSaber> action)
    {
        var focused = _session.FocusedSaber;
        if (focused is not null) action(focused);

        if (Linked)
        {
            var other = OtherSaber();
            if (other is not null) action(other);
        }

        if (PreviewMirror is not null) action(PreviewMirror);
    }

    public void ApplyTrail(Action<LiveTrail> action)
    {
        LiveSaber.WithLiveTrail(_session.FocusedSaber, action);
        if (Linked)
        LiveSaber.WithLiveTrail(OtherSaber(), action);
    }

    public void ApplyTransform(Action<TransformApplier> action)
    {
        LiveSaber.WithTransformApplier(_session.FocusedSaber, action);
        if (Linked)
        LiveSaber.WithTransformApplier(OtherSaber(), action);
        LiveSaber.WithTransformApplier(PreviewMirror, action);
    }

    private LiveSaber? OtherSaber()
    {
        var otherHand = _session.FocusedHand.Other();
        return _session.Sabers[otherHand];
    }
}