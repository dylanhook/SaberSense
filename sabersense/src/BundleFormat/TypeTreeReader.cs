// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SaberSense.BundleFormat;

internal static class TypeTreeReader
{
    internal static SerializedType ReadSerializedType(EndianReader reader, int formatVersion, TypeTreeParseDiagnostics? diagnostics = null)
    {
        long startPos = reader.Position;
        var type = new SerializedType
        {
            TypeId = reader.ReadInt32()
        };

        if (formatVersion >= 16)
        {
            reader.ReadByte();
        }

        if (formatVersion >= 17)
        {
            reader.ReadInt16();
        }

        if (formatVersion >= 13)
        {
            if ((formatVersion < 16 && type.TypeId < 0) || (formatVersion >= 16 && type.TypeId == 114) || (formatVersion >= 17 && type.TypeId < 0))
            {
                reader.ReadBytes(16);
            }
            reader.ReadBytes(16);
        }

        int nodeCount = reader.ReadInt32();
        int stringTableSize = reader.ReadInt32();

        if (nodeCount < 0 || nodeCount > 10000)
        throw new InvalidDataException(
        $"Unreasonable nodeCount={nodeCount} for typeId={type.TypeId} at pos={startPos}");
        if (stringTableSize < 0 || stringTableSize > 1_000_000)
        throw new InvalidDataException(
        $"Unreasonable stringTableSize={stringTableSize} for typeId={type.TypeId} at pos={startPos}");

        var nodes = new TypeTreeNodeRaw[nodeCount];
        for (int i = 0; i < nodeCount; i++)
        {
            nodes[i] = new TypeTreeNodeRaw
            {
                Version = reader.ReadUInt16(),
                Depth = reader.ReadByte(),
                TypeFlags = reader.ReadByte(),
                TypeStringOffset = reader.ReadInt32(),
                NameStringOffset = reader.ReadInt32(),
                ByteSize = reader.ReadInt32(),
                Index = reader.ReadInt32(),
                MetaFlags = reader.ReadInt32()
            };

            if (formatVersion >= 18)
            {
                reader.ReadBytes(8);
            }
        }

        var stringTableBytes = reader.ReadBytes(stringTableSize);
        var localStrings = ParseStringTable(stringTableBytes);

        if (formatVersion >= 21)
        {
            int depCount = reader.ReadInt32();
            if (depCount > 0)
            reader.ReadBytes(depCount * 4);
        }

        type.RootNode = BuildTypeTree(nodes, localStrings, diagnostics);

        return type;
    }

    private static TypeTreeNode? BuildTypeTree(TypeTreeNodeRaw[] rawNodes, Dictionary<int, string> localStrings, TypeTreeParseDiagnostics? diagnostics)
    {
        if (rawNodes.Length is 0) return null;

        var allNodes = new TypeTreeNode[rawNodes.Length];
        for (int i = 0; i < rawNodes.Length; i++)
        {
            var raw = rawNodes[i];
            allNodes[i] = new TypeTreeNode
            {
                TypeName = ResolveString(raw.TypeStringOffset, localStrings, diagnostics),
                FieldName = ResolveString(raw.NameStringOffset, localStrings, diagnostics),
                ByteSize = raw.ByteSize,
                Depth = raw.Depth,
                IsAligned = (raw.MetaFlags & 0x4000) != 0,
                IsArray = (raw.TypeFlags & 0x01) != 0,
                Index = raw.Index
            };
        }

        var stack = new Stack<TypeTreeNode>();
        var root = allNodes[0];
        stack.Push(root);

        for (int i = 1; i < allNodes.Length; i++)
        {
            var node = allNodes[i];
            while (stack.Count is > 0 && stack.Peek().Depth >= node.Depth)
            stack.Pop();

            if (stack.Count is > 0)
            stack.Peek().Children.Add(node);

            stack.Push(node);
        }

        return root;
    }

    private static string ResolveString(int offset, Dictionary<int, string> localStrings, TypeTreeParseDiagnostics? diagnostics)
    {
        if ((offset & 0x80000000) != 0)
        {
            var commonOffset = offset & 0x7FFFFFFF;
            var common = UnityCommonStrings.Get(commonOffset);
            if (common is not null) return common;

            diagnostics?.RecordUnresolvedCommon(commonOffset);
            return $"unknown_{commonOffset}";
        }
        if (localStrings.TryGetValue(offset, out var local)) return local;

        diagnostics?.RecordUnresolvedLocal(offset);
        return $"local_{offset}";
    }

    private static Dictionary<int, string> ParseStringTable(byte[] data)
    {
        var result = new Dictionary<int, string>();
        int i = 0;
        while (i < data.Length)
        {
            int start = i;
            while (i < data.Length && data[i] != 0) i++;
            if (i > start)
            result[start] = Encoding.UTF8.GetString(data, start, i - start);
            i++;
        }
        return result;
    }

    private struct TypeTreeNodeRaw
    {
        public ushort Version;
        public byte Depth;
        public byte TypeFlags;
        public int TypeStringOffset;
        public int NameStringOffset;
        public int ByteSize;
        public int Index;
        public int MetaFlags;
    }
}

internal sealed class TypeTreeParseDiagnostics
{
    private int _unresolvedCommonCount;
    private int _firstUnresolvedCommonOffset = -1;
    private int _unresolvedLocalCount;
    private int _firstUnresolvedLocalOffset = -1;

    public void RecordUnresolvedCommon(int offset)
    {
        if (_unresolvedCommonCount is 0) _firstUnresolvedCommonOffset = offset;
        _unresolvedCommonCount++;
    }

    public void RecordUnresolvedLocal(int offset)
    {
        if (_unresolvedLocalCount is 0) _firstUnresolvedLocalOffset = offset;
        _unresolvedLocalCount++;
    }

    public void LogSummary(Core.Logging.IModLogger log, string unityVersion, int formatVersion)
    {
        if (_unresolvedCommonCount is 0 && _unresolvedLocalCount is 0) return;

        if (_unresolvedCommonCount is > 0)
        {
            log.Warn(
            $"Type-tree decode hit {_unresolvedCommonCount} unresolved common-string offset(s) " +
            $"(first={_firstUnresolvedCommonOffset}) for {unityVersion} (fmt {formatVersion}). " +
            "The pinned Unity 2019.4-era common-string table may not match this bundle's engine version; " +
            "affected type/field names fall back to synthetic 'unknown_' and may degrade numeric fields to byte arrays.");
        }

        if (_unresolvedLocalCount is > 0)
        {
            log.Debug(
            $"Type-tree decode hit {_unresolvedLocalCount} unresolved local-string offset(s) " +
            $"(first={_firstUnresolvedLocalOffset}) for {unityVersion} (fmt {formatVersion}); " +
            "affected names fall back to synthetic 'local_'.");
        }
    }
}