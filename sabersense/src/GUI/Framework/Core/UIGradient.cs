// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.GUI.Framework.Core;

public static class UIGradient
{
    private static Texture2D? _panelGradientTex;
    private static Sprite? _panelGradientSpr;

    private static Texture2D? _accentGlowTex;
    private static Sprite? _accentGlowSpr;

    private static Texture2D? _btnNormalTex, _btnHoverTex, _btnPressedTex;
    private static Sprite? _btnNormalSpr, _btnHoverSpr, _btnPressedSpr;

    private static Texture2D? _tglUncheckedTex, _tglHoverTex;
    private static Sprite? _tglUncheckedSpr, _tglHoverSpr;

    private static Texture2D? _sldNormalTex, _sldHoverTex;
    private static Sprite? _sldNormalSpr, _sldHoverSpr;

    private static Texture2D? _cmbNormalTex, _cmbHoverTex;
    private static Sprite? _cmbNormalSpr, _cmbHoverSpr;

    private static Texture2D? _accentVertTex;
    private static Sprite? _accentVertSpr;

    public static Texture2D Create(int width, int height, Color top, Color bottom)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < height; y++)
        {
            float t = (float)y / (height - 1);
            Color c = Color.Lerp(bottom, top, t);
            for (int x = 0; x < width; x++)
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    public static Texture2D CreateHorizontal(int width, int height, Color left, Color right)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int x = 0; x < width; x++)
        {
            float t = (float)x / (width - 1);
            Color c = Color.Lerp(left, right, t);
            for (int y = 0; y < height; y++)
            tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    public static Sprite BtnNormal => GetOrCreateSprite(ref _btnNormalTex, ref _btnNormalSpr, 4, 32, UITheme.SurfaceLight, UITheme.SurfacePressed);
    public static Sprite BtnHover => GetOrCreateSprite(ref _btnHoverTex, ref _btnHoverSpr, 4, 32, UITheme.SurfaceHover, UITheme.SurfaceLight);
    public static Sprite BtnPressed => GetOrCreateSprite(ref _btnPressedTex, ref _btnPressedSpr, 4, 32, new Color32(30, 30, 30, 255), UITheme.Surface);

    public static Sprite TglUnchecked => GetOrCreateSprite(ref _tglUncheckedTex, ref _tglUncheckedSpr, 4, 32, new Color32(75, 75, 75, 255), new Color32(51, 51, 51, 255));
    public static Sprite TglHover => GetOrCreateSprite(ref _tglHoverTex, ref _tglHoverSpr, 4, 32, new Color32(83, 83, 83, 255), new Color32(58, 58, 58, 255));

    public static Sprite SldNormal => GetOrCreateSprite(ref _sldNormalTex, ref _sldNormalSpr, 4, 32, new Color32(52, 52, 52, 255), new Color32(68, 68, 68, 255));
    public static Sprite SldHover => GetOrCreateSprite(ref _sldHoverTex, ref _sldHoverSpr, 4, 32, new Color32(57, 57, 57, 255), new Color32(73, 73, 73, 255));

    public static Sprite CmbNormal => GetOrCreateSprite(ref _cmbNormalTex, ref _cmbNormalSpr, 4, 32, new Color32(31, 31, 31, 255), new Color32(36, 36, 36, 255));
    public static Sprite CmbHover => GetOrCreateSprite(ref _cmbHoverTex, ref _cmbHoverSpr, 4, 32, new Color32(41, 41, 41, 255), new Color32(46, 46, 46, 255));

    public static Sprite AccentVert => GetOrCreateSprite(ref _accentVertTex, ref _accentVertSpr, 4, 32, UITheme.Accent, UITheme.Accent * 0.75f);

    public static void InvalidateAccent()
    {
        if (_accentVertTex != null)
        {
            Color top = UITheme.Accent, bot = UITheme.Accent * 0.75f;
            bot.a = 1f;
            for (int y = 0; y < 32; y++)
            {
                Color c = Color.Lerp(bot, top, y / 31f);
                for (int x = 0; x < 4; x++)
                _accentVertTex.SetPixel(x, y, c);
            }
            _accentVertTex.Apply();
        }

        if (_accentGlowTex != null)
        {
            Color glowColor = UITheme.Accent;
            const float falloff = 0.5f;
            Vector2 center = new(16f, 16f);
            float maxDist = 16f;
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    float alpha = Mathf.Clamp01(1f - Mathf.Pow(dist, falloff)) * glowColor.a * 0.3f;
                    _accentGlowTex.SetPixel(x, y, new Color(glowColor.r, glowColor.g, glowColor.b, alpha));
                }
            }
            _accentGlowTex.Apply();
        }
    }

    public static void ClearCache()
    {
        DestroyPair(ref _panelGradientTex, ref _panelGradientSpr);
        DestroyPair(ref _accentGlowTex, ref _accentGlowSpr);
        DestroyPair(ref _btnNormalTex, ref _btnNormalSpr);
        DestroyPair(ref _btnHoverTex, ref _btnHoverSpr);
        DestroyPair(ref _btnPressedTex, ref _btnPressedSpr);
        DestroyPair(ref _tglUncheckedTex, ref _tglUncheckedSpr);
        DestroyPair(ref _tglHoverTex, ref _tglHoverSpr);
        DestroyPair(ref _sldNormalTex, ref _sldNormalSpr);
        DestroyPair(ref _sldHoverTex, ref _sldHoverSpr);
        DestroyPair(ref _cmbNormalTex, ref _cmbNormalSpr);
        DestroyPair(ref _cmbHoverTex, ref _cmbHoverSpr);
        DestroyPair(ref _accentVertTex, ref _accentVertSpr);
    }

    private static void DestroyPair(ref Texture2D? tex, ref Sprite? spr)
    {
        if (spr != null) { UnityEngine.Object.Destroy(spr); spr = null; }
        if (tex != null) { UnityEngine.Object.Destroy(tex); tex = null; }
    }

    public static Sprite PanelGradient
    {
        get
        {
            if (_panelGradientSpr == null)
            {
                _panelGradientTex = Create(4, 64,
                new Color(0.12f, 0.13f, 0.18f, 0.97f),
                new Color(0.04f, 0.04f, 0.07f, 0.99f));
                _panelGradientSpr = Sprite.Create(_panelGradientTex, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f));
            }
            return _panelGradientSpr;
        }
    }

    public static Sprite AccentGlow
    {
        get
        {
            if (_accentGlowSpr == null)
            {
                _accentGlowTex = CreateRadialGlow(32, 32, UITheme.Accent, 0.5f);
                _accentGlowSpr = Sprite.Create(_accentGlowTex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
            }
            return _accentGlowSpr;
        }
    }

    private static Sprite GetOrCreateSprite(ref Texture2D? tex, ref Sprite? spr, int w, int h, Color top, Color bottom)
    {
        if (spr == null)
        {
            if (tex == null) tex = Create(w, h, top, bottom);
            spr = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }
        return spr;
    }

    public static Texture2D CreateRadialGlow(int width, int height, Color glowColor, float falloff)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new(width * 0.5f, height * 0.5f);
        float maxDist = Mathf.Min(center.x, center.y);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                float alpha = Mathf.Clamp01(1f - Mathf.Pow(dist, falloff)) * glowColor.a;
                tex.SetPixel(x, y, new Color(glowColor.r, glowColor.g, glowColor.b, alpha * 0.3f));
            }
        }
        tex.Apply();
        return tex;
    }
}