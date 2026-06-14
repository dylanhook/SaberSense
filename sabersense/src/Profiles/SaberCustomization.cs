// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Catalog.Model;
using SaberSense.Core;
using SaberSense.Core.Utilities;
using SaberSense.Persistence;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SaberSense.Profiles;

internal sealed class SaberCustomization
{
    public Dictionary<string, JObject> MaterialOverrides { get; set; } = [];

    public TrailSettings? TrailSettings { get; set; }

    public JObject? ModifierState { get; set; }

    public TransformOverrides Transform { get; set; } = new();

    private SplitPropertyManager? _splitPropertyManager;
    private SplitPropertyManager SplitProperties => _splitPropertyManager ??= new(MaterialOverrides);

    public bool IsPropertySplit(string matName, string propName) => SplitProperties.IsPropertySplit(matName, propName);
    public void SplitProperty(string matName, string propName) => SplitProperties.SplitProperty(matName, propName);
    public void UnsplitProperty(string matName, string propName) => SplitProperties.UnsplitProperty(matName, propName);
    public JToken? GetPropertyForHand(string matName, string propName, SaberHand hand)
    => SplitProperties.GetPropertyForHand(matName, propName, hand);
    public void SetPropertyForHand(string matName, string propName, JToken value, SaberHand hand)
    => SplitProperties.SetPropertyForHand(matName, propName, value, hand);

    public static SaberCustomization SeedFromDefinition(SaberAssetDefinition def)
    {
        var customization = new SaberCustomization();
        if (def?.TrailSettings is not null)
        {
            customization.TrailSettings = def.TrailSettings.Clone();
            def.ComputeSaberExtent(customization.TrailSettings);
        }
        return customization;
    }

    private readonly struct CustomizationMember
    {
        public readonly string Name;
        public readonly System.Action<SaberCustomization, SaberCustomization> Clone;
        public readonly System.Func<SaberCustomization, JObject, Serializer, Task> Read;
        public readonly System.Action<SaberCustomization, JObject, Serializer> Write;

        public CustomizationMember(
        string name,
        System.Action<SaberCustomization, SaberCustomization> clone,
        System.Func<SaberCustomization, JObject, Serializer, Task> read,
        System.Action<SaberCustomization, JObject, Serializer> write)
        {
            Name = name;
            Clone = clone;
            Read = read;
            Write = write;
        }
    }

    private static readonly CustomizationMember[] Members =
    [
    new(nameof(TrailSettings),
    clone: (source, dest) => dest.TrailSettings = source.TrailSettings?.Clone(),
    read: async (self, obj, serializer) =>
    {
        if (obj[nameof(TrailSettings)] is JObject trailObj)
        {
            self.TrailSettings ??= new();
            await TrailSettingsCodec.ReadFromAsync(self.TrailSettings, trailObj, serializer);
        }
    },
    write: (self, obj, serializer) =>
    {
        if (self.TrailSettings is not null)
        obj[nameof(TrailSettings)] = TrailSettingsCodec.WriteTo(self.TrailSettings, serializer);
    }),

    new(nameof(MaterialOverrides),
    clone: (source, dest) =>
    {
        foreach (var kv in source.MaterialOverrides)
        dest.MaterialOverrides[kv.Key] = (JObject)kv.Value.DeepClone();
    },
    read: (self, obj, _) =>
    {
        self.MaterialOverrides.Clear();
        self._splitPropertyManager = null;
        if (obj.TryGetObject(nameof(MaterialOverrides), out var moObj))
        {
            foreach (var prop in moObj.Properties())
            self.MaterialOverrides[prop.Name] = (JObject)prop.Value;
        }
        return Task.CompletedTask;
    },
    write: (self, obj, _) =>
    {
        if (self.MaterialOverrides.Count is > 0)
        {
            var moObj = new JObject();
            foreach (var kv in self.MaterialOverrides)
            moObj.Add(kv.Key, kv.Value);
            obj[nameof(MaterialOverrides)] = moObj;
        }
    }),

    new(nameof(ModifierState),
    clone: (source, dest) => dest.ModifierState = (JObject?)source.ModifierState?.DeepClone(),
    read: (self, obj, _) =>
    {
        if (obj.TryGetObject(nameof(ModifierState), out var modObj))
        self.ModifierState = modObj;
        return Task.CompletedTask;
    },
    write: (self, obj, _) =>
    {
        if (self.ModifierState is not null)
        obj[nameof(ModifierState)] = self.ModifierState;
    }),

    new(nameof(Transform),
    clone: (source, dest) => dest.Transform = new()
    {
        Scale = source.Transform.Scale,
        Offset = source.Transform.Offset,
        RotationDeg = source.Transform.RotationDeg
    },
    read: (self, obj, serializer) =>
    {
        if (obj.TryGetObject(nameof(Transform), out var tObj))
        self.Transform.ReadFrom(tObj, serializer);
        return Task.CompletedTask;
    },
    write: (self, obj, serializer) => obj[nameof(Transform)] = self.Transform.WriteTo(serializer)),
    ];

    public SaberCustomization Clone()
    {
        AssertMemberLegs();
        var clone = new SaberCustomization();
        foreach (var member in Members)
        member.Clone(this, clone);
        return clone;
    }

    public async Task ReadFromAsync(JObject obj, Serializer serializer)
    {
        AssertMemberLegs();
        foreach (var member in Members)
        await member.Read(this, obj, serializer);
    }

    public void WriteTo(JObject obj, Serializer serializer)
    {
        AssertMemberLegs();
        foreach (var member in Members)
        member.Write(this, obj, serializer);
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private static void AssertMemberLegs()
    {
        var seen = new HashSet<string>();
        foreach (var member in Members)
        {
            if (member.Clone is null || member.Read is null || member.Write is null)
            throw new System.InvalidOperationException(
            $"SaberCustomization member '{member.Name}' is missing a clone, read, or write leg.");

            if (!seen.Add(member.Name))
            throw new System.InvalidOperationException(
            $"SaberCustomization member '{member.Name}' is declared more than once.");
        }
    }

    public void ApplyModifierState(SaberSense.Behaviors.ComponentModifierRegistry registry, IJsonProvider jsonProvider)
    {
        if (ModifierState is not null && registry is not null)
        {
            registry.ReadFrom(ModifierState, jsonProvider);
        }
    }

    public void CaptureModifierState(SaberSense.Behaviors.ComponentModifierRegistry registry, IJsonProvider jsonProvider)
    {
        if (registry is not null)
        {
            ModifierState = (JObject)registry.WriteTo(jsonProvider);
        }
    }
}