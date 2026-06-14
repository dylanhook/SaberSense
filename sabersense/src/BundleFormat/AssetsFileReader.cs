// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace SaberSense.BundleFormat;

internal sealed class AssetsFileReader
{
    private static readonly IModLogger Log = ModLogger.ForSource(nameof(AssetsFileReader));

    private SerializedType[] _types = [];
    private ObjectInfo[] _objects = [];
    private byte[] _data = [];
    private long _dataOffset;
    private int _formatVersion;
    private bool _bigEndian;

    public IReadOnlyList<ObjectInfo> Objects => _objects;

    public void Load(byte[] assetsData)
    {
        using var stream = new MemoryStream(assetsData);
        using var reader = new EndianReader(stream, bigEndian: true);

        int metadataSize = reader.ReadInt32();
        long fileSize = reader.ReadInt32();
        _formatVersion = reader.ReadInt32();
        _dataOffset = reader.ReadInt32();

        if (_formatVersion >= 22)
        {
            metadataSize = reader.ReadInt32();
            fileSize = reader.ReadInt64();
            _dataOffset = reader.ReadInt64();
            reader.ReadInt64();
        }

        if (_formatVersion >= 9)
        {
            var endianness = reader.ReadByte();
            reader.ReadBytes(3);
            _bigEndian = endianness != 0;
            reader.BigEndian = _bigEndian;
        }
        else
        {
            reader.BigEndian = false;
        }

        var unityVersion = reader.ReadNullTerminated();
        var targetPlatform = reader.ReadInt32();
        var typeTreeEnabled = reader.ReadBoolean();

        if (!typeTreeEnabled)
        throw new NotSupportedException("Assets file has no embedded type tree - cannot parse");

        int typeCount = reader.ReadInt32();
        if (typeCount < 0 || typeCount > 1000)
        throw new InvalidDataException($"Unreasonable type count: {typeCount}");

        var parseDiagnostics = new TypeTreeParseDiagnostics();

        _types = new SerializedType[typeCount];
        for (int i = 0; i < typeCount; i++) _types[i] = TypeTreeReader.ReadSerializedType(reader, _formatVersion, parseDiagnostics);

        int objectCount = reader.ReadInt32();
        if (objectCount < 0 || objectCount > 100000)
        throw new InvalidDataException($"Unreasonable object count: {objectCount}");

        Log.Info($"Loaded {unityVersion} (fmt {_formatVersion}) | types={typeCount}, objects={objectCount}");

        parseDiagnostics.LogSummary(Log, unityVersion, _formatVersion);

        _objects = new ObjectInfo[objectCount];

        for (int i = 0; i < objectCount; i++)
        {
            _objects[i] = ReadObjectInfo(reader, _formatVersion);
        }

        for (int i = 0; i < _objects.Length; i++)
        {
            var obj = _objects[i];
            if (obj.TypeId == -1 && obj.TypeIndex >= 0 && obj.TypeIndex < _types.Length)
            {
                obj.TypeId = _types[obj.TypeIndex].TypeId;
            }
        }

        SkipRemainingMetadata(reader);

        if (_dataOffset <= 0 || _dataOffset >= assetsData.Length)
        {
            _dataOffset = (reader.Position + 15) & ~15L;

            if (_dataOffset >= assetsData.Length)
            _dataOffset = reader.Position;
        }

        _data = assetsData;
    }

    private void SkipRemainingMetadata(EndianReader reader)
    {
        try
        {
            if (_formatVersion >= 11)
            {
                int scriptCount = reader.ReadInt32();
                for (int i = 0; i < scriptCount; i++)
                {
                    int localFileIndex = reader.ReadInt32();
                    if (_formatVersion >= 14)
                    {
                        reader.Align4();
                        reader.ReadInt64();
                    }
                    else
                    {
                        reader.ReadInt32();
                    }
                }
            }

            int externalsCount = reader.ReadInt32();
            for (int i = 0; i < externalsCount; i++)
            {
                if (_formatVersion >= 6) reader.ReadNullTerminated();
                reader.ReadBytes(16);
                reader.ReadInt32();
                reader.ReadNullTerminated();
            }

            if (_formatVersion >= 20)
            {
                int refTypesCount = reader.ReadInt32();
                for (int i = 0; i < refTypesCount; i++)
                {
                    int classId = reader.ReadInt32();
                    if (_formatVersion >= 16) reader.ReadByte();
                    if (_formatVersion >= 17) reader.ReadInt16();

                    if ((_formatVersion >= 16 && classId == 114) ||
                    (_formatVersion >= 17 && classId < 0))
                    {
                        reader.ReadBytes(16);
                    }
                    reader.ReadBytes(16);
                }
            }

            if (_formatVersion >= 5)
            {
                reader.ReadNullTerminated();
            }
        }
        catch (Exception ex)
        {
            Log.Debug(
            $"SkipRemainingMetadata stopped early ({ex.GetType().Name}: {ex.Message}) at position {reader.Position} " +
            $"(fmt {_formatVersion}); data-offset fallback will use the current reader position if needed.");
        }
    }

    public SerializedObject? ReadObject(ObjectInfo info)
    {
        if (info.TypeIndex < 0 || info.TypeIndex >= _types.Length)
        return null;

        var type = _types[info.TypeIndex];
        if (type.RootNode is null)
        return null;

        using var stream = new MemoryStream(_data);
        using var reader = new EndianReader(stream, bigEndian: _bigEndian);
        reader.Position = _dataOffset + info.ByteOffset;

        var result = new SerializedObject
        {
            PathId = info.PathId,
            TypeId = info.TypeId
        };

        TypeTreeValueReader.ReadFields(reader, type.RootNode, result);
        return result;
    }

    public SerializedType? GetType(ObjectInfo info) =>
    info.TypeIndex >= 0 && info.TypeIndex < _types.Length ? _types[info.TypeIndex] : null;

    private static ObjectInfo ReadObjectInfo(EndianReader reader, int formatVersion)
    {
        var info = new ObjectInfo();

        if (formatVersion >= 14)
        {
            reader.Align4();
            info.PathId = reader.ReadInt64();
        }
        else
        {
            info.PathId = reader.ReadInt32();
        }

        if (formatVersion >= 22)
        {
            info.ByteOffset = reader.ReadInt64();
        }
        else
        {
            info.ByteOffset = reader.ReadInt32();
        }

        info.ByteSize = reader.ReadInt32();
        info.TypeIndex = reader.ReadInt32();

        if (formatVersion < 16)
        {
            info.TypeId = reader.ReadUInt16();
            reader.ReadInt16();
        }
        else
        {
            info.TypeId = -1;
        }

        if (formatVersion >= 11 && formatVersion < 17)
        {
            reader.ReadBoolean();
        }

        return info;
    }
}