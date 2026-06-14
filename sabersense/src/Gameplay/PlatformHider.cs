// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;
using System.Collections;
using UnityEngine;
using Zenject;

namespace SaberSense.Gameplay;

internal sealed class PlatformHider : IInitializable
{
    [Inject] private readonly ModSettings _settings = null!;

    public void Initialize()
    {
        if (!_settings.HidePlatform) return;

        var go = new GameObject("SaberSense_PlatformHider");
        var runner = go.AddComponent<CoroutineRunner>();
        runner.StartCoroutine(HidePlatformCoroutine(runner));
    }

    private static IEnumerator HidePlatformCoroutine(MonoBehaviour runner)
    {
        yield return null;

        var playersPlace = GameObject.Find("Environment/PlayersPlace");
        if (playersPlace != null)
        {
            foreach (Transform child in playersPlace.transform)
            {
                if (child.name != "Feet")
                child.gameObject.SetActive(false);
            }
        }

        if (runner != null)
        Object.Destroy(runner.gameObject);
    }

    private sealed class CoroutineRunner : MonoBehaviour { }
}