// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.IO;
using System.Text;

namespace SaberSense.BundleFormat;

internal sealed class EndianReader : IDisposable
{
    private readonly BinaryReader _reader;
    public bool BigEndian { get; set; }
    public Stream BaseStream => _reader.BaseStream;
    public long Position
    {
        get => _reader.BaseStream.Position;
        set => _reader.BaseStream.Position = value;
    }

    public long Length => _reader.BaseStream.Length;

    public EndianReader(Stream stream, bool bigEndian = false)
    {
        _reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        BigEndian = bigEndian;
    }

    public byte ReadByte() => _reader.ReadByte();
    public byte[] ReadBytes(int count) => _reader.ReadBytes(count);
    public bool ReadBoolean() => _reader.ReadBoolean();

    public short ReadInt16()
    {
        var val = _reader.ReadInt16();
        return BigEndian ? SwapInt16(val) : val;
    }

    public ushort ReadUInt16()
    {
        var val = _reader.ReadUInt16();
        return BigEndian ? SwapUInt16(val) : val;
    }

    public int ReadInt32()
    {
        var val = _reader.ReadInt32();
        return BigEndian ? SwapInt32(val) : val;
    }

    public uint ReadUInt32()
    {
        var val = _reader.ReadUInt32();
        return BigEndian ? SwapUInt32(val) : val;
    }

    public long ReadInt64()
    {
        var val = _reader.ReadInt64();
        return BigEndian ? SwapInt64(val) : val;
    }

    public float ReadFloat()
    {
        if (!BigEndian) return _reader.ReadSingle();
        var bytes = _reader.ReadBytes(4);
        Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    public string ReadNullTerminated()
    {
        var sb = new StringBuilder(64);
        byte b;
        while ((b = _reader.ReadByte()) != 0)
        sb.Append((char)b);
        return sb.ToString();
    }

    public string ReadAlignedString()
    {
        var length = ReadInt32();
        if (length <= 0)
        {
            Align4();
            return string.Empty;
        }

        long remaining = _reader.BaseStream.Length - _reader.BaseStream.Position;
        if (length > remaining)
        throw new InvalidDataException(
        $"String length {length} exceeds remaining stream ({remaining} bytes) at position {_reader.BaseStream.Position}");

        var bytes = ReadBytes(length);
        Align4();
        return Encoding.UTF8.GetString(bytes);
    }

    public void Align4()
    {
        var pos = _reader.BaseStream.Position;
        var mod = pos % 4;
        if (mod != 0) _reader.BaseStream.Position += 4 - mod;
    }

    public void Dispose() => _reader.Dispose();

    private static short SwapInt16(short val) =>
    (short)((val >> 8) & 0xFF | (val << 8) & 0xFF00);

    private static ushort SwapUInt16(ushort val) =>
    (ushort)((val >> 8) & 0xFF | (val << 8) & 0xFF00);

    private static int SwapInt32(int val)
    {
        var u = (uint)val;
        return (int)SwapUInt32(u);
    }

    private static uint SwapUInt32(uint val) =>
    (val >> 24) |
    ((val >> 8) & 0x0000FF00) |
    ((val << 8) & 0x00FF0000) |
    (val << 24);

    private static long SwapInt64(long val)
    {
        var bytes = BitConverter.GetBytes(val);
        Array.Reverse(bytes);
        return BitConverter.ToInt64(bytes, 0);
    }
}