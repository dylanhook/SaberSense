// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using SaberSense.Core.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SaberSense.Catalog;

public sealed class PreviewRow
{
    public string RelativePath { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string CreatorName { get; set; } = "";
    public byte[]? CoverBytes { get; set; }
    public bool IsSPICompatible { get; set; } = true;
    public int MetaVersion { get; set; }
    public string LastModified { get; set; } = "";
    public long FileSize { get; set; }
    public long FileLastModifiedTicks { get; set; }

    public string? ContentHash { get; set; }
}

public sealed class PreviewDatabase : IDisposable
{
    public const int CurrentMetaVersion = 2;

    private const uint MAGIC = 0x53534342;
    private const byte FormatVersion = 4;

    private readonly string _filePath;
    private readonly IModLogger _log;
    private readonly object _lock = new();
    private Dictionary<string, PreviewRow> _cache;
    private bool _dirty;

    private const int MAX_RECORD_COUNT = 50_000;
    private const int MAX_COVER_BYTES = 10 * 1024 * 1024;

    public PreviewDatabase(string filePath, IModLogger log)
    {
        _filePath = filePath;
        _log = log.ForSource(nameof(PreviewDatabase));
        _cache = new(StringComparer.OrdinalIgnoreCase);
    }

    public void Open()
    {
        if (!File.Exists(_filePath)) return;

        try
        {
            using var stream = File.OpenRead(_filePath);
            using var reader = new BinaryReader(stream);

            if (stream.Length is < 9) return;
            uint magic = reader.ReadUInt32();
            if (magic != MAGIC) return;
            byte version = reader.ReadByte();

            if (version != FormatVersion)
            {
                _log.Warn($"Preview cache format v{version} does not match supported v{FormatVersion} - will rebuild");
                return;
            }

            _log.Info($"Loading preview cache (format v{version}, {stream.Length} bytes)");

            int count = reader.ReadInt32();
            if (count is < 0 or > MAX_RECORD_COUNT)
            {
                _log.Warn($"Preview cache has suspicious record count ({count}), skipping.");
                return;
            }

            lock (_lock)
            {
                for (int i = 0; i < count; i++)
                {
                    var row = new PreviewRow
                    {
                        RelativePath = reader.ReadString(),
                        DisplayName = reader.ReadString(),
                        CreatorName = reader.ReadString(),
                    };

                    int coverLen = reader.ReadInt32();
                    if (coverLen is < 0 or > MAX_COVER_BYTES)
                    {
                        _log.Warn($"Preview cache record {i} has suspicious cover size ({coverLen}), skipping remainder.");
                        break;
                    }
                    row.CoverBytes = coverLen is > 0 ? reader.ReadBytes(coverLen) : null;

                    row.IsSPICompatible = reader.ReadBoolean();
                    row.MetaVersion = reader.ReadInt32();
                    row.LastModified = reader.ReadString();
                    row.FileSize = reader.ReadInt64();
                    row.FileLastModifiedTicks = reader.ReadInt64();
                    row.ContentHash = reader.ReadString();
                    if (string.IsNullOrEmpty(row.ContentHash)) row.ContentHash = null;

                    if (row.RelativePath is not null)
                    _cache[row.RelativePath] = row;
                }
            }
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to load preview cache: {ex}");
            lock (_lock) { _cache.Clear(); }
        }
    }

    public void Save()
    {
        if (!_dirty) return;

        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

            var tempPath = _filePath + ".tmp";

            lock (_lock)
            {
                using (var stream = File.Create(tempPath))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(MAGIC);
                    writer.Write(FormatVersion);
                    writer.Write(_cache.Count);

                    foreach (var row in _cache.Values)
                    {
                        writer.Write(row.RelativePath ?? "");
                        writer.Write(row.DisplayName ?? "");
                        writer.Write(row.CreatorName ?? "");

                        if (row.CoverBytes is { Length: > 0 })
                        {
                            writer.Write(row.CoverBytes.Length);
                            writer.Write(row.CoverBytes);
                        }
                        else
                        {
                            writer.Write(0);
                        }

                        writer.Write(row.IsSPICompatible);
                        writer.Write(row.MetaVersion);
                        writer.Write(row.LastModified ?? "");
                        writer.Write(row.FileSize);
                        writer.Write(row.FileLastModifiedTicks);
                        writer.Write(row.ContentHash ?? "");
                    }
                }
            }

            AtomicFileWriter.ReplaceFile(tempPath, _filePath);
            lock (_lock) { _dirty = false; }
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to save preview cache: {ex}");
        }
    }

    public void Dispose() => Save();

    public PreviewRow? GetPreview(string relativePath)
    {
        lock (_lock) { return _cache.TryGetValue(relativePath, out var row) ? row : null; }
    }

    public List<PreviewRow> GetAllPreviews()
    {
        lock (_lock) { return _cache.Values.ToList(); }
    }

    public void UpsertPreview(PreviewRow row)
    {
        lock (_lock)
        {
            _cache[row.RelativePath] = row;
            _dirty = true;
        }
    }

    public void DeletePreview(string relativePath)
    {
        lock (_lock)
        {
            if (_cache.Remove(relativePath))
            _dirty = true;
        }
    }

    public bool HasCurrentPreview(string relativePath)
    {
        lock (_lock)
        {
            return _cache.TryGetValue(relativePath, out var row) && row.MetaVersion >= CurrentMetaVersion;
        }
    }

    public bool HasCurrentPreview(string relativePath, long fileSize, long fileLastModifiedTicks)
    {
        lock (_lock)
        {
            return _cache.TryGetValue(relativePath, out var row)
            && row.MetaVersion >= CurrentMetaVersion
            && row.FileSize == fileSize
            && row.FileLastModifiedTicks == fileLastModifiedTicks;
        }
    }

    public byte[]? FindCoverByContentHash(string? contentHash)
    {
        if (string.IsNullOrEmpty(contentHash)) return null;
        lock (_lock)
        {
            foreach (var row in _cache.Values)
            {
                if (row.CoverBytes is { Length: > 0 } &&
                string.Equals(row.ContentHash, contentHash, StringComparison.OrdinalIgnoreCase))
                return row.CoverBytes;
            }
        }
        return null;
    }

    public void Clear()
    {
        lock (_lock)
        {
            _cache.Clear();
            _dirty = true;
        }
    }
}