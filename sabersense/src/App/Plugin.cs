// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using HarmonyLib;
using IPA;
using IPA.Loader;
using IPA.Utilities;
using SaberSense.Configuration;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using SaberSense.Installers;
using SiraUtil.Zenject;
using System;
using System.IO;
using System.Reflection;
using UnityEngine.XR.OpenXR;
using IPALogger = IPA.Logging.Logger;

namespace SaberSense;

[Plugin(RuntimeOptions.SingleStartInit), NoEnableDisable]
public class Plugin
{
    private const string HarmonyId = "com.dylan.sabersense";
    private Harmony? _harmony;
    private LogFileWriter? _fileWriter;

    public static bool MultiPassEnabled
    {
        get
        {
            try
            {
                if (RuntimeEnvironment.IsFpfcActive)
                return true;
                var openXrSettings = OpenXRSettings.Instance;
                return openXrSettings != null && openXrSettings.renderMode == OpenXRSettings.RenderMode.MultiPass;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    [OnExit]
    public void OnExit()
    {
        _harmony?.UnpatchSelf();
        _fileWriter?.Dispose();
    }

    [Init]
    public void Init(IPALogger logger, Zenjector zenjector, PluginMetadata metadata)
    {
        var ipaAdapter = new IPALoggerAdapter(logger);

        _fileWriter = new LogFileWriter(
        Path.Combine(UnityGame.UserDataPath, "SaberSense", "latest_log.txt"));
        var ringBuffer = new LogRingBuffer();
        var log = new LoggingLogger(ipaAdapter, _fileWriter, ringBuffer);

        ModLogger.Initialize(log);
        ErrorBoundary.Run(() =>
        {
            _harmony = new Harmony(HarmonyId);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            UnityEngine.SceneManagement.SceneManager.activeSceneChanged +=
            (_, next) => Patches.HarmonyBridge.IsInGameCore = next.name == "GameCore";

            if (!MultiPassEnabled)
            {
                log.Info("Running in Single Pass Instanced mode. Sabers requiring multi-pass rendering will be hidden.");
            }

            zenjector.UseLogger(logger);
            zenjector.Install<CoreInstaller>(Location.App, logger, metadata, log, _fileWriter, ringBuffer);
            zenjector.Install<MenuInstaller>(Location.Menu);
            zenjector.Install<GameplayInstaller>(Location.Player | Location.MultiPlayer);
        }, log);
    }
}