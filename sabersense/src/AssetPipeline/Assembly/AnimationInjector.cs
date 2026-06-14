// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using UnityEngine;

namespace SaberSense.AssetPipeline.Assembly;

internal static class AnimationInjector
{
    private static readonly IModLogger Log = ModLogger.ForSource(nameof(AnimationInjector));

    internal static void MirrorAnimations(GameObject root)
    {
        if (root == null) return;
        MirrorAnimators(root.transform);
        MirrorLegacyAnimations(root.transform);
    }

    private static void MirrorAnimators(Transform root)
    {
        InjectionHelpers.MirrorComponents<Animator>(
        root,
        copyFields: static (srcAnimator, dst) =>
        {
            dst.runtimeAnimatorController = srcAnimator.runtimeAnimatorController;
            dst.avatar = srcAnimator.avatar;
            dst.applyRootMotion = srcAnimator.applyRootMotion;
            dst.updateMode = srcAnimator.updateMode;
            dst.cullingMode = srcAnimator.cullingMode;
            dst.speed = srcAnimator.speed;
            dst.keepAnimatorStateOnDisable = srcAnimator.keepAnimatorStateOnDisable;

            Log.Info(
            $"Mirrored Animator to '{dst.transform.name}' controller='{srcAnimator.runtimeAnimatorController.name}'");
        },

        skipIfTargetHas: static existing => existing != null,
        includeInactive: true,

        sourceFilter: static a => a.runtimeAnimatorController != null);
    }

    private static void MirrorLegacyAnimations(Transform root)
    {
        InjectionHelpers.MirrorComponents<Animation>(
        root,
        copyFields: static (srcAnim, dst) =>
        {
            dst.playAutomatically = srcAnim.playAutomatically;
            dst.wrapMode = srcAnim.wrapMode;
            dst.animatePhysics = srcAnim.animatePhysics;
            dst.cullingType = srcAnim.cullingType;

            foreach (AnimationState state in srcAnim)
            {
                if (state.clip != null)
                dst.AddClip(state.clip, state.name);
            }

            if (srcAnim.clip != null)
            dst.clip = srcAnim.clip;

            Log.Info(
            $"Mirrored Animation to '{dst.transform.name}' clips={srcAnim.GetClipCount()}");
        },

        skipIfTargetHas: static existing => existing != null,
        includeInactive: true);
    }
}