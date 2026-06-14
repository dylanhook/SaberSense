// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Catalog;
using SaberSense.Catalog.Model;
using SaberSense.Core;
using SaberSense.Core.Logging;
using SaberSense.Persistence;
using System;
using System.Threading.Tasks;

namespace SaberSense.Profiles;

public class SaberProfile
{
    public readonly SaberHand Hand;

    public SaberAssetDefinition? Equipped { get; internal set; }

    public SaberScale Scale { get; internal set; } = SaberScale.Unit;

    internal SaberCustomization? Customization { get; set; }

    public bool IsBlank => Equipped is null;

    public SaberProfile(SaberHand hand)
    {
        Hand = hand;
    }

    public bool TryGetSaberAsset(out SaberAssetDefinition? saberAsset)
    {
        saberAsset = Equipped;
        return saberAsset is not null;
    }

    public void ApplyAssetEntry(SaberAssetEntry entry)
    {
        Equipped = Hand == SaberHand.Left ? entry.LeftPiece : entry.RightPiece;
    }

    public void PropagateChanges()
    {
        Equipped?.OwnerEntry?.SyncPiece(Equipped);
    }
}

internal static class SaberProfileCodec
{
    private readonly struct ProfileField
    {
        public readonly string Key;
        public readonly System.Action<SaberProfile, JObject> Read;
        public readonly System.Action<SaberProfile, JObject> Write;

        public ProfileField(
        string key,
        System.Action<SaberProfile, JObject> read,
        System.Action<SaberProfile, JObject> write)
        {
            Key = key;
            Read = read;
            Write = write;
        }
    }

    private static readonly ProfileField[] Fields =
    [
    new(nameof(SaberProfile.Scale),
    read: (target, obj) =>
    {
        if (obj.TryGetValue(nameof(SaberProfile.Scale), out var scaleTkn) && scaleTkn is JObject scaleObj)
        {
            target.Scale = new()
            {
                Length = SaberScale.Clamp(scaleObj.Value<float?>("Length") ?? 1f),
                Width = SaberScale.Clamp(scaleObj.Value<float?>("Width") ?? 1f)
            };
        }
    },

    write: (source, obj) => obj[nameof(SaberProfile.Scale)] = JObject.FromObject(source.Scale)),
    ];

    private const string SaberKey = "Saber";

    public static async Task ReadFromAsync(SaberProfile target, JObject obj, Serializer serializer)
    {
        AssertFieldLegs();

        foreach (var field in Fields)
        field.Read(target, obj);

        if (obj.TryGetValue(SaberKey, out var saberToken) && saberToken is JObject saberObj)
        {
            var path = saberObj["Path"];
            if (path is null) return;

            var savedHash = saberObj["ContentHash"]?.ToObject<string>();

            var entry = await serializer.ResolveSaberEntryAsync(path.ToObject<string>()!);
            if (entry is null) return;

            if (!string.IsNullOrEmpty(savedHash) &&
            !string.IsNullOrEmpty(entry.LeftPiece?.Asset?.ContentHash) &&
            !string.Equals(savedHash, entry.LeftPiece!.Asset!.ContentHash, StringComparison.OrdinalIgnoreCase))
            {
                var actualHash = entry.LeftPiece!.Asset!.ContentHash!;
                ModLogger.ForSource("Config").Warn($"Saber at '{path}' has different content hash - skipping (expected {savedHash![..8]}..., got {actualHash[..8]}...)");
                return;
            }

            var loaded = entry[target.Hand];
            target.Equipped = loaded;

            if (loaded is not null)
            {
                var customization = SaberCustomization.SeedFromDefinition(loaded);
                await customization.ReadFromAsync(saberObj, serializer);
                target.Customization = customization;
            }
        }
    }

    public static JObject WriteTo(SaberProfile source, Serializer serializer)
    {
        AssertFieldLegs();

        var obj = new JObject();

        foreach (var field in Fields)
        field.Write(source, obj);

        if (source.Equipped is { } equipped &&
        equipped.Asset?.RelativePath != DefaultSaberProvider.DefaultSaberPath)
        {
            var saberJson = (JObject)equipped.WriteTo(serializer);

            source.Customization?.WriteTo(saberJson, serializer);

            obj[SaberKey] = saberJson;
        }

        return obj;
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private static void AssertFieldLegs()
    {
        for (var i = 0; i < Fields.Length; i++)
        {
            if (Fields[i].Read is null || Fields[i].Write is null)
            throw new System.InvalidOperationException(
            $"SaberProfileCodec field '{Fields[i].Key}' is missing a read or write leg.");

            for (var j = i + 1; j < Fields.Length; j++)
            {
                if (Fields[i].Key == Fields[j].Key)
                throw new System.InvalidOperationException(
                $"SaberProfileCodec field '{Fields[i].Key}' is declared more than once.");
            }
        }
    }
}