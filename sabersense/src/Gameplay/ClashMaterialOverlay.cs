// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Behaviors;
using SaberSense.Rendering;
using SiraUtil.Affinity;
using UnityEngine;

namespace SaberSense.Gameplay;

internal sealed class ClashMaterialOverlay : IAffinity, IClashCustomizer
{
    private ParticleSystemRenderer? _glow;
    private LiveSaber? _deferred;

    public void SetSaber(LiveSaber saber)
    {
        if (!saber.Profile.TryGetSaberAsset(out _)) return;
        if (_glow == null)
        {
            _deferred = saber;
            return;
        }

        ApplyMaterial(saber);
    }

    private void ApplyMaterial(LiveSaber saber)
    {
        if (!saber.Profile.TryGetSaberAsset(out var saberAsset)) return;
        if (saberAsset?.OwnerEntry?.AuxObjects?.GetRootComponent<ClashBehavior>() is { Material: { } mat })
        {
            _glow!.sharedMaterial = mat;
        }
    }

    [AffinityPostfix]
    [AffinityPatch(typeof(SaberClashEffect), nameof(SaberClashEffect.Start))]
    private void OnClashEffectReady(
    SaberClashEffect __instance,
    ParticleSystem ____glowParticleSystem)
    {
        _glow = ____glowParticleSystem.GetComponent<ParticleSystemRenderer>();

        if (_deferred is { } s)
        {
            ApplyMaterial(s);
            _deferred = null;
        }
    }
}