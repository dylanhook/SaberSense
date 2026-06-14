// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Core;
using SaberSense.Core.Utilities;
using SaberSense.Customization;
using SaberSense.Persistence;
using SaberSense.Profiles;
using SaberSense.Rendering.Shaders;
using UnityEngine;

namespace SaberSense.Rendering.Materials;

internal sealed class MaterialOverrideService
{
    private readonly ShaderIntrospector _shaderCache;
    private readonly EditScope _scope;
    private readonly SaberLoadout _loadout;
    private readonly IJsonProvider _jsonProvider;

    public MaterialOverrideService(ShaderIntrospector shaderCache, IJsonProvider jsonProvider, EditScope scope, SaberLoadout loadout)
    {
        _shaderCache = shaderCache;
        _jsonProvider = jsonProvider;
        _scope = scope;
        _loadout = loadout;
    }

    public void Snapshot(string materialName, Material mat, SaberHand? sourceHand = null)
    {
        if (_shaderCache is null || mat?.shader == null) return;

        bool overrideOff = mat.HasProperty(ShaderUtils.CustomColorToggleId) && mat.GetFloat(ShaderUtils.CustomColorToggleId) > 0.5f;

        var focusedHand = sourceHand ?? _scope.FocusedHand;
        var focusedCustomization = _loadout[focusedHand].Customization;
        var otherCustomization = _loadout[focusedHand.Other()].Customization;

        if (focusedCustomization is null) return;

        JObject overrideObj;
        if (focusedCustomization.MaterialOverrides.TryGetValue(materialName, out var existing))
        overrideObj = existing;
        else
        overrideObj = new JObject();

        foreach (var prop in _shaderCache[mat.shader]!)
        {
            if (overrideOff && prop.Name == "_Color") continue;
            var json = MaterialPropertyCodec.Encode(prop, mat, _jsonProvider);
            if (json is null)
            {
                if (prop.Kind == PropertyKind.Texture)
                {
                    bool isSplitTex = focusedCustomization.IsPropertySplit(materialName, prop.Name);
                    if (isSplitTex)
                    {
                        if (overrideObj[prop.Name] is JObject existingProp && existingProp.ContainsKey("Left"))
                        existingProp["Left"] = new JValue("");
                    }
                    else
                    {
                        overrideObj[prop.Name] = new JValue("");
                    }
                }
                continue;
            }

            bool isSplit = focusedCustomization.IsPropertySplit(materialName, prop.Name);
            if (isSplit)
            {
                if (overrideObj[prop.Name] is JObject existingProp && existingProp.ContainsKey("Left"))
                existingProp["Left"] = json;
            }
            else
            {
                overrideObj[prop.Name] = json;
            }
        }

        focusedCustomization.MaterialOverrides[materialName] = overrideObj;

        if (_scope.Linked && otherCustomization is not null && otherCustomization != focusedCustomization)
        otherCustomization.MaterialOverrides[materialName] = (JObject)overrideObj.DeepClone();
    }

    public void SnapshotSplit(string materialName, string propName,
    JToken value, SaberHand hand)
    {
        var focusedHand = _scope.FocusedHand;
        var focusedCustomization = _loadout[focusedHand].Customization;
        var otherCustomization = _loadout[focusedHand.Other()].Customization;
        if (focusedCustomization is null) return;

        focusedCustomization.SetPropertyForHand(materialName, propName, value, hand);

        if (_scope.Linked && otherCustomization is not null && otherCustomization != focusedCustomization)
        otherCustomization.SetPropertyForHand(materialName, propName, value, hand);
    }
}