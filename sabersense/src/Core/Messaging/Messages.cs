// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Catalog;
using SaberSense.Catalog.Model;
using SaberSense.Core.Logging;
using SaberSense.Loadout;
using SaberSense.Rendering;

namespace SaberSense.Core.Messaging;

internal readonly record struct SaberEquippedMsg(SaberAssetEntry Entry, EquipSource Source);

internal readonly record struct PreviewSaberChangedMsg(SaberAssetEntry Entry);

internal readonly record struct SaberPreviewInstantiatedMsg(LiveSaber Saber, SaberHand Hand);

internal readonly record struct TrailSettingsChangedMsg();

internal readonly record struct TrailMaterialEditedMsg(UnityEngine.Material Material);

internal readonly record struct SettingsChangedMsg();

internal readonly record struct ConfigLoadingMsg();

internal readonly record struct ConfigLoadedMsg();

internal readonly record struct CoverGeneratedMsg(AssetPreview Preview);

internal readonly record struct LogEntryMsg(LogEntry Entry);

internal readonly record struct PreviewsWipedMsg();

internal readonly record struct SaberLoadProgressMsg(string Phase, float Progress);

internal readonly record struct SaberLoadCompletedMsg();

internal readonly record struct ScanProgressMsg(int Completed, int Discovered);