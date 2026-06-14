// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System;
using System.IO;

namespace SaberSense.BundleFormat;

internal static class LzmaDecoder
{
    private static readonly IModLogger Log = ModLogger.ForSource(nameof(LzmaDecoder));

    public static byte[] DecodePartial(byte[] compressed, int uncompressedSize, int maxOutputBytes)
    => Decode(compressed, Math.Min(uncompressedSize, maxOutputBytes));

    public static byte[] Decode(byte[] compressed, int uncompressedSize)
    {
        if (compressed.Length is < 5)
        throw new InvalidDataException("LZMA data too short for properties header");

        int headerOffset = FindLzmaHeaderOffset(compressed);

        byte propsByte = compressed[headerOffset];
        int lc = propsByte % 9;
        int remainder = propsByte / 9;
        int lp = remainder % 5;
        int pb = remainder / 5;

        if (pb > 4)
        throw new InvalidDataException($"Invalid LZMA properties byte: {propsByte}");

        int dictionarySize = compressed[headerOffset + 1] | (compressed[headerOffset + 2] << 8) |
        (compressed[headerOffset + 3] << 16) | (compressed[headerOffset + 4] << 24);
        if (dictionarySize < 0) dictionarySize = int.MaxValue;

        if (dictionarySize is <= 0 || dictionarySize > uncompressedSize)
        {
            Log.Debug(
            $"LZMA props at offset {headerOffset} declare an unusual dictionary size " +
            $"({dictionarySize}) vs output size {uncompressedSize}; header offset may be mis-detected.");
        }

        int dataStart = headerOffset + 5;

        var output = new byte[uncompressedSize];
        var decoder = new LzmaRangeDecoder(compressed, dataStart, compressed.Length - dataStart);
        var state = new LzmaState(lc, lp, pb, dictionarySize, output, uncompressedSize);

        state.Decode(decoder);

        if (state.ProducedBytes != uncompressedSize)
        {
            Log.Debug(
            $"LZMA decode produced {state.ProducedBytes} of {uncompressedSize} expected byte(s) " +
            $"(header offset {headerOffset}); the remainder is zero-filled. This is expected for a " +
            "truncated partial read but otherwise indicates an early end marker or a mis-detected block.");
        }

        return output;
    }

    private static int FindLzmaHeaderOffset(byte[] data)
    {
        byte props0 = data[0];
        int pb0 = (props0 / 9) / 5;
        if (pb0 <= 4 && props0 != 0)
        return 0;

        for (int offset = 1; offset < Math.Min(16, data.Length - 5); offset++)
        {
            byte candidate = data[offset];
            if (candidate == 0) continue;

            int candidatePb = (candidate / 9) / 5;
            if (candidatePb > 4) continue;

            int dictSize = data[offset + 1] | (data[offset + 2] << 8) |
            (data[offset + 3] << 16) | (data[offset + 4] << 24);
            if (dictSize > 0)
            return offset;
        }

        return 0;
    }

    private sealed class LzmaRangeDecoder
    {
        private readonly byte[] _data;
        private int _pos;
        private readonly int _end;
        private uint _range;
        private uint _code;

        public LzmaRangeDecoder(byte[] data, int offset, int length)
        {
            _data = data;
            _pos = offset;
            _end = offset + length;
            _range = 0xFFFFFFFF;
            _code = 0;

            for (int i = 0; i < 5; i++)
            _code = (_code << 8) | ReadByte();
        }

        private byte ReadByte()
        {
            return _pos < _end ? _data[_pos++] : (byte)0;
        }

        public void Normalize()
        {
            if (_range < 0x01000000)
            {
                _range <<= 8;
                _code = (_code << 8) | ReadByte();
            }
        }

        public uint DecodeBit(ushort[] probs, int index)
        {
            Normalize();
            uint prob = probs[index];
            uint bound = (_range >> 11) * prob;

            if (_code < bound)
            {
                _range = bound;
                probs[index] = (ushort)(prob + ((2048 - prob) >> 5));
                return 0;
            }

            _range -= bound;
            _code -= bound;
            probs[index] = (ushort)(prob - (prob >> 5));
            return 1;
        }

        public uint DecodeDirectBits(int numBits)
        {
            uint result = 0;
            for (int i = numBits; i > 0; i--)
            {
                Normalize();
                _range >>= 1;
                uint t = (_code - _range) >> 31;
                _code -= _range & (t - 1);
                result = (result << 1) | (1 - t);
            }
            return result;
        }
    }

    private sealed class LzmaState
    {
        private const int NumStates = 12;
        private const int NumPosSlots = 64;
        private const int NumLenToPosStates = 4;
        private const int NumAlignBits = 4;
        private const int NumPosBitsMax = 4;
        private const int EndPosModelIndex = 14;
        private const int StartPosModelIndex = 4;

        private readonly int _lc, _lp, _pb;
        private readonly byte[] _output;
        private readonly int _outputSize;
        private int _outPos;

        public int ProducedBytes => _outPos;

        private readonly ushort[] _isMatch;
        private readonly ushort[] _isRep;
        private readonly ushort[] _isRepG0;
        private readonly ushort[] _isRepG1;
        private readonly ushort[] _isRepG2;
        private readonly ushort[] _isRep0Long;
        private readonly ushort[][] _litProbs;
        private readonly ushort[][] _posSlotProbs;
        private readonly ushort[] _posProbs;
        private readonly ushort[] _alignProbs;
        private readonly LenDecoder _lenDecoder;
        private readonly LenDecoder _repLenDecoder;

        public LzmaState(int lc, int lp, int pb, int dictSize, byte[] output, int outputSize)
        {
            _lc = lc;
            _lp = lp;
            _pb = pb;
            _output = output;
            _outputSize = outputSize;

            _isMatch = CreateProbs(NumStates << NumPosBitsMax);
            _isRep = CreateProbs(NumStates);
            _isRepG0 = CreateProbs(NumStates);
            _isRepG1 = CreateProbs(NumStates);
            _isRepG2 = CreateProbs(NumStates);
            _isRep0Long = CreateProbs(NumStates << NumPosBitsMax);

            _litProbs = new ushort[1 << (lc + lp)][];
            for (int i = 0; i < _litProbs.Length; i++)
            _litProbs[i] = CreateProbs(0x300);

            _posSlotProbs = new ushort[NumLenToPosStates][];
            for (int i = 0; i < NumLenToPosStates; i++)
            _posSlotProbs[i] = CreateProbs(1 << 6);

            _posProbs = CreateProbs(115);
            _alignProbs = CreateProbs(1 << NumAlignBits);

            _lenDecoder = new LenDecoder(pb);
            _repLenDecoder = new LenDecoder(pb);
        }

        private static ushort[] CreateProbs(int count)
        {
            var probs = new ushort[count];
            for (int i = 0; i < count; i++)
            probs[i] = 1024;
            return probs;
        }

        public void Decode(LzmaRangeDecoder rc)
        {
            int state = 0;
            int rep0 = 0, rep1 = 0, rep2 = 0, rep3 = 0;
            int pbMask = (1 << _pb) - 1;

            while (_outPos < _outputSize)
            {
                int posState = _outPos & pbMask;

                if (rc.DecodeBit(_isMatch, (state << NumPosBitsMax) + posState) == 0)
                {
                    int litState = GetLitState();
                    byte symbol;

                    if (state >= 7)
                    {
                        byte matchByte = GetDictByte(rep0);
                        symbol = DecodeLitMatched(rc, _litProbs[litState], matchByte);
                    }
                    else
                    {
                        symbol = DecodeLit(rc, _litProbs[litState]);
                    }

                    PutByte(symbol);
                    state = state < 4 ? 0 : (state < 10 ? state - 3 : state - 6);
                }
                else
                {
                    int len;

                    if (rc.DecodeBit(_isRep, state) == 0)
                    {
                        rep3 = rep2;
                        rep2 = rep1;
                        rep1 = rep0;

                        len = _lenDecoder.Decode(rc, posState);
                        state = state < 7 ? 7 : 10;

                        int posSlot = DecodeTree(rc, _posSlotProbs[Math.Min(len, NumLenToPosStates - 1)], 6);
                        if (posSlot >= StartPosModelIndex)
                        {
                            int numDirectBits = (posSlot >> 1) - 1;
                            rep0 = (2 | (posSlot & 1)) << numDirectBits;

                            if (posSlot < EndPosModelIndex)
                            {
                                rep0 += DecodeReverseBitsTree(rc, _posProbs,
                                rep0 - posSlot - 1, numDirectBits);
                            }
                            else
                            {
                                rep0 += (int)(rc.DecodeDirectBits(numDirectBits - NumAlignBits) << NumAlignBits);
                                rep0 += DecodeReverseBitsTree(rc, _alignProbs, 0, NumAlignBits);
                            }
                        }
                        else
                        {
                            rep0 = posSlot;
                        }

                        if (rep0 < 0)
                        return;

                        len += 2;
                    }
                    else
                    {
                        if (rc.DecodeBit(_isRepG0, state) == 0)
                        {
                            if (rc.DecodeBit(_isRep0Long, (state << NumPosBitsMax) + posState) == 0)
                            {
                                state = state < 7 ? 9 : 11;
                                PutByte(GetDictByte(rep0));
                                continue;
                            }
                        }
                        else
                        {
                            int dist;
                            if (rc.DecodeBit(_isRepG1, state) == 0)
                            {
                                dist = rep1;
                            }
                            else
                            {
                                if (rc.DecodeBit(_isRepG2, state) == 0)
                                {
                                    dist = rep2;
                                }
                                else
                                {
                                    dist = rep3;
                                    rep3 = rep2;
                                }
                                rep2 = rep1;
                            }
                            rep1 = rep0;
                            rep0 = dist;
                        }

                        len = _repLenDecoder.Decode(rc, posState) + 2;
                        state = state < 7 ? 8 : 11;
                    }

                    for (int i = 0; i < len && _outPos < _outputSize; i++)
                    {
                        PutByte(GetDictByte(rep0));
                    }
                }
            }
        }

        private int GetLitState()
        {
            byte prevByte = _outPos > 0 ? _output[_outPos - 1] : (byte)0;
            return (((_outPos & ((1 << _lp) - 1)) << _lc) + (prevByte >> (8 - _lc)));
        }

        private byte GetDictByte(int dist)
        {
            int pos = _outPos - dist - 1;
            return pos >= 0 ? _output[pos] : (byte)0;
        }

        private void PutByte(byte b)
        {
            _output[_outPos++] = b;
        }

        private static byte DecodeLit(LzmaRangeDecoder rc, ushort[] probs)
        {
            uint symbol = 1;
            for (int i = 0; i < 8; i++)
            symbol = (symbol << 1) | rc.DecodeBit(probs, (int)symbol);
            return (byte)(symbol - 0x100);
        }

        private static byte DecodeLitMatched(LzmaRangeDecoder rc, ushort[] probs, byte matchByte)
        {
            uint symbol = 1;
            uint matchBit;
            int offset = 0x100;

            for (int i = 0; i < 8; i++)
            {
                matchBit = (uint)(matchByte >> (7 - i)) & 1;
                uint bit = rc.DecodeBit(probs, (int)(offset + (matchBit << 8) + symbol));
                symbol = (symbol << 1) | bit;

                if (matchBit != bit)
                {
                    while (symbol < 0x100)
                    symbol = (symbol << 1) | rc.DecodeBit(probs, (int)symbol);
                    break;
                }
            }
            return (byte)(symbol - 0x100);
        }

        private static int DecodeTree(LzmaRangeDecoder rc, ushort[] probs, int numBits)
        {
            uint symbol = 1;
            for (int i = 0; i < numBits; i++)
            symbol = (symbol << 1) | rc.DecodeBit(probs, (int)symbol);
            return (int)(symbol - (1u << numBits));
        }

        private static int DecodeReverseBitsTree(LzmaRangeDecoder rc, ushort[] probs, int startIndex, int numBits)
        {
            int result = 0;
            uint symbol = 1;
            for (int i = 0; i < numBits; i++)
            {
                uint bit = rc.DecodeBit(probs, startIndex + (int)symbol);
                symbol = (symbol << 1) | bit;
                result |= (int)(bit << i);
            }
            return result;
        }
    }

    private sealed class LenDecoder
    {
        private readonly ushort[] _choice;
        private readonly ushort[][] _lowCoder;
        private readonly ushort[][] _midCoder;
        private readonly ushort[] _highCoder;

        public LenDecoder(int pb)
        {
            int posStates = 1 << pb;
            _choice = new ushort[2];
            _choice[0] = _choice[1] = 1024;

            _lowCoder = new ushort[posStates][];
            _midCoder = new ushort[posStates][];
            for (int i = 0; i < posStates; i++)
            {
                _lowCoder[i] = CreateProbs(1 << 3);
                _midCoder[i] = CreateProbs(1 << 3);
            }
            _highCoder = CreateProbs(1 << 8);
        }

        private static ushort[] CreateProbs(int count)
        {
            var probs = new ushort[count];
            for (int i = 0; i < count; i++)
            probs[i] = 1024;
            return probs;
        }

        public int Decode(LzmaRangeDecoder rc, int posState)
        {
            if (rc.DecodeBit(_choice, 0) == 0)
            return DecodeTree(rc, _lowCoder[posState], 3);

            if (rc.DecodeBit(_choice, 1) == 0)
            return 8 + DecodeTree(rc, _midCoder[posState], 3);

            return 16 + DecodeTree(rc, _highCoder, 8);
        }

        private static int DecodeTree(LzmaRangeDecoder rc, ushort[] probs, int numBits)
        {
            uint symbol = 1;
            for (int i = 0; i < numBits; i++)
            symbol = (symbol << 1) | rc.DecodeBit(probs, (int)symbol);
            return (int)(symbol - (1u << numBits));
        }
    }
}