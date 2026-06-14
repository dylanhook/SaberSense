// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Catalog.Model;
using SaberSense.Core.Logging;
using SaberSense.Persistence;
using SaberSense.Rendering;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Profiles;

internal static class TrailSettingsCodec
{
    private static readonly IModLogger Log = ModLogger.ForSource(nameof(TrailSettingsCodec));

    private readonly struct TrailField
    {
        public readonly string Key;
        public readonly System.Action<TrailSettings, JObject, Serializer> Read;
        public readonly System.Action<TrailSettings, JObject, Serializer> Write;

        public readonly bool AlwaysWritten;

        public TrailField(
        string key,
        System.Action<TrailSettings, JObject, Serializer> read,
        System.Action<TrailSettings, JObject, Serializer> write,
        bool alwaysWritten = true)
        {
            Key = key;
            Read = read;
            Write = write;
            AlwaysWritten = alwaysWritten;
        }
    }

    private static readonly TrailField[] Fields =
    [
    new(nameof(TrailSettings.TrailLength),
    (t, o, _) => t.TrailLength = o.Value<int?>(nameof(t.TrailLength)) ?? t.TrailLength,
    (t, o, _) => o[nameof(t.TrailLength)] = t.TrailLength),
    new(nameof(TrailSettings.TrailWidth),
    (t, o, _) => t.TrailWidth = o.Value<float?>(nameof(t.TrailWidth)) ?? t.TrailWidth,
    (t, o, _) => o[nameof(t.TrailWidth)] = t.TrailWidth),
    new(nameof(TrailSettings.WhiteBlend),
    (t, o, _) => t.WhiteBlend = o.Value<float?>(nameof(t.WhiteBlend)) ?? t.WhiteBlend,
    (t, o, _) => o[nameof(t.WhiteBlend)] = t.WhiteBlend),
    new(nameof(TrailSettings.TextureClamped),
    (t, o, _) => t.TextureClamped = o.Value<bool?>(nameof(t.TextureClamped)) ?? t.TextureClamped,
    (t, o, _) => o[nameof(t.TextureClamped)] = t.TextureClamped),
    new(nameof(TrailSettings.Reversed),
    (t, o, _) => t.Reversed = o.Value<bool?>(nameof(t.Reversed)) ?? t.Reversed,
    (t, o, _) => o[nameof(t.Reversed)] = t.Reversed),
    new(nameof(TrailSettings.OriginAssetPath),
    (t, o, _) => t.OriginAssetPath = o.Value<string>(nameof(t.OriginAssetPath)) ?? "",
    (t, o, _) => o[nameof(t.OriginAssetPath)] = t.OriginAssetPath),
    new(nameof(TrailSettings.SaberExtent),
    (t, o, _) => t.SaberExtent = o.Value<float?>(nameof(t.SaberExtent)) ?? t.SaberExtent,
    (t, o, _) => o[nameof(t.SaberExtent)] = t.SaberExtent),
    new(nameof(TrailSettings.MaxTrailWidth),
    (t, o, _) => t.MaxTrailWidth = o.Value<float?>(nameof(t.MaxTrailWidth)) ?? t.MaxTrailWidth,
    (t, o, _) => o[nameof(t.MaxTrailWidth)] = t.MaxTrailWidth),
    new(nameof(TrailSettings.TrailEndZ),
    (t, o, _) => t.TrailEndZ = o.Value<float?>(nameof(t.TrailEndZ)) ?? t.TrailEndZ,
    (t, o, _) => o[nameof(t.TrailEndZ)] = t.TrailEndZ),
    new(nameof(TrailSettings.PositionOffset),
    (t, o, ser) =>
    {
        if (o.TryGetValue(nameof(t.PositionOffset), out var posToken))
        t.PositionOffset = posToken.ToObject<Vector3>(ser.Json);
    },
    (t, o, ser) => o[nameof(t.PositionOffset)] = JToken.FromObject(t.PositionOffset, ser.Json)),
    new(nameof(TrailSettings.NativeTextureWrap),
    (t, o, ser) =>
    {
        if (o.TryGetValue(nameof(t.NativeTextureWrap), out var wrapToken))
        t.NativeTextureWrap = wrapToken.ToObject<TextureWrapMode?>(ser.Json);
    },
    (t, o, ser) =>
    {
        if (t.NativeTextureWrap.HasValue)
        o[nameof(t.NativeTextureWrap)] = JToken.FromObject(t.NativeTextureWrap.Value, ser.Json);
    },
    alwaysWritten: false),
    ];

    public static async Task ReadFromAsync(TrailSettings target, JObject obj, Serializer serializer)
    {
        AssertTableSymmetry();

        foreach (var field in Fields)
        field.Read(target, obj, serializer);

        if (target.OriginAssetPath is { Length: > 0 } originPath)
        await ResolveOriginAssetAsync(target, serializer, originPath);

        if (obj.TryGetValue("Material", out var materialToken) && materialToken is JObject matObj)
        {
            target.Material ??= new(null);
            var liveMat = target.Material.Material;
            Log.Debug($"ReadFromAsync: Material JSON has {matObj.Count} props, liveMat={(liveMat != null)} id={liveMat?.GetInstanceID()}");
            if (liveMat != null)
            await serializer.LoadMaterialAsync(matObj, liveMat);
            else
            target.DeferredMaterialJson = matObj;
        }
        else
        {
            Log.Debug($"ReadFromAsync: No 'Material' key in JSON");
        }
    }

    public static JToken WriteTo(TrailSettings source, Serializer serializer)
    {
        var obj = new JObject();

        foreach (var field in Fields)
        field.Write(source, obj, serializer);

        if (source.Material is { IsValid: true })
        obj.Add("Material", serializer.SerializeMaterial(source.Material.Material!, includeClears: true));

        AssertWriteCoverage(obj);

        return obj;
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private static void AssertTableSymmetry()
    {
        var seen = new HashSet<string>();
        foreach (var field in Fields)
        {
            if (field.Read is null || field.Write is null)
            throw new System.InvalidOperationException(
            $"TrailSettingsCodec field '{field.Key}' is missing a read or write leg.");

            if (!seen.Add(field.Key))
            throw new System.InvalidOperationException(
            $"TrailSettingsCodec field '{field.Key}' is declared more than once.");
        }
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private static void AssertWriteCoverage(JObject obj)
    {
        AssertTableSymmetry();

        foreach (var field in Fields)
        {
            if (field.AlwaysWritten && obj[field.Key] is null)
            throw new System.InvalidOperationException(
            $"TrailSettingsCodec wrote no value for table field '{field.Key}'.");
        }

        foreach (var prop in obj.Properties())
        {
            var isTableKey = false;
            foreach (var field in Fields)
            {
                if (field.Key == prop.Name) { isTableKey = true; break; }
            }

            var isSpecialCase = prop.Name is "Material";

            if (!isTableKey && !isSpecialCase)
            throw new System.InvalidOperationException(
            $"TrailSettingsCodec key '{prop.Name}' is neither a declared table field nor a " +
            "documented special case -- it would not round-trip through ReadFromAsync.");
        }
    }

    private static async Task ResolveOriginAssetAsync(TrailSettings target, Serializer serializer, string originPath)
    {
        var entry = await serializer.ResolveSaberEntryAsync(originPath);
        if (entry?.LeftPiece is not SaberAssetDefinition cs) return;

        var originTrail = cs.ExtractTrail(true);
        if (originTrail is null) return;

        target.Material = new(new UnityEngine.Material(originTrail.Material!.Material!));
        target.OriginTrails = SaberComponentDiscovery.GetTrails(cs.Prefab);
    }
}