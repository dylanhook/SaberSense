// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaberSense.Catalog;
using SaberSense.Catalog.Model;
using SaberSense.Core.Utilities;
using SaberSense.Rendering.Shaders;
using System.Threading.Tasks;
using UnityEngine;

namespace SaberSense.Persistence;

internal static class JsonExtensions
{
    public static void Populate<T>(this JToken token, T target, JsonSerializer serializer)
    {
        using var reader = token.CreateReader();
        serializer.Populate(reader, target!);
    }
}

public class Serializer : IJsonProvider
{
    public JsonSerializer Json { get; } = CreateSerializer();

    private readonly SaberCatalog _catalog;
    private readonly ShaderIntrospector _shaders;
    private readonly TextureCacheRegistry _textures;

    internal Serializer(SaberCatalog catalog, ShaderIntrospector shaders, TextureCacheRegistry textures)
    {
        _catalog = catalog;
        _shaders = shaders;
        _textures = textures;
    }

    public async Task<SaberAssetEntry?> ResolveSaberEntryAsync(string relativePath)
    {
        await _catalog.WaitForFinishAsync();
        return await _catalog[relativePath];
    }

    public JToken SerializeMaterial(Material mat, bool includeClears = false)
    {
        var result = new JObject();
        foreach (var prop in _shaders[mat.shader]!)
        {
            var encoded = MaterialPropertyCodec.Encode(prop, mat, this);
            if (encoded is not null)
            result.Add(prop.Name, encoded);
            else if (includeClears && prop.Kind == PropertyKind.Texture)
            result.Add(prop.Name, new JValue(""));
        }
        return result;
    }

    public async Task LoadMaterialAsync(JObject data, Material mat)
    {
        foreach (var prop in _shaders[mat.shader]!)
        {
            var entry = data.Property(prop.Name);
            if (entry is null) continue;

            if (prop.Kind == PropertyKind.Texture)
            {
                var texName = entry.Value.ToObject<string>();

                switch (ShaderProperty.ClassifyTextureToken(texName))
                {
                    case TextureDirective.KeepDefault:
                    continue;

                    case TextureDirective.Clear:
                    mat.SetTexture(prop.Id, null);
                    continue;

                    default:
                    var tex = (await _textures.ResolveAnyAsync(texName!))?.Texture;
                    MaterialPropertyCodec.Decode(prop, entry.Value, mat, this, tex!);
                    continue;
                }
            }

            MaterialPropertyCodec.Decode(prop, entry.Value, mat, this);
        }
    }

    private static JsonSerializer CreateSerializer()
    {
        var s = new JsonSerializer { ObjectCreationHandling = ObjectCreationHandling.Replace };
        s.Converters.Add(new UnityValueTypeConverter());
        return s;
    }
}