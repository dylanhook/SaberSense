// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SaberSense.Persistence;

internal sealed class UnityValueTypeConverter : JsonConverter
{
    private static readonly HashSet<Type> SupportedTypes = new()
    {
        typeof(Color), typeof(Vector2), typeof(Vector3), typeof(Vector4)
    };

    public override bool CanConvert(Type objectType) => SupportedTypes.Contains(objectType);

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        var obj = new JObject();
        switch (value)
        {
            case Color c:
            obj["r"] = c.r; obj["g"] = c.g; obj["b"] = c.b; obj["a"] = c.a;
            break;
            case Vector2 v:
            obj["x"] = v.x; obj["y"] = v.y;
            break;
            case Vector3 v:
            obj["x"] = v.x; obj["y"] = v.y; obj["z"] = v.z;
            break;
            case Vector4 v:
            obj["x"] = v.x; obj["y"] = v.y; obj["z"] = v.z; obj["w"] = v.w;
            break;
        }
        obj.WriteTo(writer);
    }

    public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var obj = JObject.Load(reader);

        if (objectType == typeof(Color))
        return new Color(Val(obj, "r"), Val(obj, "g"), Val(obj, "b"), Val(obj, "a", 1f));

        if (objectType == typeof(Vector2))
        return new Vector2(Val(obj, "x"), Val(obj, "y"));

        if (objectType == typeof(Vector3))
        return new Vector3(Val(obj, "x"), Val(obj, "y"), Val(obj, "z"));

        if (objectType == typeof(Vector4))
        return new Vector4(Val(obj, "x"), Val(obj, "y"), Val(obj, "z"), Val(obj, "w"));

        return existingValue;
    }

    private static float Val(JObject obj, string key, float fallback = 0f)
    => obj.Value<float?>(key) ?? fallback;
}