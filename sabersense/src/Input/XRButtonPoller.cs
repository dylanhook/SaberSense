// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.XR;

namespace SaberSense.Input;

internal abstract class XRButtonPoller<T> : MonoBehaviour where T : XRButtonPoller<T>
{
    protected static T? Instance;

    private bool _isPressed;
    private bool _isPressedDown;
    private VrButtonBinding _binding;

    protected bool IsButtonPressed => _isPressed;

    protected bool IsButtonPressedDown => _isPressedDown;

    protected VrButtonBinding ButtonBinding
    {
        get => _binding;
        set => _binding = value;
    }

    protected static void EnsureInstance(string objectName)
    {
        if (Instance != null) return;
        var go = new GameObject(objectName);
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<T>();
    }

    protected virtual void OnDestroy()
    {
        if (Instance == (T)this) Instance = null;
    }

    protected bool PollVRButton()
    {
        if (_binding is VrButtonBinding.None) return false;

        var descriptor = VrButtonBindings.Get(_binding);
        var device = InputDevices.GetDeviceAtXRNode(descriptor.Node);

        if (!device.isValid) return false;

        device.TryGetFeatureValue(descriptor.Usage, out bool value);
        return value;
    }

    protected void CommitState(bool combined, bool extraDown = false)
    {
        _isPressedDown = (combined && !_isPressed) || extraDown;
        _isPressed = combined;
    }
}