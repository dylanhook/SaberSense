// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using Zenject;

namespace SaberSense.Gameplay;

internal sealed class WorldModulationController : IInitializable, ITickable, IDisposable
{
    private const int RainMinParticles = 200;
    private const int RainMaxParticles = 15000;
    private const float RainMinEmission = 200f;
    private const float RainMaxEmission = 10000f;

    private const int SnowMinParticles = 100;
    private const int SnowMaxParticles = 6000;
    private const float SnowMinEmission = 40f;
    private const float SnowMaxEmission = 1200f;

    private const float RainSpawnHeight = 8f;
    private const float RainSpawnRotationX = 90f;
    private const float RainMinLifetime = 0.5f;
    private const float RainMaxLifetime = 1.0f;
    private const float RainMinSpeed = 12f;
    private const float RainMaxSpeed = 20f;
    private const float RainMinDropSize = 0.012f;
    private const float RainMaxDropSize = 0.028f;
    private const float RainBoxWidth = 50f;
    private const float RainBoxDepth = 50f;
    private const float RainBoxHeight = 0.1f;
    private const float RainNoiseStrength = 0.15f;
    private const float RainNoiseFrequency = 0.6f;
    private const float RainNoiseScroll = 0.3f;
    private const float RainLengthScale = 2f;
    private const float RainVelocityScale = 0.04f;

    private const float SnowSpawnHeight = 2f;
    private const float SnowMinLifetime = 6f;
    private const float SnowMaxLifetime = 14f;
    private const float SnowMinSpeed = 0.03f;
    private const float SnowMaxSpeed = 0.12f;
    private const float SnowMinFlakeSize = 0.01f;
    private const float SnowMaxFlakeSize = 0.045f;
    private const float SnowGravity = 0.06f;
    private const float SnowBoxWidth = 50f;
    private const float SnowBoxHeight = 30f;
    private const float SnowBoxDepth = 80f;
    private const float SnowNoiseMin = 0.2f;
    private const float SnowNoiseMax = 0.5f;
    private const float SnowNoiseFrequency = 0.35f;
    private const float SnowNoiseScroll = 0.15f;
    private const float SnowSpinMin = -0.5f;
    private const float SnowSpinMax = 0.5f;

    private const float CollisionRadiusScale = 0.5f;

    private const float StrengthMax = 100f;

    private readonly ModSettings _settings;
    private readonly ViewVisibilityService? _viewVis;
    private readonly IModLogger _log;

    private GameObject? _root;
    private readonly Dictionary<int, GameObject> _activeSystems = [];
    private readonly ProceduralMaterialFactory _materialFactory;
    private NetworkEmitter? _networkEmitter;
    private WorldModConfig? _subscribedWorldMod;
    private Camera? _cachedCamera;

    public WorldModulationController(ModSettings settings, ShaderRegistry shaders, IModLogger log, [InjectOptional] ViewVisibilityService? viewVis)
    {
        _settings = settings;
        _viewVis = viewVis;
        _log = log.ForSource(nameof(WorldModulationController));
        _materialFactory = new(shaders);
    }

    public void Initialize()
    {
        _root = new GameObject("WorldModulation");

        if (_settings is not null)
        {
            _settings.PropertyChanged += OnConfigChanged;
            if (_settings.Visibility is not null)
            _settings.Visibility.PropertyChanged += OnVisibilityChanged;
            ResubscribeWorldMod();
            Rebuild();
        }
    }

    public void Tick()
    {
        if (_root == null) return;

        if (_cachedCamera == null) _cachedCamera = Camera.main;
        if (_cachedCamera != null)
        _root.transform.position = _cachedCamera.transform.position;

        _networkEmitter?.Tick(Time.deltaTime, _cachedCamera != null ? _cachedCamera.transform.position : Vector3.zero);
    }

    public void Dispose()
    {
        try
        {
            if (_settings is not null)
            {
                _settings.PropertyChanged -= OnConfigChanged;
                if (_settings.Visibility is not null)
                _settings.Visibility.PropertyChanged -= OnVisibilityChanged;
                if (_subscribedWorldMod is not null)
                _subscribedWorldMod.PropertyChanged -= OnWorldModChanged;
                _subscribedWorldMod = null;
            }
            if (_root != null)
            UnityEngine.Object.Destroy(_root);
            _activeSystems.Clear();
            _networkEmitter = null;
            _materialFactory.Dispose();
        }
        catch (Exception ex)
        {
            _log?.Error($"Dispose failed: {ex}");
        }
    }

    private void OnConfigChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null || e.PropertyName == nameof(ModSettings.WorldMod))
        {
            ResubscribeWorldMod();
            Rebuild();
            return;
        }

        if (e.PropertyName == "Visibility" ||
        e.PropertyName.StartsWith("WorldMod.") ||
        e.PropertyName.StartsWith("Visibility."))
        Rebuild();
    }

    private void OnVisibilityChanged(object sender, PropertyChangedEventArgs e) => Rebuild();

    private void OnWorldModChanged(object sender, PropertyChangedEventArgs e) => Rebuild();

    private void ResubscribeWorldMod()
    {
        if (_subscribedWorldMod is not null)
        _subscribedWorldMod.PropertyChanged -= OnWorldModChanged;

        _subscribedWorldMod = _settings?.WorldMod;

        if (_subscribedWorldMod is not null)
        _subscribedWorldMod.PropertyChanged += OnWorldModChanged;
    }

    private static float StrengthCurve(float strength)
    {
        float t = strength / StrengthMax;
        return t * t;
    }

    private GameObject CreateParticleRoot(string name, Vector3 localPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root!.transform, false);
        go.transform.localPosition = localPosition;
        return go;
    }

    private static void SetupFadeGradient(ParticleSystem ps, GradientAlphaKey[] alphaKeys)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
        [new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f)],
        alphaKeys
        );
        col.color = gradient;
    }

    private void Rebuild()
    {
        foreach (var kvp in _activeSystems)
        {
            if (kvp.Value != null) UnityEngine.Object.Destroy(kvp.Value);
        }
        _activeSystems.Clear();
        _networkEmitter = null;

        if (_root == null || _settings is null) return;
        if (!_settings.WorldMod.Enabled) return;

        var modes = _settings.WorldMod.Modes;
        if (modes is null || modes.Count is 0) return;

        if (_settings.WorldMod.MenuOnly &&
        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "GameCore")
        return;

        float s = StrengthCurve(_settings.WorldMod.Strength);

        foreach (var mode in modes)
        {
            if (!WorldModulationOptions.IsMode(mode)) continue;
            if (_activeSystems.ContainsKey(mode)) continue;

            GameObject? modeRoot = mode switch
            {
                (int)WorldModulationMode.Rain => CreateRainSystem(s),
                (int)WorldModulationMode.Snow => CreateSnowSystem(s),
                (int)WorldModulationMode.Network => CreateNetworkSystem(s),
                _ => null
            };

            if (modeRoot != null)
            {
                _viewVis?.ApplyLayers(modeRoot, ViewFeature.WorldModulation);
                _activeSystems[mode] = modeRoot;
            }
        }
    }

    private GameObject CreateRainSystem(float s)
    {
        var go = CreateParticleRoot("Rain", new Vector3(0f, RainSpawnHeight, 0f));
        go.transform.localEulerAngles = new Vector3(RainSpawnRotationX, 0f, 0f);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(RainMinLifetime, RainMaxLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(RainMinSpeed, RainMaxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(RainMinDropSize, RainMaxDropSize);
        main.gravityModifier = 0f;
        main.maxParticles = (int)Mathf.Lerp(RainMinParticles, RainMaxParticles, s);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(RainBoxWidth, RainBoxDepth, RainBoxHeight);

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Lerp(RainMinEmission, RainMaxEmission, s);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = RainNoiseStrength;
        noise.frequency = RainNoiseFrequency;
        noise.scrollSpeed = RainNoiseScroll;
        noise.quality = ParticleSystemNoiseQuality.Low;

        SetupFadeGradient(ps, [
        new GradientAlphaKey(0.1f, 0f), new GradientAlphaKey(0.9f, 0.05f),
        new GradientAlphaKey(0.9f, 0.8f), new GradientAlphaKey(0f, 1f)
        ]);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = RainLengthScale;
        renderer.velocityScale = RainVelocityScale;
        renderer.material = _materialFactory.GetSoftParticleMaterial();

        ApplyModeColor(ps, new Color(0.7f, 0.85f, 1f, 0.5f), WorldModulationMode.Rain);
        ApplyCollision(ps);

        return go;
    }

    private GameObject CreateSnowSystem(float s)
    {
        var go = CreateParticleRoot("Snow", new Vector3(0f, SnowSpawnHeight, 0f));

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(SnowMinLifetime, SnowMaxLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(SnowMinSpeed, SnowMaxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(SnowMinFlakeSize, SnowMaxFlakeSize);
        main.gravityModifier = SnowGravity;
        main.maxParticles = (int)Mathf.Lerp(SnowMinParticles, SnowMaxParticles, s);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Lerp(SnowMinEmission, SnowMaxEmission, s);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(SnowBoxWidth, SnowBoxHeight, SnowBoxDepth);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(SnowNoiseMin, SnowNoiseMax);
        noise.frequency = SnowNoiseFrequency;
        noise.scrollSpeed = SnowNoiseScroll;
        noise.quality = ParticleSystemNoiseQuality.Medium;
        noise.damping = true;

        var rotOverLifetime = ps.rotationOverLifetime;
        rotOverLifetime.enabled = true;
        rotOverLifetime.z = new ParticleSystem.MinMaxCurve(SnowSpinMin, SnowSpinMax);

        SetupFadeGradient(ps, [
        new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.1f),
        new GradientAlphaKey(0.9f, 0.65f), new GradientAlphaKey(0f, 1f)
        ]);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
        new Keyframe(0f, 0.3f), new Keyframe(0.15f, 1f),
        new Keyframe(0.75f, 1f), new Keyframe(1f, 0.2f)
        ));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = _materialFactory.GetSoftParticleMaterial();

        ApplyModeColor(ps, new Color(1f, 1f, 1f, 0.65f), WorldModulationMode.Snow);
        ApplyCollision(ps);

        return go;
    }

    private GameObject CreateNetworkSystem(float s)
    {
        _networkEmitter = new(_root!.transform, s, _materialFactory, _settings);
        return _networkEmitter.Root;
    }

    private static void ApplyCollision(ParticleSystem ps)
    {
        var col = ps.collision;
        col.enabled = true;
        col.type = ParticleSystemCollisionType.World;
        col.mode = ParticleSystemCollisionMode.Collision3D;
        col.dampen = 0f;
        col.bounce = 0f;
        col.lifetimeLoss = 1f;
        col.quality = ParticleSystemCollisionQuality.Medium;
        col.radiusScale = CollisionRadiusScale;
    }

    private void ApplyModeColor(ParticleSystem ps, Color defaultColor, WorldModulationMode mode)
    {
        var main = ps.main;
        main.startColor = GetOverrideOrDefault(defaultColor, mode);
    }

    private Color GetOverrideOrDefault(Color defaultColor, WorldModulationMode mode)
    {
        if (_settings is null || !_settings.WorldMod.OverrideColor) return defaultColor;

        return mode switch
        {
            WorldModulationMode.Rain => _settings.WorldMod.RainColor,
            WorldModulationMode.Snow => _settings.WorldMod.SnowColor,
            WorldModulationMode.Network => _settings.WorldMod.NetworkColor,
            _ => defaultColor
        };
    }
}