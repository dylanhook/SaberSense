// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;

namespace SaberSense.Core.Utilities;
internal sealed class SelectionRandomizer
{
    private readonly System.Random _random = new();

    public T? PickRandom<T>(IList<T> source)
    {
        if (source.Count is 0) return default;
        return source[_random.Next(source.Count)];
    }
}