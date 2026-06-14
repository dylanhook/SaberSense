// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.App;
using SaberSense.Core.Logging;
using SaberSense.Persistence;
using System.Collections.Generic;
using System.IO;

namespace SaberSense.Configuration;

internal sealed class InternalConfig
{
    private const string FileName = "internal.sabersense";

    public string ActiveConfigName { get; set; } = "default";
    public List<string> PinnedSabers { get; set; } = [];

    private readonly string _filePath;
    private readonly IModLogger _log;

    public InternalConfig(AppPaths paths, IModLogger log)
    {
        _filePath = Path.Combine(paths.DataRoot.FullName, FileName);
        _log = log.ForSource(nameof(InternalConfig));
    }

    public void Load()
    {
        if (!File.Exists(_filePath)) return;

        try
        {
            var obj = ConfigEnvelope.ReadFromDisk(_filePath);
            if (obj.TryGetValue(nameof(ActiveConfigName), out var nameToken))
            ActiveConfigName = nameToken.Value<string>() ?? "default";
            if (obj.TryGetValue(nameof(PinnedSabers), out var pinsToken) && pinsToken is JArray pinsArray)
            PinnedSabers = pinsArray.ToObject<List<string>>() ?? new();
        }
        catch (System.Exception ex)
        {
            _log.Error($"Failed to load: {ex.Message}");
        }
    }

    public void Save()
    {
        try
        {
            var obj = new JObject
            {
                [nameof(ActiveConfigName)] = ActiveConfigName,
                [nameof(PinnedSabers)] = JArray.FromObject(PinnedSabers),
            };
            ConfigEnvelope.WriteToDisk(_filePath, obj);
        }
        catch (System.Exception ex)
        {
            _log.Error($"Failed to save: {ex.Message}");
        }
    }
}