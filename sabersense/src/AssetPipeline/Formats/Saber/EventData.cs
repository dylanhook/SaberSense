// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;

namespace SaberSense.AssetPipeline.Formats.Saber;

public sealed class EventParseData
{
    public IReadOnlyList<EventManagerEntry> EventManagers { get; init; } = [];
    public IReadOnlyList<ComboFilterEntry> ComboFilters { get; init; } = [];
    public IReadOnlyList<EveryNthComboEntry> NthComboFilters { get; init; } = [];
    public IReadOnlyList<AccuracyFilterEntry> AccuracyFilters { get; init; } = [];

    internal IReadOnlyDictionary<long, int>? PathIdToTypeId { get; init; }

    public bool HasEvents =>
    EventManagers.Count is > 0 || ComboFilters.Count is > 0 ||
    NthComboFilters.Count is > 0 || AccuracyFilters.Count is > 0;
}

public sealed class EventCallEntry
{
    public long TargetPathId { get; init; }

    public string MethodName { get; init; } = string.Empty;

    public int Mode { get; init; }

    public int CallState { get; init; }

    public long ObjectArgumentPathId { get; init; }
    public int IntArgument { get; init; }
    public float FloatArgument { get; init; }
    public string StringArgument { get; init; } = string.Empty;
    public bool BoolArgument { get; init; }
}

public sealed class EventManagerEntry
{
    public long HostGameObjectPathId { get; init; }

    public IReadOnlyList<EventCallEntry> OnSlice { get; init; } = [];
    public IReadOnlyList<EventCallEntry> OnComboBreak { get; init; } = [];
    public IReadOnlyList<EventCallEntry> MultiplierUp { get; init; } = [];
    public IReadOnlyList<EventCallEntry> SaberStartColliding { get; init; } = [];
    public IReadOnlyList<EventCallEntry> SaberStopColliding { get; init; } = [];
    public IReadOnlyList<EventCallEntry> OnLevelStart { get; init; } = [];
    public IReadOnlyList<EventCallEntry> OnLevelFail { get; init; } = [];
    public IReadOnlyList<EventCallEntry> OnLevelEnded { get; init; } = [];
    public IReadOnlyList<EventCallEntry> OnBlueLightOn { get; init; } = [];
    public IReadOnlyList<EventCallEntry> OnRedLightOn { get; init; } = [];
    public IReadOnlyList<EventCallEntry> OnComboChanged { get; init; } = [];
    public IReadOnlyList<EventCallEntry> OnAccuracyChanged { get; init; } = [];

    public bool HasAnyCalls =>
    OnSlice.Count is > 0 || OnComboBreak.Count is > 0 || MultiplierUp.Count is > 0 ||
    SaberStartColliding.Count is > 0 || SaberStopColliding.Count is > 0 ||
    OnLevelStart.Count is > 0 || OnLevelFail.Count is > 0 || OnLevelEnded.Count is > 0 ||
    OnBlueLightOn.Count is > 0 || OnRedLightOn.Count is > 0 ||
    OnComboChanged.Count is > 0 || OnAccuracyChanged.Count is > 0;
}

public sealed class ComboFilterEntry
{
    public long HostGameObjectPathId { get; init; }
    public int ComboTarget { get; init; }
    public IReadOnlyList<EventCallEntry> NthComboReached { get; init; } = [];
}

public sealed class EveryNthComboEntry
{
    public long HostGameObjectPathId { get; init; }
    public int ComboStep { get; init; }
    public IReadOnlyList<EventCallEntry> NthComboReached { get; init; } = [];
}

public sealed class AccuracyFilterEntry
{
    public long HostGameObjectPathId { get; init; }
    public float Target { get; init; }
    public IReadOnlyList<EventCallEntry> OnAccuracyReachTarget { get; init; } = [];
    public IReadOnlyList<EventCallEntry> OnAccuracyHigherThanTarget { get; init; } = [];
    public IReadOnlyList<EventCallEntry> OnAccuracyLowerThanTarget { get; init; } = [];
}