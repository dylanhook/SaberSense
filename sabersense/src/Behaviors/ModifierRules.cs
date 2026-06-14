// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;

namespace SaberSense.Behaviors;

public abstract class ModifierRuleBase
{
    public string Name { get; init; } = "";

    public int Id { get; init; }

    public List<int> TargetIndices { get; init; } = [];

    [Newtonsoft.Json.JsonIgnore]
    public List<UnityEngine.GameObject?> Targets { get; internal set; } = [];
}

public sealed class VisibilityRule : ModifierRuleBase
{
    public bool IsVisibleByDefault { get; init; }
}

public sealed class TransformRule : ModifierRuleBase;