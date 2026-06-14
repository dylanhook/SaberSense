// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using CameraUtils.Core;
using SaberSense.Core.Logging;
using UnityEngine;
using Zenject;

namespace SaberSense.GUI.Menu;

internal sealed class MenuCameraRegistrator : IInitializable
{
    private readonly IModLogger _log;

    public MenuCameraRegistrator(IModLogger log)
    {
        _log = log.ForSource(nameof(MenuCameraRegistrator));
    }

    public void Initialize()
    {
        var allCameras = Resources.FindObjectsOfTypeAll<Camera>();
        foreach (var camera in allCameras)
        {
            switch (camera.name)
            {
                case "MenuMainCamera":
                RegisterHmd(camera);
                break;
                case "SmoothCamera":
                RegisterDesktop(camera);
                break;
            }
        }
    }

    private void RegisterHmd(Camera camera)
    {
        _log.Info($"Registering '{camera.name}' as HMD (stereo={camera.stereoEnabled})");

        var registrator = VisibilityUtils.GetOrAddCameraRegistrator(camera);
        registrator.AdditionalFlags |= CameraFlags.FirstPerson;

        CullingMaskUtils.SetupHMDCamera(camera);

        if (camera.gameObject.GetComponent<StereoAwareRegistratorFix>() == null)
        camera.gameObject.AddComponent<StereoAwareRegistratorFix>();
    }

    private void RegisterDesktop(Camera camera)
    {
        _log.Info($"Registering '{camera.name}' as Desktop");

        var registrator = VisibilityUtils.GetOrAddCameraRegistrator(camera);
        registrator.AdditionalFlags |= CameraFlags.FirstPerson | CameraFlags.Composition;

        CamerasManager.RegisterDesktopCamera(camera, CameraFlags.FirstPerson | CameraFlags.Composition);
    }
}