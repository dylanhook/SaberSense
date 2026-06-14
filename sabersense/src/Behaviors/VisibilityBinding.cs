// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Persistence;
using System.Collections.Generic;

namespace SaberSense.Behaviors;

internal sealed class VisibilityBinding : ModifierBinding
{
    public override string Name { get; }
    public override string Category => "Visibility";

    private bool _shown;
    private readonly bool _defaultVisible;
    private readonly List<VisibilityRule> _sources = [];

    public bool Visible
    {
        get => _shown;
        set
        {
            _shown = value;
            SetActive(value);
        }
    }

    public VisibilityBinding(VisibilityRule def) : base(def.Id)
    {
        _shown = def.IsVisibleByDefault;
        _defaultVisible = def.IsVisibleByDefault;
        Name = def.Name;
    }

    public override void SetInstance(object instance)
    {
        if (instance is not VisibilityRule rule) return;

        _sources.RemoveAll(s => s?.Targets is null || (s.Targets.Count is > 0 && s.Targets[0] == null));
        if (!_sources.Contains(rule))
        _sources.Add(rule);
        SetActiveSingle(rule, _shown);
    }

    public override void Reset() => Visible = _sources.Count is > 0
    ? _sources[0].IsVisibleByDefault
    : _defaultVisible;

    public override void ReadFrom(JObject obj, IJsonProvider jsonProvider)
    {
        if (obj is not null && obj.TryGetValue(nameof(Visible), out var tok))
        Visible = tok.ToObject<bool>();
    }

    public override JToken WriteTo(IJsonProvider jsonProvider) =>
    new JObject { { nameof(Visible), Visible } };

    public override void Update() { }

    public override void Sync(object otherMod)
    {
        if (otherMod is VisibilityBinding peer)
        Visible = peer.Visible;
    }

    public override IEnumerable<ModifierParam> DescribeEditor(JObject? modJson, IJsonProvider jsonProvider)
    {
        yield return ModifierParam.Boolean(
        Name,
        () => modJson?[nameof(Visible)]?.ToObject<bool>() ?? Visible,
        value => Visible = value);
    }

    private void SetActive(bool show)
    {
        foreach (var source in _sources)
        SetActiveSingle(source, show);
    }

    private static void SetActiveSingle(VisibilityRule source, bool show)
    {
        if (source?.Targets is null) return;
        foreach (var go in source.Targets)
        if (go != null) go.SetActive(show);
    }
}