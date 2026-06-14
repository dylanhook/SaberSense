// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core;
using SaberSense.Rendering;
using UnityEngine;

namespace SaberSense.Customization;
internal sealed class GripAttachment
{
    public readonly Transform LeftMount;
    public readonly Transform RightMount;

    private readonly MenuPlayerController _menuPlayer;
    private bool _leftVisible = true;
    private bool _rightVisible = true;

    private const string PRIMARY_NODE = "MenuHandle";
    private static readonly string[] FX_NODES = ["Glowing", "Normal", "FakeGlow0", "FakeGlow1"];

    public GripAttachment(MenuPlayerController menuPlayerController)
    {
        _menuPlayer = menuPlayerController;

        LeftMount = new GameObject("SaberGrabContainer").transform;
        var leftParent = menuPlayerController.leftController.transform.Find(PRIMARY_NODE) ?? menuPlayerController.leftController.transform;
        LeftMount.SetParent(leftParent, false);

        RightMount = new GameObject("SaberGrabContainerRight").transform;
        var rightParent = menuPlayerController.rightController.transform.Find(PRIMARY_NODE) ?? menuPlayerController.rightController.transform;
        RightMount.SetParent(rightParent, false);
    }

    public Transform GetMount(SaberHand hand)
    => hand == SaberHand.Left ? LeftMount : RightMount;

    public void Attach(LiveSaber saber, SaberHand hand)
    {
        SetGripVisible(hand, false);
        saber.SetParent(GetMount(hand));
        saber.CachedTransform.localPosition = Vector3.zero;
        saber.CachedTransform.localRotation = Quaternion.identity;
    }

    public void SetGripVisible(SaberHand hand, bool visible)
    {
        ref bool current = ref (hand == SaberHand.Left ? ref _leftVisible : ref _rightVisible);
        if (current == visible) return;
        current = visible;
        var controller = hand == SaberHand.Left ? _menuPlayer.leftController : _menuPlayer.rightController;
        ToggleRenderers(controller.transform, visible);
    }

    private static void ToggleRenderers(Transform controllerTransform, bool visible)
    {
        if (controllerTransform == null || controllerTransform.Find(PRIMARY_NODE) is not { } handle) return;

        foreach (var nodeName in FX_NODES)
        {
            if (handle.Find(nodeName) is { } child && child.GetComponent<MeshRenderer>() is { } renderer)
            {
                renderer.enabled = visible;
            }
        }
    }
}