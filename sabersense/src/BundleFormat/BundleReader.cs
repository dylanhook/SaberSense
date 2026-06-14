// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System;
using System.Collections.Generic;
using System.IO;

namespace SaberSense.BundleFormat;

internal static class BundleReader
{
    private static readonly IModLogger Log = ModLogger.ForSource(nameof(BundleReader));

    private const int CompressionNone = 0;
    private const int CompressionLzma = 1;
    private const int CompressionLz4 = 2;
    private const int CompressionLz4Hc = 3;

    private const int MaxCompressedBlockInfoBytes = 4 * 1024 * 1024;
    private const int MaxUncompressedBlockInfoBytes = 16 * 1024 * 1024;
    private const int MaxBlockCount = 100_000;
    private const int MaxDirectoryCount = 100_000;
    private const long MaxTotalUncompressedBytes = 512L * 1024 * 1024;

    public static Dictionary<string, byte[]> ExtractBundleContent(string bundlePath)
    => ExtractBundleContent(bundlePath, fileFilter: null);

    public static Dictionary<string, byte[]> ExtractBundleContent(string bundlePath, Func<string, bool>? fileFilter)
    {
        using var stream = File.OpenRead(bundlePath);
        using var reader = new EndianReader(stream, bigEndian: true);

        var signature = reader.ReadNullTerminated();
        if (signature != "UnityFS")
        throw new InvalidDataException($"Expected 'UnityFS' signature, got '{signature}'");

        var formatVersion = reader.ReadInt32();
        var unityVersion = reader.ReadNullTerminated();
        var generatorVersion = reader.ReadNullTerminated();

        var bundleSize = reader.ReadInt64();
        var compressedBlockInfoSize = reader.ReadInt32();
        var uncompressedBlockInfoSize = reader.ReadInt32();
        var flags = reader.ReadInt32();

        int compressionType = flags & 0x3F;
        bool blockInfoAtEnd = (flags & 0x80) != 0;

        Log.Debug($"'{Path.GetFileName(bundlePath)}' fmt={formatVersion} " +
        $"gen={generatorVersion} size={bundleSize} cbis={compressedBlockInfoSize} " +
        $"ubis={uncompressedBlockInfoSize} flags=0x{flags:X} headerPos={reader.Position}");

        long headerEnd = reader.Position;
        if (formatVersion >= 7)
        {
            headerEnd = (headerEnd + 15) & ~15L;
            reader.Position = headerEnd;
        }

        if (compressedBlockInfoSize <= 0 || compressedBlockInfoSize > Math.Min(reader.Length, MaxCompressedBlockInfoBytes))
        throw new InvalidDataException($"Unreasonable compressed block info size: {compressedBlockInfoSize}");
        if (uncompressedBlockInfoSize <= 0 || uncompressedBlockInfoSize > MaxUncompressedBlockInfoBytes)
        throw new InvalidDataException($"Unreasonable uncompressed block info size: {uncompressedBlockInfoSize}");

        long dataBlocksStart;
        byte[] blockInfoData;

        if (blockInfoAtEnd)
        {
            dataBlocksStart = headerEnd;
            long savedPos = reader.Position;

            long blockInfoStart = reader.Length - compressedBlockInfoSize;
            if (blockInfoStart < headerEnd)
            throw new InvalidDataException(
            $"Block info ({compressedBlockInfoSize} bytes at end) overlaps the bundle header");

            reader.Position = blockInfoStart;
            var compressedBlockInfo = reader.ReadBytes(compressedBlockInfoSize);

            blockInfoData = DecompressData(compressedBlockInfo, compressionType,
            uncompressedBlockInfoSize, "block info");

            reader.Position = savedPos;
        }
        else
        {
            var compressedBlockInfo = reader.ReadBytes(compressedBlockInfoSize);

            blockInfoData = DecompressData(compressedBlockInfo, compressionType,
            uncompressedBlockInfoSize, "block info");

            dataBlocksStart = reader.Position;
        }

        using var blockInfoStream = new MemoryStream(blockInfoData);
        using var blockReader = new EndianReader(blockInfoStream, bigEndian: true);

        blockReader.ReadBytes(16);
        int blockCount = blockReader.ReadInt32();
        if (blockCount < 0 || blockCount > MaxBlockCount)
        throw new InvalidDataException($"Unreasonable block count: {blockCount}");

        long dataRegionBytes = reader.Length - dataBlocksStart;
        long totalUncompressed = 0;

        var blocks = new BlockInfo[blockCount];
        for (int i = 0; i < blockCount; i++)
        {
            blocks[i] = new BlockInfo
            {
                UncompressedSize = blockReader.ReadInt32(),
                CompressedSize = blockReader.ReadInt32(),
                Flags = blockReader.ReadUInt16()
            };

            if (blocks[i].CompressedSize < 0 || blocks[i].CompressedSize > dataRegionBytes)
            throw new InvalidDataException(
            $"Block {i} compressed size {blocks[i].CompressedSize} exceeds data region ({dataRegionBytes} bytes)");
            if (blocks[i].UncompressedSize < 0)
            throw new InvalidDataException($"Block {i} has negative uncompressed size {blocks[i].UncompressedSize}");

            totalUncompressed += blocks[i].UncompressedSize;
            if (totalUncompressed > MaxTotalUncompressedBytes)
            throw new InvalidDataException(
            $"Total uncompressed size exceeds the {MaxTotalUncompressedBytes / (1024 * 1024)} MB budget");
        }

        int directoryCount = blockReader.ReadInt32();
        if (directoryCount < 0 || directoryCount > MaxDirectoryCount)
        throw new InvalidDataException($"Unreasonable directory count: {directoryCount}");

        var directories = new DirectoryEntry[directoryCount];
        for (int i = 0; i < directoryCount; i++)
        {
            directories[i] = new DirectoryEntry
            {
                Offset = blockReader.ReadInt64(),
                Size = blockReader.ReadInt64(),
                Flags = blockReader.ReadInt32(),
                Name = blockReader.ReadNullTerminated()
            };

            var entry = directories[i];
            if (entry.Offset < 0 || entry.Size < 0 ||
            entry.Size > totalUncompressed || entry.Offset > totalUncompressed - entry.Size)
            throw new InvalidDataException(
            $"Directory entry '{entry.Name}' out of bounds (offset={entry.Offset}, size={entry.Size}, total={totalUncompressed})");
        }

        Log.Debug($"blocks={blockCount} dirs={directoryCount} dataStart={dataBlocksStart}");

        var wantedEntries = new List<DirectoryEntry>();
        foreach (var entry in directories)
        {
            if (fileFilter is null || fileFilter(entry.Name))
            wantedEntries.Add(entry);
        }

        var neededBlocks = new bool[blockCount];
        var blockBytesNeeded = new int[blockCount];
        bool extractAll = fileFilter is null;

        if (extractAll)
        {
            for (int i = 0; i < blockCount; i++)
            {
                neededBlocks[i] = true;
                blockBytesNeeded[i] = blocks[i].UncompressedSize;
            }
        }
        else
        {
            long blockStart = 0;
            for (int i = 0; i < blockCount; i++)
            {
                long blockEnd = blockStart + blocks[i].UncompressedSize;
                foreach (var entry in wantedEntries)
                {
                    long fileStart = entry.Offset;
                    long fileEnd = entry.Offset + entry.Size;
                    if (fileStart < blockEnd && fileEnd > blockStart)
                    {
                        neededBlocks[i] = true;
                        int needed = (int)(Math.Min(fileEnd, blockEnd) - blockStart);
                        if (needed > blockBytesNeeded[i])
                        blockBytesNeeded[i] = needed;
                    }
                }
                blockStart = blockEnd;
            }
        }

        reader.Position = dataBlocksStart;
        var decompressedBlocks = new byte[blockCount][];
        var blockOffsets = new long[blockCount];

        long cumOffset = 0;
        for (int i = 0; i < blockCount; i++)
        {
            blockOffsets[i] = cumOffset;

            if (neededBlocks[i])
            {
                var compressedData = reader.ReadBytes(blocks[i].CompressedSize);
                int blockCompression = blocks[i].Flags & 0x3F;
                int fullSize = blocks[i].UncompressedSize;
                int needed = blockBytesNeeded[i];

                if (needed < fullSize)
                decompressedBlocks[i] = DecompressPartial(compressedData, blockCompression,
                fullSize, needed, $"block {i}");
                else
                decompressedBlocks[i] = DecompressData(compressedData, blockCompression,
                fullSize, $"block {i}");
            }
            else
            {
                reader.Position += blocks[i].CompressedSize;
                decompressedBlocks[i] = [];
            }

            cumOffset += blocks[i].UncompressedSize;
        }

        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in wantedEntries)
        {
            var fileData = new byte[entry.Size];
            long filePos = entry.Offset;
            int destPos = 0;
            int remaining = (int)entry.Size;

            for (int i = 0; i < blockCount && remaining > 0; i++)
            {
                long blockEnd = blockOffsets[i] + blocks[i].UncompressedSize;
                if (filePos >= blockEnd) continue;

                int srcOffset = (int)(filePos - blockOffsets[i]);
                int available = decompressedBlocks[i].Length - srcOffset;
                int copyLen = Math.Min(remaining, available);

                if (copyLen > 0)
                Buffer.BlockCopy(decompressedBlocks[i], srcOffset, fileData, destPos, copyLen);

                destPos += copyLen;
                filePos += copyLen;
                remaining -= copyLen;
            }

            result[entry.Name] = fileData;
        }

        return result;
    }

    private static byte[] DecompressData(byte[] compressed, int compressionType,
    int uncompressedSize, string context)
    {
        return compressionType switch
        {
            CompressionNone => compressed,
            CompressionLzma => LzmaDecoder.Decode(compressed, uncompressedSize),
            CompressionLz4 or CompressionLz4Hc => Lz4Decoder.Decode(compressed, uncompressedSize),
            _ => throw new NotSupportedException(
            $"Unsupported compression type {compressionType} in {context}")
        };
    }

    private static byte[] DecompressPartial(byte[] compressed, int compressionType,
    int uncompressedSize, int maxOutputBytes, string context)
    {
        return compressionType switch
        {
            CompressionNone => compressed[..Math.Min(compressed.Length, maxOutputBytes)],
            CompressionLzma => LzmaDecoder.DecodePartial(compressed, uncompressedSize, maxOutputBytes),
            CompressionLz4 or CompressionLz4Hc => Lz4Decoder.DecodePartial(compressed, uncompressedSize, maxOutputBytes),
            _ => throw new NotSupportedException(
            $"Unsupported compression type {compressionType} in {context}")
        };
    }

    private struct BlockInfo
    {
        public int UncompressedSize;
        public int CompressedSize;
        public ushort Flags;
    }

    private struct DirectoryEntry
    {
        public long Offset;
        public long Size;
        public int Flags;
        public string Name;
    }
}