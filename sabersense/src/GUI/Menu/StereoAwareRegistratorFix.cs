// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using CameraUtils.Behaviours;
using UnityEngine;

namespace SaberSense.GUI.Menu;

internal sealed class StereoAwareRegistratorFix : MonoBehaviour
{
    private Camera? _camera;
    private AutoCameraRegistrator? _registrator;
    private bool _lastStereo;

    private void Start()
    {
        _camera = GetComponent<Camera>();
        _registrator = GetComponent<AutoCameraRegistrator>();

        if (_camera == null || _registrator == null)
        {
            Destroy(this);
            return;
        }

        _lastStereo = _camera.stereoEnabled;
    }

    private void Update()
    {
        if (_camera == null || _registrator == null)
        {
            Destroy(this);
            return;
        }

        bool stereo = _camera.stereoEnabled;
        if (stereo != _lastStereo)
        {
            _registrator.enabled = false;
            _registrator.enabled = true;
            Destroy(this);
        }
    }
}