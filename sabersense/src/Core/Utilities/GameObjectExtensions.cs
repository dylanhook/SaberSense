// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Core.Utilities;

internal static class GameObjectExtensions
{
    public static SaberType ToSaberType(this SaberHand hand) =>
    hand == SaberHand.Left ? SaberType.SaberA : SaberType.SaberB;

    public static SaberHand Other(this SaberHand hand) =>
    hand == SaberHand.Left ? SaberHand.Right : SaberHand.Left;

    public static void SetLayer<T>(this GameObject go, int layer) where T : Component
    {
        if (go is null) return;
        foreach (var c in go.GetComponentsInChildren<T>())
        c.gameObject.layer = layer;
    }

    public static async Task WaitForFinishAsync(this IAsyncLoadable loadable)
    {
        if (loadable.CurrentTask is not null)
        await loadable.CurrentTask;
    }
}