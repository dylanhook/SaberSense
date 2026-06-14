// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaberSense.Catalog;
using SaberSense.Core;
using SaberSense.Rendering.Shaders;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Rendering.Materials;

internal static class MaterialPropertyApplier
{
    public static JToken? ResolveSplitValue(JToken? entry, SaberHand hand)
    {
        if (entry is null) return null;
        if (entry.Type != JTokenType.Object) return entry;
        var obj = (JObject)entry;
        if (!obj.ContainsKey("Left")) return entry;
        if (!obj.ContainsKey("Right")) return entry;
        return hand == SaberHand.Left ? obj["Left"] : obj["Right"];
    }

    public static bool IsSplit(JToken entry)
    {
        return entry is JObject obj && obj.ContainsKey("Left");
    }

    public static List<(int propId, string texName)>? ApplyAll(
    Material mat, JObject overrides, SaberHand hand,
    JsonSerializer json, TextureCacheRegistry? textureCache = null)
    {
        if (mat == null || overrides is null) return null;

        List<(int, string)>? asyncTextures = null;

        foreach (var prop in ShaderIntrospector.Enumerate(mat.shader))
        {
            var entry = overrides[prop.Name];
            if (entry is null) continue;

            var resolved = ResolveSplitValue(entry, hand);
            if (resolved is null) continue;

            var asyncTex = MaterialPropertyCodec.Apply(prop, resolved, mat, json, textureCache);
            if (asyncTex is not null)
            {
                asyncTextures ??= [];
                asyncTextures.Add((prop.Id, asyncTex));
            }
        }

        return asyncTextures;
    }
}