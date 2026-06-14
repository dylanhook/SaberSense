// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Persistence;
using System.Collections.Generic;
using System.Linq;

namespace SaberSense.Behaviors;

public abstract class ModifierBinding
{
    public abstract string Name { get; }

    public abstract string Category { get; }

    public int Id { get; }

    protected ModifierBinding(int id) => Id = id;

    public abstract void SetInstance(object instance);

    public abstract void Reset();

    public virtual void OnSelected(params object[] args) { }

    public virtual void Tick() { }

    public abstract void ReadFrom(JObject obj, IJsonProvider jsonProvider);

    public abstract JToken WriteTo(IJsonProvider jsonProvider);

    public abstract void Update();

    public abstract void Sync(object otherMod);

    public virtual IEnumerable<ModifierParam> DescribeEditor(JObject? modJson, IJsonProvider jsonProvider) =>
    Enumerable.Empty<ModifierParam>();
}