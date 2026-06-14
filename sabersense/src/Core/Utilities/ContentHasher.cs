// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SaberSense.Core.Utilities;

internal static class ContentHasher
{
    public static string Compute(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return BytesToHex(hash);
    }

    public static string? TryCompute(string path)
    {
        try
        {
            return Compute(path);
        }
        catch (System.Exception ex)
        {
            ModLogger.ForSource("ContentHasher").Debug($"Hash computation failed for {Path.GetFileName(path)}: {ex.Message}");
            return null;
        }
    }

    private static string BytesToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}