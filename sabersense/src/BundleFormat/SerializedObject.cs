// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;

namespace SaberSense.BundleFormat;

internal sealed class SerializedObject
{
    public long PathId { get; set; }

    public int TypeId { get; set; }

    private readonly Dictionary<string, object> _fields = [];

    public object? this[string fieldName] =>
    _fields.TryGetValue(fieldName, out var value) ? value : null;

    public string GetString(string fieldName, string fallback = "") =>
    _fields.TryGetValue(fieldName, out var value) && value is string s ? s : fallback;

    public int GetInt(string fieldName, int fallback = 0) =>
    _fields.TryGetValue(fieldName, out var value) ? (int)ConvertToLong(value, fallback) : fallback;

    public long GetLong(string fieldName, long fallback = 0) =>
    _fields.TryGetValue(fieldName, out var value) ? ConvertToLong(value, fallback) : fallback;

    public bool GetBool(string fieldName, bool fallback = false) =>
    _fields.TryGetValue(fieldName, out var value) && TryConvertToLong(value, out var l)
    ? l is not 0
    : fallback;

    public float GetFloat(string fieldName, float fallback = 0f) =>
    _fields.TryGetValue(fieldName, out var value) ? ConvertToFloat(value, fallback) : fallback;

    public SerializedObject? GetChild(string fieldName) =>
    _fields.TryGetValue(fieldName, out var value) && value is SerializedObject child ? child : null;

    public byte[]? GetBytes(string fieldName) =>
    _fields.TryGetValue(fieldName, out var value) && value is byte[] bytes ? bytes : null;

    public bool TryGetString(string fieldName, out string value)
    {
        if (_fields.TryGetValue(fieldName, out var raw) && raw is string s)
        {
            value = s;
            return true;
        }
        value = string.Empty;
        return false;
    }

    public bool TryGetInt(string fieldName, out int value)
    {
        if (_fields.TryGetValue(fieldName, out var raw) && TryConvertToLong(raw, out var l))
        {
            value = (int)l;
            return true;
        }
        value = 0;
        return false;
    }

    public bool TryGetLong(string fieldName, out long value)
    {
        if (_fields.TryGetValue(fieldName, out var raw) && TryConvertToLong(raw, out var l))
        {
            value = l;
            return true;
        }
        value = 0;
        return false;
    }

    public bool TryGetFloat(string fieldName, out float value)
    {
        if (_fields.TryGetValue(fieldName, out var raw) && TryConvertToFloat(raw, out var f))
        {
            value = f;
            return true;
        }
        value = 0f;
        return false;
    }

    public bool TryGetChild(string fieldName, out SerializedObject? value)
    {
        if (_fields.TryGetValue(fieldName, out var raw) && raw is SerializedObject child)
        {
            value = child;
            return true;
        }
        value = null;
        return false;
    }

    public bool TryGetBytes(string fieldName, out byte[]? value)
    {
        if (_fields.TryGetValue(fieldName, out var raw) && raw is byte[] bytes)
        {
            value = bytes;
            return true;
        }
        value = null;
        return false;
    }

    public void SetField(string name, object value) => _fields[name] = value;

    public bool HasFields => _fields.Count is > 0;

    public IEnumerable<string> FieldNames => _fields.Keys;

    private static long ConvertToLong(object value, long fallback) =>
    TryConvertToLong(value, out var result) ? result : fallback;

    private static bool TryConvertToLong(object value, out long result)
    {
        switch (value)
        {
            case long l: result = l; return true;
            case ulong ul: result = (long)ul; return true;
            case int i: result = i; return true;
            case uint u: result = u; return true;
            case short s: result = s; return true;
            case ushort us: result = us; return true;
            case byte b: result = b; return true;
            case sbyte sb: result = sb; return true;
            case bool bo: result = bo ? 1 : 0; return true;
            case float f: result = (long)f; return true;
            case double d: result = (long)d; return true;
            default: result = 0; return false;
        }
    }

    private static float ConvertToFloat(object value, float fallback) =>
    TryConvertToFloat(value, out var result) ? result : fallback;

    private static bool TryConvertToFloat(object value, out float result)
    {
        switch (value)
        {
            case float f: result = f; return true;
            case double d: result = (float)d; return true;
            case long l: result = l; return true;
            case ulong ul: result = ul; return true;
            case int i: result = i; return true;
            case uint u: result = u; return true;
            case short s: result = s; return true;
            case ushort us: result = us; return true;
            case byte b: result = b; return true;
            case sbyte sb: result = sb; return true;
            case bool bo: result = bo ? 1f : 0f; return true;
            default: result = 0f; return false;
        }
    }
}