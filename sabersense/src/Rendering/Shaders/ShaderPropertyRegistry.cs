// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaberSense.Catalog;
using SaberSense.Persistence;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace SaberSense.Rendering.Shaders;

internal sealed class ShaderIntrospector
{
    private readonly ConcurrentDictionary<int, IReadOnlyList<ShaderProperty>> _cache = [];

    public IReadOnlyList<ShaderProperty>? this[Shader shader] => Get(shader);

    public void Clear() => _cache.Clear();

    public IReadOnlyList<ShaderProperty>? Get(Shader shader)
    {
        if (shader == null) return null;

        var hash = shader.GetInstanceID();
        if (_cache.TryGetValue(hash, out var props))
        return props;

        props = Enumerate(shader);
        _cache.TryAdd(hash, props);
        return props;
    }

    public static IReadOnlyList<ShaderProperty> Enumerate(Shader shader)
    {
        if (shader == null) return [];
        var count = shader.GetPropertyCount();
        var result = new List<ShaderProperty>(count);

        for (var i = 0; i < count; i++)
        {
            var unityType = shader.GetPropertyType(i);
            var kind = unityType switch
            {
                ShaderPropertyType.Range => PropertyKind.Range,
                ShaderPropertyType.Float => PropertyKind.Float,
                ShaderPropertyType.Color => PropertyKind.Color,
                ShaderPropertyType.Vector => PropertyKind.Vector,
                ShaderPropertyType.Texture => PropertyKind.Texture,
                _ => PropertyKind.Float
            };

            float? rangeMin = null, rangeMax = null;
            if (kind == PropertyKind.Range)
            {
                var limits = shader.GetPropertyRangeLimits(i);
                rangeMin = limits.x;
                rangeMax = limits.y;
            }

            result.Add(new ShaderProperty(
            Name: shader.GetPropertyName(i),
            Description: shader.GetPropertyDescription(i),
            Id: shader.GetPropertyNameId(i),
            Kind: kind,
            UnityType: unityType,
            Attributes: shader.GetPropertyAttributes(i),
            RangeMin: rangeMin,
            RangeMax: rangeMax));
        }

        return result;
    }
}

internal enum PropertyKind { Float, Range, Color, Vector, Texture }

internal sealed record ShaderProperty(
string Name,
string Description,
int Id,
PropertyKind Kind,
ShaderPropertyType UnityType,
string[] Attributes,
float? RangeMin = null,
float? RangeMax = null)
{
    public bool HasAttribute(string attr) => Attributes?.Contains(attr) == true;

    public object? ReadValue(Material mat) => Kind switch
    {
        PropertyKind.Float or PropertyKind.Range => mat.GetFloat(Id),
        PropertyKind.Color => mat.GetColor(Id),
        PropertyKind.Vector => mat.GetVector(Id),
        PropertyKind.Texture => mat.GetTexture(Id),
        _ => throw new System.NotSupportedException($"Unhandled PropertyKind {Kind} reading '{Name}'.")
    };

    public void WriteTo(Material mat, object value)
    {
        switch (Kind)
        {
            case PropertyKind.Float or PropertyKind.Range:
            if (value is float f) mat.SetFloat(Id, f);
            break;
            case PropertyKind.Color:
            if (value is Color c) mat.SetColor(Id, c);
            break;
            case PropertyKind.Vector:
            if (value is Vector4 v4) mat.SetVector(Id, v4);
            else if (value is Vector3 v3) mat.SetVector(Id, v3);
            else if (value is Vector2 v2) mat.SetVector(Id, v2);
            break;
            case PropertyKind.Texture:
            if (value is Texture t) mat.SetTexture(Id, t);
            break;
            default:
            throw new System.NotSupportedException($"Unhandled PropertyKind {Kind} writing '{Name}'.");
        }
    }

    public static TextureDirective ClassifyTextureToken(string? texName) => texName switch
    {
        null => TextureDirective.KeepDefault,
        { Length: 0 } => TextureDirective.Clear,
        _ => TextureDirective.Resolve
    };
}

internal enum TextureDirective { KeepDefault, Clear, Resolve }

internal static class MaterialPropertyCodec
{
    public static JToken? Encode(ShaderProperty prop, Material mat, IJsonProvider jsonProvider)
    {
        var json = jsonProvider.Json;
        return prop.Kind switch
        {
            PropertyKind.Float or PropertyKind.Range => new JValue(mat.GetFloat(prop.Id)),
            PropertyKind.Color => JToken.FromObject(mat.GetColor(prop.Id), json),
            PropertyKind.Vector => JToken.FromObject(mat.GetVector(prop.Id), json),
            PropertyKind.Texture => mat.GetTexture(prop.Id) is { } tex ? new JValue(tex.name) : null,
            _ => throw new System.NotSupportedException($"Unhandled PropertyKind {prop.Kind} encoding '{prop.Name}'.")
        };
    }

    public static void Decode(ShaderProperty prop, JToken token, Material mat, IJsonProvider jsonProvider, Texture? tex = null)
    {
        var json = jsonProvider.Json;
        switch (prop.Kind)
        {
            case PropertyKind.Float or PropertyKind.Range:
            mat.SetFloat(prop.Id, token.ToObject<float>(json));
            break;
            case PropertyKind.Color:
            mat.SetColor(prop.Id, token.ToObject<Color>(json));
            break;
            case PropertyKind.Vector:
            mat.SetVector(prop.Id, token.ToObject<Vector4>(json));
            break;
            case PropertyKind.Texture:
            if (tex != null) mat.SetTexture(prop.Id, tex);
            break;
            default:
            throw new System.NotSupportedException($"Unhandled PropertyKind {prop.Kind} decoding '{prop.Name}'.");
        }
    }

    public static string? Apply(ShaderProperty prop, JToken value, Material mat, JsonSerializer json, TextureCacheRegistry? textureCache)
    {
        switch (prop.Kind)
        {
            case PropertyKind.Float or PropertyKind.Range:
            mat.SetFloat(prop.Id, value.ToObject<float>(json));
            break;
            case PropertyKind.Color:
            mat.SetColor(prop.Id, value.ToObject<Color>(json));
            break;
            case PropertyKind.Vector:
            mat.SetVector(prop.Id, value.ToObject<Vector4>(json));
            break;
            case PropertyKind.Texture:
            var texName = value.ToObject<string>();

            if (ShaderProperty.ClassifyTextureToken(texName) != TextureDirective.Resolve)
            {
                mat.SetTexture(prop.Id, null);
                break;
            }

            var resolved = textureCache?.FindByName(texName!);
            if (resolved != null)
            {
                mat.SetTexture(prop.Id, resolved);
            }
            else if (texName!.Contains('\\') || texName.Contains('/'))
            {
                return texName;
            }
            break;
            default:
            throw new System.NotSupportedException($"Unhandled PropertyKind {prop.Kind} applying '{prop.Name}'.");
        }

        return null;
    }
}