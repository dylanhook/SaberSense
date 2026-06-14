// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using Newtonsoft.Json.Linq;
using SaberSense.Core;

namespace SaberSense.Profiles;

internal sealed class SplitPropertyManager
{
    private readonly System.Collections.Generic.Dictionary<string, JObject> _overrides;

    public SplitPropertyManager(System.Collections.Generic.Dictionary<string, JObject> overrides)
    {
        _overrides = overrides;
    }

    public bool IsPropertySplit(string matName, string propName)
    {
        if (!_overrides.TryGetValue(matName, out var matObj)) return false;
        var val = matObj[propName];
        return val is JObject obj && obj.ContainsKey("Left") && obj.ContainsKey("Right");
    }

    public void SplitProperty(string matName, string propName)
    {
        if (!_overrides.TryGetValue(matName, out var matObj)) return;
        var val = matObj[propName];
        if (val is null) return;
        if (val is JObject jObj && jObj.ContainsKey("Left")) return;
        matObj[propName] = new JObject
        {
            ["Left"] = val.DeepClone(),
            ["Right"] = val.DeepClone()
        };
    }

    public void UnsplitProperty(string matName, string propName)
    {
        if (!_overrides.TryGetValue(matName, out var matObj)) return;
        if (matObj[propName] is not JObject obj || !obj.ContainsKey("Left")) return;
        matObj[propName] = obj["Left"]!.DeepClone();
    }

    public JToken? GetPropertyForHand(string matName, string propName, SaberHand hand)
    {
        if (!_overrides.TryGetValue(matName, out var matObj)) return null;
        var val = matObj[propName];
        if (val is null) return null;
        if (val is JObject obj && obj.ContainsKey("Left"))
        return hand == SaberHand.Left ? obj["Left"] : obj["Right"];
        return val;
    }

    public void SetPropertyForHand(string matName, string propName, JToken value, SaberHand hand)
    {
        if (!_overrides.ContainsKey(matName))
        _overrides[matName] = new();
        var matObj = _overrides[matName];
        if (matObj[propName] is JObject obj && obj.ContainsKey("Left"))
        {
            obj[hand == SaberHand.Left ? "Left" : "Right"] = value;
            return;
        }
        matObj[propName] = value;
    }
}