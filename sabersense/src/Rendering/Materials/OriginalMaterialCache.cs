// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Catalog.Model;
using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using SaberSense.Persistence;
using SaberSense.Rendering.Shaders;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Rendering.Materials;

internal sealed class OriginalMaterialCache : IDisposable
{
    private readonly ShaderIntrospector _shaderCache;
    private readonly IModLogger _log;
    private readonly IJsonProvider _jsonProvider;

    private readonly Dictionary<(string saber, string mat, string prop), JToken> _values = [];

    private readonly Dictionary<(string saber, string mat, string prop), Texture> _textures = [];

    private readonly HashSet<string> _snapshotted = [];

    private string? _currentSaber;

    private string CurrentSaberPath => _currentSaber ?? string.Empty;
    private readonly MaterialNameResolver _nameResolver = new();

    public OriginalMaterialCache(ShaderIntrospector shaderCache, IModLogger log, IJsonProvider jsonProvider)
    {
        _shaderCache = shaderCache;
        _log = log.ForSource(nameof(OriginalMaterialCache));
        _jsonProvider = jsonProvider;
    }

    public void SetContext(string saberPath)
    {
        _currentSaber = saberPath ?? string.Empty;
    }

    public void Snapshot(SaberAssetDefinition def, string saberPath)
    {
        saberPath ??= string.Empty;
        _currentSaber = saberPath;

        if (_snapshotted.Contains(saberPath)) return;
        if (def?.AuxObjects is null || _shaderCache is null) return;

        var srcPrefab = def.AuxObjects.SourcePrefab;
        if (srcPrefab == null) return;

        var leftTransform = srcPrefab.transform.Find("LeftSaber");
        if (leftTransform == null) return;

        var renderers = new List<Renderer>();
        leftTransform.GetComponentsInChildren(true, renderers);

        TraverseRendererProperties(renderers, (mat, baseName, prop) =>
        {
            var json = MaterialPropertyCodec.Encode(prop, mat, _jsonProvider);
            if (json is not null)
            _values[(saberPath, baseName, prop.Name)] = json;

            if (prop.Kind == PropertyKind.Texture)
            {
                var tex = mat.GetTexture(prop.Id);
                if (tex != null)
                _textures[(saberPath, baseName, prop.Name)] = tex;
            }
        });

        var trails = SaberComponentDiscovery.GetTrails(leftTransform.gameObject);
        if (trails is not null)
        {
            foreach (var trail in trails)
            {
                var mat = trail.TrailMaterial;
                if (mat == null || mat.shader == null) continue;

                var shaderInfo = _shaderCache[mat.shader];
                if (shaderInfo is null || shaderInfo.Count is 0) continue;

                var trailMatName = MaterialNameResolver.StripInstanceSuffix(mat.name);

                foreach (var prop in shaderInfo)
                {
                    var key = (saberPath, trailMatName, prop.Name);
                    if (_values.ContainsKey(key)) continue;

                    var json = MaterialPropertyCodec.Encode(prop, mat, _jsonProvider);
                    if (json is not null) _values[key] = json;

                    if (prop.Kind == PropertyKind.Texture)
                    {
                        var tex = mat.GetTexture(prop.Id);
                        if (tex != null) _textures[key] = tex;
                    }
                }
            }
        }

        _snapshotted.Add(saberPath);
    }

    private T GetOriginalProperty<T>(string matName, string propName, T fallback)
    {
        var key = (CurrentSaberPath, matName, propName);
        if (_values.TryGetValue(key, out var json))
        {
            try { return json.ToObject<T>(_jsonProvider.Json)!; }
            catch (System.Exception ex) { _log.Warn($"Failed to deserialize {typeof(T).Name} for {matName}.{propName}: {ex.Message}"); }
        }
        return fallback;
    }

    public Color GetOriginalColor(string matName, string propName)
    => GetOriginalProperty(matName, propName, Color.white);

    public float GetOriginalFloat(string matName, string propName)
    => GetOriginalProperty(matName, propName, 0f);

    public bool GetOriginalToggle(string matName, string propName)
    {
        return GetOriginalFloat(matName, propName) > 0;
    }

    public JToken GetOriginalValue(string matName, string propName)
    {
        var key = (CurrentSaberPath, matName, propName);
        _values.TryGetValue(key, out var json);
        return json;
    }

    public Texture GetOriginalTexture(string matName, string propName)
    {
        var key = (CurrentSaberPath, matName, propName);
        _textures.TryGetValue(key, out var tex);
        return tex;
    }

    public void RestoreOriginals(GameObject saberRoot)
    {
        if (saberRoot == null || _shaderCache is null) return;
        var saberPath = CurrentSaberPath;

        var renderers = new List<Renderer>();
        saberRoot.GetComponentsInChildren(true, renderers);

        TraverseRendererProperties(renderers, (mat, baseName, prop) =>
        {
            var key = (saberPath, baseName, prop.Name);
            if (_values.TryGetValue(key, out var json))
            {
                _textures.TryGetValue(key, out var tex);
                MaterialPropertyCodec.Decode(prop, json, mat, _jsonProvider, tex);
            }
        });
    }

    private void TraverseRendererProperties(
    IEnumerable<Renderer> renderers,
    Action<Material, string, ShaderProperty> perProp)
    {
        var nameResolver = _nameResolver.BeginScope();
        foreach (var rend in renderers)
        {
            if (rend == null) continue;
            var mats = rend.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || mats[i].shader == null) continue;
                var shaderInfo = _shaderCache[mats[i].shader];
                if (shaderInfo is null || shaderInfo.Count is 0) continue;
                string baseName = nameResolver.Resolve(mats[i]);
                foreach (var prop in shaderInfo)
                perProp(mats[i], baseName, prop);
            }
        }
    }

    public void Dispose()
    {
        _values.Clear();
        _textures.Clear();
        _snapshotted.Clear();
        _currentSaber = null;
    }
}