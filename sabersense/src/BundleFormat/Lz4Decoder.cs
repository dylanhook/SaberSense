// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;

namespace SaberSense.BundleFormat;

internal static class Lz4Decoder
{
    private const int MaxSequenceLength = 512 * 1024 * 1024;

    public static byte[] Decode(byte[] compressed, int uncompressedSize)
    {
        var output = new byte[uncompressedSize];
        Decode(compressed, 0, compressed.Length, output, 0, uncompressedSize);
        return output;
    }

    public static byte[] DecodePartial(byte[] compressed, int uncompressedSize, int maxOutputBytes)
    {
        int outputSize = Math.Min(uncompressedSize, maxOutputBytes);
        var output = new byte[outputSize];
        Decode(compressed, 0, compressed.Length, output, 0, outputSize);
        return output;
    }

    public static void Decode(
    byte[] source, int srcOffset, int srcLength,
    byte[] dest, int destOffset, int destLength)
    {
        int srcEnd = srcOffset + srcLength;
        int destEnd = destOffset + destLength;
        int si = srcOffset;
        int di = destOffset;

        while (si < srcEnd && di < destEnd)
        {
            byte token = source[si++];
            int literalLength = token >> 4;
            int matchLength = token & 0x0F;

            if (literalLength == 15)
            {
                byte extra;
                do
                {
                    if (si >= srcEnd)
                    throw new System.IO.InvalidDataException("LZ4: truncated literal length");
                    extra = source[si++];
                    literalLength += extra;

                    if (literalLength > srcLength)
                    throw new System.IO.InvalidDataException("LZ4: literal length exceeds source size");
                } while (extra == 255);
            }

            if (si + literalLength > srcEnd)
            throw new System.IO.InvalidDataException("LZ4: truncated literal data");
            int safeLiteralLen = Math.Min(literalLength, destEnd - di);
            Buffer.BlockCopy(source, si, dest, di, safeLiteralLen);
            si += literalLength;
            di += safeLiteralLen;

            if (di >= destEnd) break;

            if (si + 1 >= srcEnd)
            throw new System.IO.InvalidDataException("LZ4: truncated match offset");
            int matchOffset = source[si] | (source[si + 1] << 8);
            si += 2;

            matchLength += 4;
            if ((token & 0x0F) == 15)
            {
                byte extra;
                do
                {
                    if (si >= srcEnd)
                    throw new System.IO.InvalidDataException("LZ4: truncated match length");
                    extra = source[si++];
                    matchLength += extra;

                    if (matchLength > MaxSequenceLength)
                    throw new System.IO.InvalidDataException("LZ4: match length exceeds sanity cap");
                } while (extra == 255);
            }

            int matchStart = di - matchOffset;
            if (matchOffset == 0 || matchStart < destOffset)
            throw new System.IO.InvalidDataException(
            $"LZ4: invalid match offset {matchOffset} at output position {di - destOffset}");

            for (int i = 0; i < matchLength && di < destEnd; i++, di++)
            {
                dest[di] = dest[matchStart + i];
            }
        }
    }
}