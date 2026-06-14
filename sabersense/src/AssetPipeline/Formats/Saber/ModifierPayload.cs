// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;

namespace SaberSense.AssetPipeline.Formats.Saber;

public sealed class ModifierPayload
{
    public string DefinitionJson { get; }

    public IReadOnlyList<long> ObjectPathIds { get; }

    public long HostGameObjectPathId { get; }

    public ModifierPayload(string definitionJson, IReadOnlyList<long> objectPathIds, long hostGameObjectPathId)
    {
        DefinitionJson = definitionJson ?? string.Empty;
        ObjectPathIds = objectPathIds ?? [];
        HostGameObjectPathId = hostGameObjectPathId;
    }
}