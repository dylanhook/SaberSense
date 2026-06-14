// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SaberSense.Services;

internal interface IConfigStore
{
    event Action? OnConfigsChanged;

    Task? CurrentTask { get; }

    void StartWatching();

    void StopWatching();

    Task InitializeLoadoutAsync();

    Task EnsureAssetsValidAsync();

    Task ValidateActiveConfigAsync();

    List<ConfigInfo> GetConfigs();

    Task SaveAsync(string name);

    Task LoadAsync(string name);

    bool Delete(string name);

    Task ResetToDefaultsAsync();

    Task<string?> ExportAsync();

    Task<bool> ImportAsync(string clipboardData);
}