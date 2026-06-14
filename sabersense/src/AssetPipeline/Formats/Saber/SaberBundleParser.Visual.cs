// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.BundleFormat;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SaberSense.AssetPipeline.Formats.Saber;

public sealed partial class SaberBundleParser
{
    private const int MaxCoverDimension = 4096;

    private static (SaberMetadata metadata, long coverPathId) ReadSaberDescriptor(SerializedObject obj, ParseDiagnostics diagnostics)
    {
        var saberName = diagnostics.RequireString(obj, "SaberDescriptor", "SaberName", "Custom Saber");
        var authorName = obj.GetString("AuthorName", "Unknown");
        var description = obj.GetString("Description", string.Empty);

        var coverImageRef = obj.GetChild("CoverImage");
        var coverPathId = coverImageRef?.GetLong("m_PathID") ?? 0;

        return (new(saberName, authorName, description, null), coverPathId);
    }

    private CoverImageData? TryExtractCoverImage(AssetsFileReader assetsReader, long spritePathId, Dictionary<string, byte[]> bundleContent, bool silent = false)
    {
        try
        {
            ObjectInfo? spriteInfo = null;
            foreach (var info in assetsReader.Objects)
            {
                if (info.PathId == spritePathId) { spriteInfo = info; break; }
            }
            if (spriteInfo is null) return null;

            var sprite = assetsReader.ReadObject(spriteInfo);
            if (sprite is null) return null;

            var renderData = sprite.GetChild("m_RD");
            if (renderData is null) return null;

            var texRef = renderData.GetChild("texture");
            if (texRef is null) return null;

            var mFileId = texRef.GetInt("m_FileID");
            var mPathId = texRef.GetLong("m_PathID");

            if (mFileId is 0 && mPathId is not 0)
            {
                ObjectInfo? texInfo = null;
                foreach (var info in assetsReader.Objects)
                {
                    if (info.PathId == mPathId) { texInfo = info; break; }
                }

                if (texInfo is null)
                {
                    _log.Warn($"Texture2D pathId {mPathId} NOT FOUND in asset table");
                }
                else
                {
                    var texture = assetsReader.ReadObject(texInfo);
                    if (texture is not null)
                    {
                        int width = texture.GetInt("m_Width");
                        int height = texture.GetInt("m_Height");
                        int format = texture.GetInt("m_TextureFormat");
                        byte[]? imageData = texture.GetBytes("image data");

                        var streamInfo = texture.GetChild("m_StreamData");
                        if (streamInfo is not null)
                        {
                            long streamOffset = streamInfo.GetLong("offset");
                            int streamSize = streamInfo.GetInt("size");
                            string streamPath = streamInfo.GetString("path");

                            if (streamSize is > 0 && !string.IsNullOrEmpty(streamPath))
                            {
                                string streamFilename = Path.GetFileName(streamPath);
                                if (bundleContent.TryGetValue(streamFilename, out var resSData))
                                {
                                    if (streamOffset + streamSize <= resSData.Length)
                                    {
                                        imageData = new byte[streamSize];
                                        Buffer.BlockCopy(resSData, (int)streamOffset, imageData, 0, streamSize);
                                    }
                                }
                                else if (!silent)
                                {
                                    _log.Warn($"Missing resource file: {streamFilename}");
                                }
                            }
                        }

                        if (imageData is not null && imageData.Length is > 0 && width > 0 && height > 0)
                        {
                            if (width > MaxCoverDimension || height > MaxCoverDimension ||
                            !Enum.IsDefined(typeof(TextureFormat), format))
                            {
                                if (!silent)
                                _log.Warn($"Rejecting implausible cover image: {width}x{height} format={format}");
                                return null;
                            }
                            return new(width, height, format, imageData);
                        }
                    }
                }
            }

            if (mFileId is > 0)
            {
                _log.Warn($"Texture is in external file (fileId={mFileId}), needs multi-file extraction");
            }
            return null;
        }
        catch (Exception ex)
        {
            _log.Warn($"extraction failed: {ex}");
            return null;
        }
    }

    private static TrailData ReadCustomTrail(SerializedObject obj, ParseDiagnostics diagnostics)
    {
        var pointStart = obj.GetChild("PointStart");
        var pointEnd = obj.GetChild("PointEnd");
        var trailMaterial = obj.GetChild("TrailMaterial");
        var trailColor = obj.GetChild("TrailColor");
        var multiplierColor = obj.GetChild("MultiplierColor");

        return new()
        {
            PointStartPathId = pointStart?.GetLong("m_PathID") ?? 0,
            PointEndPathId = pointEnd?.GetLong("m_PathID") ?? 0,
            TrailMaterialPathId = trailMaterial?.GetLong("m_PathID") ?? 0,
            ColorType = obj.GetInt("colorType", 2),
            TrailColor = ReadColor(trailColor),
            MultiplierColor = ReadColor(multiplierColor),

            Length = diagnostics.RequireInt(obj, "CustomTrail", "Length", 20)
        };
    }

    private static Color ReadColor(SerializedObject? colorObj)
    {
        if (colorObj is null) return Color.white;
        return new Color(
        colorObj.GetFloat("r", 1f),
        colorObj.GetFloat("g", 1f),
        colorObj.GetFloat("b", 1f),
        colorObj.GetFloat("a", 1f));
    }

    private static ModifierPayload? ReadModifierCollection(SerializedObject obj)
    {
        var objectJson = obj.GetString("ObjectJson", string.Empty);
        if (string.IsNullOrEmpty(objectJson)) return null;

        var goRef = obj.GetChild("m_GameObject");
        var hostPathId = goRef?.GetLong("m_PathID") ?? 0;

        var pathIds = new List<long>();
        if (obj["Objects"] is List<object> pptrList)
        {
            foreach (var element in pptrList)
            {
                if (element is SerializedObject pptr)
                pathIds.Add(pptr.GetLong("m_PathID"));
            }
        }

        return new(objectJson, pathIds, hostPathId);
    }
}