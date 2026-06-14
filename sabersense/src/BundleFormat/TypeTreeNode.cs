// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;

namespace SaberSense.BundleFormat;

internal sealed class TypeTreeNode
{
    public string TypeName { get; set; } = "";

    public string FieldName { get; set; } = "";

    public int ByteSize { get; set; }

    public int Depth { get; set; }

    public bool IsAligned { get; set; }

    public bool IsArray { get; set; }

    public int Index { get; set; }

    public List<TypeTreeNode> Children { get; } = [];
}

internal sealed class SerializedType
{
    public int TypeId { get; set; }

    public TypeTreeNode? RootNode { get; set; }
}

internal sealed class ObjectInfo
{
    public long PathId { get; set; }

    public long ByteOffset { get; set; }

    public int ByteSize { get; set; }

    public int TypeIndex { get; set; }

    public int TypeId { get; set; }
}