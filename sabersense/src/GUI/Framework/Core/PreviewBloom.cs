// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SaberSense.GUI.Framework.Core;

[RequireComponent(typeof(Camera))]
internal sealed class PreviewBloom : MonoBehaviour
{
    private KawaseBlurRendererSO? _blurRenderer;
    private Material? _additiveMaterial;
    private bool _initialized;
    private bool _bloomEnabled = false;

    private const float NoBloomExposure = 2.0f;

    private static readonly int _alphaID = Shader.PropertyToID("_Alpha");

    private void OnEnable()
    {
        if (_initialized) return;
        _initialized = true;

        _blurRenderer = Resources.FindObjectsOfTypeAll<KawaseBlurRendererSO>().FirstOrDefault();
        if (_blurRenderer == null) return;

        var field = typeof(KawaseBlurRendererSO).GetField(
        "_additiveMaterial", BindingFlags.NonPublic | BindingFlags.Instance);
        _additiveMaterial = field?.GetValue(_blurRenderer) as Material;
    }

    public void SetBloom(bool val) => _bloomEnabled = val;

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (_bloomEnabled)
        {
            RenderWithBloom(src, dest);
        }
        else
        {
            RenderWithExposure(src, dest);
        }
    }

    private void RenderWithBloom(RenderTexture src, RenderTexture dest)
    {
        if (_blurRenderer == null || _additiveMaterial == null)
        {
            Graphics.Blit(src, dest);
            return;
        }

        var bloomTex = RenderTexture.GetTemporary(512, 512, 0,
        RenderTextureFormat.RGB111110Float, RenderTextureReadWrite.Linear);

        _blurRenderer.Bloom(src, bloomTex,
        0,
        4,
        0f,
        1f,
        KawaseBlurRendererSO.WeightsType.AlphaWeights,
        null
        );

        Graphics.Blit(src, dest);
        _additiveMaterial.SetFloat(_alphaID, 1f);
        Graphics.Blit(bloomTex, dest, _additiveMaterial);

        RenderTexture.ReleaseTemporary(bloomTex);
    }

    private void RenderWithExposure(RenderTexture src, RenderTexture dest)
    {
        if (_additiveMaterial != null)
        {
            Graphics.Blit(src, dest);
            _additiveMaterial.SetFloat(_alphaID, NoBloomExposure - 1f);
            Graphics.Blit(src, dest, _additiveMaterial);
        }
        else
        {
            Graphics.Blit(src, dest);
        }
    }
}