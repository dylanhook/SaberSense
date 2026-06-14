// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SaberSense.Core.Utilities;
using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace SaberSense.Persistence;

internal static class ConfigEnvelope
{
    private static readonly byte[] Magic = { 0x53, 0x53, 0x43, 0x46 };

    private const int MaxClipboardBase64Bytes = 10 * 1024 * 1024;
    private const int DecompressBufferSize = 8192;

    private const string ClipboardPrefix = "SABERSENSE:";

    public static void WriteToDisk(string path, JObject payload)
    {
        var data = Serialize(payload);
        AtomicFileWriter.WriteAllBytes(path, data);
    }

    public static JObject ReadFromDisk(string path)
    {
        var data = File.ReadAllBytes(path);
        return Deserialize(data);
    }

    public static byte[] Serialize(JObject payload)
    {
        var json = payload.ToString(Formatting.None);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        byte[] checksum;
        using (var sha = SHA256.Create())
        checksum = sha.ComputeHash(jsonBytes);

        var compressed = Compress(jsonBytes);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(Magic);
        bw.Write(checksum);
        bw.Write(compressed.Length);
        bw.Write(compressed);
        return ms.ToArray();
    }

    public static JObject Deserialize(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        var magic = br.ReadBytes(4);
        if (magic.Length < 4 || magic[0] != Magic[0] || magic[1] != Magic[1]
        || magic[2] != Magic[2] || magic[3] != Magic[3])
        throw new InvalidDataException("Not a valid .sabersense file (bad magic bytes).");

        var storedChecksum = br.ReadBytes(32);

        var compressedLength = br.ReadInt32();
        if (compressedLength < 0 || compressedLength > data.Length - ms.Position)
        throw new InvalidDataException(
        $"[ConfigEnvelope] Declared payload length {compressedLength} exceeds remaining file bytes ({data.Length - ms.Position}).");
        var compressed = br.ReadBytes(compressedLength);
        var jsonBytes = Decompress(compressed);

        byte[] actualChecksum;
        using (var sha = SHA256.Create())
        actualChecksum = sha.ComputeHash(jsonBytes);

        if (!ChecksumsMatch(storedChecksum, actualChecksum))
        throw new InvalidDataException("[ConfigEnvelope] Checksum mismatch - file is corrupted or was modified externally.");

        var json = Encoding.UTF8.GetString(jsonBytes);
        return JObject.Parse(json);
    }

    public static string ToClipboardString(JObject payload)
    {
        var json = payload.ToString(Formatting.None);
        var compressed = Compress(Encoding.UTF8.GetBytes(json));
        return $"{ClipboardPrefix}{Convert.ToBase64String(compressed)}";
    }

    public static JObject FromClipboardString(string clipboard)
    {
        if (string.IsNullOrWhiteSpace(clipboard) || !clipboard.StartsWith(ClipboardPrefix, StringComparison.Ordinal))
        throw new FormatException("Invalid clipboard data - expected 'SABERSENSE:' prefix.");

        var body = clipboard[ClipboardPrefix.Length..];

        if (body.Length > MaxClipboardBase64Bytes)
        throw new InvalidDataException("[ConfigEnvelope] Clipboard data exceeds size limit.");

        var compressed = Convert.FromBase64String(body);
        var jsonBytes = Decompress(compressed);
        return JObject.Parse(Encoding.UTF8.GetString(jsonBytes));
    }

    private static byte[] Compress(byte[] raw)
    {
        using var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionMode.Compress))
        gz.Write(raw, 0, raw.Length);
        return output.ToArray();
    }

    private const int MaxDecompressedBytes = 50 * 1024 * 1024;

    private static byte[] Decompress(byte[] compressed)
    {
        using var input = new MemoryStream(compressed);
        using var gz = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[DecompressBufferSize];
        int totalRead = 0;
        int bytesRead;
        while ((bytesRead = gz.Read(buffer, 0, buffer.Length)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > MaxDecompressedBytes)
            throw new InvalidDataException(
            $"[ConfigEnvelope] Decompressed payload exceeds {MaxDecompressedBytes / (1024 * 1024)} MB limit.");
            output.Write(buffer, 0, bytesRead);
        }
        return output.ToArray();
    }

    private static bool ChecksumsMatch(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++)
        diff |= a[i] ^ b[i];
        return diff == 0;
    }
}