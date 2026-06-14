// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core;
using SaberSense.Rendering;
using SaberSense.Rendering.TrailGeometry;
using System;

namespace SaberSense.Customization;

internal sealed class SaberPair
{
    public LiveSaber? Left { get; set; }

    public LiveSaber? Right { get; set; }

    public LiveSaber? this[SaberHand hand]
    => hand == SaberHand.Left ? Left : Right;

    public void ForEach(Action<LiveSaber> action)
    {
        if (Left is not null) action(Left);
        if (Right is not null) action(Right);
    }

    public void ForEachTransform(Action<TransformApplier> action)
    {
        LiveSaber.WithTransformApplier(Left, action);
        LiveSaber.WithTransformApplier(Right, action);
    }

    public void ForEachLiveTrail(Action<LiveTrail> action)
    {
        LiveSaber.WithLiveTrail(Left, action);
        LiveSaber.WithLiveTrail(Right, action);
    }

    public void Clear()
    {
        Left?.Destroy();
        Right?.Destroy();
        Left = null;
        Right = null;
    }
}