// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.IO;

namespace SaberSense.BundleFormat;

internal static class TypeTreeValueReader
{
    internal static void ReadFields(EndianReader reader, TypeTreeNode rootNode, SerializedObject target)
    {
        foreach (var child in rootNode.Children)
        {
            try
            {
                var value = ReadFieldValue(reader, child);
                if (value is not null) target.SetField(child.FieldName, value);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Stream desync at {child.FieldName} ({child.TypeName}): {ex.Message}", ex);
            }
        }
    }

    private static object? ReadFieldValue(EndianReader reader, TypeTreeNode node)
    {
        object? value;

        if (node.Children.Count is 0)
        {
            value = ReadPrimitive(reader, node);
        }
        else if (node.TypeName == "string")
        {
            return reader.ReadAlignedString();
        }
        else if (node.Children.Count is 1 && node.Children[0].FieldName == "Array")
        {
            value = ReadFieldValue(reader, node.Children[0]);
        }
        else if (node.IsArray)
        {
            value = ReadArray(reader, node);
        }
        else
        {
            var obj = new SerializedObject { TypeId = 0, PathId = 0 };
            ReadFields(reader, node, obj);
            value = obj;
        }

        if (node.IsAligned) reader.Align4();

        return value;
    }

    private static object? ReadPrimitive(EndianReader reader, TypeTreeNode node)
    {
        return node.TypeName switch
        {
            "bool" => reader.ReadBoolean(),
            "SInt8" or "char" => (int)reader.ReadByte(),
            "UInt8" => (int)reader.ReadByte(),
            "SInt16" or "short" => (int)reader.ReadInt16(),
            "UInt16" or "unsigned short" => (int)reader.ReadUInt16(),
            "SInt32" or "int" => reader.ReadInt32(),
            "UInt32" or "unsigned int" => (long)reader.ReadUInt32(),
            "SInt64" or "long long" => reader.ReadInt64(),
            "UInt64" or "unsigned long long" => reader.ReadInt64(),
            "float" => reader.ReadFloat(),
            "double" => BitConverter.Int64BitsToDouble(reader.ReadInt64()),
            _ => ReadBySize(reader, node.ByteSize)
        };
    }

    private static object? ReadBySize(EndianReader reader, int byteSize)
    {
        if (byteSize <= 0) return null;
        return reader.ReadBytes(byteSize);
    }

    private static object? ReadArray(EndianReader reader, TypeTreeNode node)
    {
        TypeTreeNode? elementNode = null;
        if (node.Children.Count is >= 2)
        {
            elementNode = node.Children[1];
        }
        else if (node.Children.Count is 1 && node.Children[0].TypeName != "Array")
        {
            elementNode = node.Children[0];
        }

        if (elementNode is null) return null;

        int count = reader.ReadInt32();

        if (count is < 0)
        throw new InvalidDataException($"Negative array count {count} at position {reader.Position - 4}");

        if (elementNode.TypeName is "UInt8" or "SInt8" or "char" or "byte")
        {
            long remaining = reader.Length - reader.Position;
            if (count > remaining)
            throw new InvalidDataException(
            $"Byte array count {count} exceeds remaining stream ({remaining} bytes)");
            return reader.ReadBytes(count);
        }

        if (count > 10_000_000)
        throw new InvalidDataException($"Unreasonable array count {count} at position {reader.Position - 4}");

        var list = new List<object>(Math.Min(count, 1024));
        for (int i = 0; i < count; i++)
        {
            list.Add(ReadFieldValue(reader, elementNode)!);
        }
        return list;
    }
}