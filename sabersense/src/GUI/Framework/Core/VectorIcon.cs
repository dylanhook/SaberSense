// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

[RequireComponent(typeof(CanvasRenderer))]
internal sealed class VectorGraphic : Graphic
{
    private List<Vector2>? _verts;
    private List<int>? _tris;
    private VectorBounds _bounds;

    public override Texture? mainTexture => null;

    public void SetShape(List<List<Vector2>> contours)
    {
        _tris = PolygonTriangulator.Triangulate(contours, out _verts);
        _bounds = VectorBounds.Compute(_verts);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (_verts is null || _tris is null || _verts.Count is 0 || !_bounds.IsValid)
        return;

        var rect = GetPixelAdjustedRect();
        var c = color;

        float scale = Mathf.Min(rect.width / _bounds.Width, rect.height / _bounds.Height);
        float offsetX = rect.x + (rect.width - _bounds.Width * scale) * 0.5f;
        float offsetY = rect.y + (rect.height - _bounds.Height * scale) * 0.5f;

        for (int i = 0; i < _verts.Count; i++)
        {
            var v = _verts[i];
            float x = (v.x - _bounds.MinX) * scale + offsetX;
            float y = rect.yMax - ((v.y - _bounds.MinY) * scale + (rect.height - _bounds.Height * scale) * 0.5f);
            vh.AddVert(new Vector3(x, y, 0), c, Vector4.zero);
        }

        for (int i = 0; i + 2 < _tris.Count; i += 3)
        {
            vh.AddTriangle(_tris[i], _tris[i + 1], _tris[i + 2]);
        }
    }
}

public sealed class VectorIcon : UIElement
{
    public Graphic GraphicComponent { get; private set; }

    private VectorIcon(string name, string svgPath) : base(name)
    {
        var graphic = GameObject.AddComponent<VectorGraphic>();
        graphic.material = UIMaterials.NoBloomMaterial;
        graphic.raycastTarget = false;

        var contours = SvgPathParser.Parse(svgPath);
        graphic.SetShape(contours);

        GraphicComponent = graphic;
    }

    public static VectorIcon Create(string name, string svgPath)
    {
        return new VectorIcon(name, svgPath);
    }

    public VectorIcon SetColor(Color color)
    {
        GraphicComponent.color = color;
        return this;
    }
}