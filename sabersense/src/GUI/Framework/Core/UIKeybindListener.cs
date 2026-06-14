// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Input;
using System;
using UnityEngine;
using UnityEngine.XR;

namespace SaberSense.GUI.Framework.Core;

internal sealed class UIKeybindListener : MonoBehaviour
{
    public Action<int>? OnCaptured;

    public Action? OnCancelled;

    private bool _captured;
    private bool _armed;

    private void Update()
    {
        if (_captured) return;

        if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
        {
            Finish(0);
            return;
        }

        var leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        var rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        if (!_armed)
        {
            if (!AnyButtonHeld(leftDevice, rightDevice))
            _armed = true;
            return;
        }

        foreach (var binding in VrButtonBindings.Pressable)
        {
            var descriptor = VrButtonBindings.Get(binding);
            if (TryCapture(DeviceFor(descriptor.Node, leftDevice, rightDevice), descriptor.Usage, binding))
            return;
        }
    }

    private static bool AnyButtonHeld(InputDevice left, InputDevice right)
    {
        foreach (var binding in VrButtonBindings.Pressable)
        {
            var descriptor = VrButtonBindings.Get(binding);
            if (IsHeld(DeviceFor(descriptor.Node, left, right), descriptor.Usage))
            return true;
        }
        return false;
    }

    private static InputDevice DeviceFor(XRNode node, InputDevice left, InputDevice right)
    => node == XRNode.LeftHand ? left : right;

    private static bool IsHeld(InputDevice device, InputFeatureUsage<bool> usage)
    {
        return device.isValid && device.TryGetFeatureValue(usage, out bool v) && v;
    }

    private bool TryCapture(InputDevice device, InputFeatureUsage<bool> usage, VrButtonBinding binding)
    {
        if (!device.isValid) return false;
        if (device.TryGetFeatureValue(usage, out bool pressed) && pressed)
        {
            Finish((int)binding);
            return true;
        }
        return false;
    }

    private void Finish(int index)
    {
        _captured = true;
        if (index > 0)
        OnCaptured?.Invoke(index);
        else
        OnCancelled?.Invoke();
        Destroy(this);
    }

    public void Cancel()
    {
        if (_captured) return;
        _captured = true;
        OnCancelled?.Invoke();
        Destroy(this);
    }
}