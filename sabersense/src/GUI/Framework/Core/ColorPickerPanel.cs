// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SaberSense.GUI.Framework.Core;

internal sealed class ColorPickerPanel : Graphic
{
    public UIColorPicker? Picker;

    internal const float SvL = 0.028f, SvR = 0.861f, SvB = 0.111f, SvT = 0.972f;
    internal const float HuL = 0.883f, HuR = 0.972f, HuB = SvB, HuT = SvT;
    internal const float AlL = 0.028f, AlR = 0.861f, AlB = 0.028f, AlT = 0.089f;

    private const int SvGrid = 16;
    private const int HueSegs = 32;

    private const float OuterBorderFrac = 0.006f;
    private const float InnerBorderFrac = 0.011f;

    private const float SvCursorFrac = 0.018f;
    private const float SvCursorInnerScale = 0.6f;
    private const float HueCursorFrac = 0.015f;
    private const float HueCursorInsetFrac = 0.06f;
    private const float HueCursorInnerScale = 0.55f;
    private const float HueCursorInnerInsetScale = 1.5f;
    private const float AlphaCursorFrac = 0.015f;
    private const float AlphaCursorInsetFrac = 0.08f;
    private const float AlphaCursorInnerScale = 0.55f;
    private const float AlphaCursorInnerInsetScale = 1.5f;

    private static readonly Color32 CursorFillColor = new(255, 255, 255, 180);

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (Picker is null) return;

        Rect r = rectTransform.rect;
        float W = r.width, H = r.height;
        float X0 = r.xMin, Y0 = r.yMin;

        AddQuad(vh, X0, Y0, X0 + W, Y0 + H, C(10, 10, 10));

        float b1 = W * OuterBorderFrac;
        AddQuad(vh, X0 + b1, Y0 + b1, X0 + W - b1, Y0 + H - b1, C(60, 60, 60));

        float b2 = W * InnerBorderFrac;
        AddQuad(vh, X0 + b2, Y0 + b2, X0 + W - b2, Y0 + H - b2, C(40, 40, 40));

        float svBL = X0 + W * (SvL - OuterBorderFrac), svBR = X0 + W * (SvR + OuterBorderFrac);
        float svBB = Y0 + H * (SvB - OuterBorderFrac), svBT = Y0 + H * (SvT + OuterBorderFrac);
        AddQuad(vh, svBL, svBB, svBR, svBT, C(10, 10, 10));

        float svL = X0 + W * SvL, svR2 = X0 + W * SvR;
        float svB2 = Y0 + H * SvB, svT2 = Y0 + H * SvT;
        Color hueCol = Color.HSVToRGB(Picker.Hue, 1f, 1f);

        int svStart = vh.currentVertCount;
        int cols = SvGrid + 1;
        for (int iy = 0; iy <= SvGrid; iy++)
        {
            float ty = (float)iy / SvGrid;
            float y = Mathf.Lerp(svB2, svT2, ty);
            for (int ix = 0; ix <= SvGrid; ix++)
            {
                float tx = (float)ix / SvGrid;
                float x = Mathf.Lerp(svL, svR2, tx);
                Color baseC = Color.Lerp(Color.white, hueCol, tx);
                Color finalC = Color.Lerp(Color.black, baseC, ty);
                AddVert(vh, x, y, (Color32)finalC);
            }
        }
        for (int iy = 0; iy < SvGrid; iy++)
        {
            for (int ix = 0; ix < SvGrid; ix++)
            {
                int bl = svStart + iy * cols + ix;
                int br = bl + 1;
                int tl = bl + cols;
                int tr = tl + 1;
                vh.AddTriangle(bl, tl, tr);
                vh.AddTriangle(bl, tr, br);
            }
        }

        float huBL = X0 + W * (HuL - OuterBorderFrac), huBR = X0 + W * (HuR + OuterBorderFrac);
        float huBB = Y0 + H * (HuB - OuterBorderFrac), huBT = Y0 + H * (HuT + OuterBorderFrac);
        AddQuad(vh, huBL, huBB, huBR, huBT, C(10, 10, 10));

        float huL2 = X0 + W * HuL, huR2 = X0 + W * HuR;
        float huB2 = Y0 + H * HuB, huT2 = Y0 + H * HuT;

        int hueStart = vh.currentVertCount;
        for (int i = 0; i <= HueSegs; i++)
        {
            float t = (float)i / HueSegs;
            float y = Mathf.Lerp(huB2, huT2, t);

            Color hc = Color.HSVToRGB(1f - t, 1f, 1f);
            Color32 c32 = hc;
            AddVert(vh, huL2, y, c32);
            AddVert(vh, huR2, y, c32);
        }
        for (int i = 0; i < HueSegs; i++)
        {
            int bL = hueStart + i * 2;
            int bR = bL + 1;
            int tL = bL + 2;
            int tR = bL + 3;
            vh.AddTriangle(bL, tL, tR);
            vh.AddTriangle(bL, tR, bR);
        }

        float alBL = X0 + W * (AlL - OuterBorderFrac), alBR = X0 + W * (AlR + OuterBorderFrac);
        float alBB = Y0 + H * (AlB - OuterBorderFrac), alBT = Y0 + H * (AlT + OuterBorderFrac);
        AddQuad(vh, alBL, alBB, alBR, alBT, C(10, 10, 10));

        float alL2 = X0 + W * AlL, alR2 = X0 + W * AlR;
        float alB2 = Y0 + H * AlB, alT2 = Y0 + H * AlT;
        Color alphaC = Color.HSVToRGB(Picker.Hue, Picker.Sat, Picker.Val);
        Color32 alDark = C(0, 0, 0);
        Color32 alFull = (Color32)alphaC;
        int alStart = vh.currentVertCount;
        AddVert(vh, alL2, alB2, alDark);
        AddVert(vh, alL2, alT2, alDark);
        AddVert(vh, alR2, alT2, alFull);
        AddVert(vh, alR2, alB2, alFull);
        vh.AddTriangle(alStart, alStart + 1, alStart + 2);
        vh.AddTriangle(alStart, alStart + 2, alStart + 3);

        {
            float cx = Mathf.Lerp(svL, svR2, Picker.Sat);
            float cy = Mathf.Lerp(svB2, svT2, Picker.Val);
            float cs = W * SvCursorFrac;
            AddQuad(vh, cx - cs, cy - cs, cx + cs, cy + cs, C(10, 10, 10));
            float ci = cs * SvCursorInnerScale;
            AddQuad(vh, cx - ci, cy - ci, cx + ci, cy + ci, CursorFillColor);
        }

        {
            float hy = Mathf.Lerp(huB2, huT2, 1f - Picker.Hue);
            float cs = H * HueCursorFrac;
            float inset = (huR2 - huL2) * HueCursorInsetFrac;
            AddQuad(vh, huL2 + inset, hy - cs, huR2 - inset, hy + cs, C(10, 10, 10));
            float ci = cs * HueCursorInnerScale;
            float ini = inset * HueCursorInnerInsetScale;
            AddQuad(vh, huL2 + ini, hy - ci, huR2 - ini, hy + ci, CursorFillColor);
        }

        {
            float ax = Mathf.Lerp(alL2, alR2, Picker.Alpha);
            float cs = W * AlphaCursorFrac;
            float inset = (alT2 - alB2) * AlphaCursorInsetFrac;
            AddQuad(vh, ax - cs, alB2 + inset, ax + cs, alT2 - inset, C(10, 10, 10));
            float ci = cs * AlphaCursorInnerScale;
            float ini = inset * AlphaCursorInnerInsetScale;
            AddQuad(vh, ax - ci, alB2 + ini, ax + ci, alT2 - ini, CursorFillColor);
        }
    }

    private static Color32 C(byte r, byte g, byte b, byte a = 255) => new Color32(r, g, b, a);

    private static void AddVert(VertexHelper vh, float x, float y, Color32 col)
    {
        vh.AddVert(new Vector3(x, y, 0f), col, Vector4.zero);
    }

    private static void AddQuad(VertexHelper vh, float x0, float y0, float x1, float y1, Color32 col)
    {
        int i = vh.currentVertCount;
        AddVert(vh, x0, y0, col);
        AddVert(vh, x0, y1, col);
        AddVert(vh, x1, y1, col);
        AddVert(vh, x1, y0, col);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }

    internal bool ScreenToFrac(PointerEventData eventData, out float fx, out float fy)
    {
        fx = fy = 0;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
        rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 lp))
        return false;

        Rect r = rectTransform.rect;
        fx = (lp.x - r.xMin) / r.width;
        fy = (lp.y - r.yMin) / r.height;
        return true;
    }
}