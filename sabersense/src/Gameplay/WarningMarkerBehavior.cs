// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using IPA.Utilities;
using JetBrains.Annotations;
using UnityEngine;
using Zenject;

namespace SaberSense.Gameplay;

internal sealed class WarningMarkerBehavior : MonoBehaviour
{
    private static Mesh? _proceduralMesh;
    private static Material? _sharedMaterial;
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

    private MeshRenderer _meshRenderer = null!;
    private MeshFilter _meshFilter = null!;
    private MaterialPropertyBlock? _propertyBlock;

    private Color _baseColor;
    private Vector3 _baseScale;

    private void Awake()
    {
        EnsureComponents();
    }

    private void EnsureComponents()
    {
        if (_propertyBlock is not null) return;

        _propertyBlock = new();

        _meshRenderer = gameObject.GetComponent<MeshRenderer>() ?? gameObject.AddComponent<MeshRenderer>();
        _meshFilter = gameObject.GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();

        _proceduralMesh ??= CreateProceduralMesh();

        if (_sharedMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            _sharedMaterial = new Material(shader);
        }

        if (_meshRenderer.sharedMaterial == null)
        _meshRenderer.sharedMaterial = _sharedMaterial;
    }

    private void Update()
    {
        if (_propertyBlock is null) return;

        float pulse = (Mathf.Sin(Time.timeSinceLevelLoad * PulseSpeed) + 1f) * 0.5f;

        float brightness = Mathf.Lerp(PulseMinBrightness, PulseMaxBrightness, pulse);
        var color = _baseColor * brightness;
        color.a = Mathf.Lerp(PulseMinAlpha, PulseMaxAlpha, pulse);

        _propertyBlock.SetColor(ColorPropertyId, color);
        _meshRenderer.SetPropertyBlock(_propertyBlock);

        transform.localScale = _baseScale * Mathf.Lerp(PulseMinScale, PulseMaxScale, pulse);
    }

    public void Setup(Color saberColor)
    {
        EnsureComponents();
        _baseColor = saberColor;
        _propertyBlock!.SetColor(ColorPropertyId, saberColor);
        _meshRenderer!.SetPropertyBlock(_propertyBlock);
    }

    private const float MarkerSize = 0.14f;
    private const float MarkerOffset = 1.0f;

    private const float PulseSpeed = 25f;
    private const float PulseMinBrightness = 1.2f;
    private const float PulseMaxBrightness = 3.5f;
    private const float PulseMinAlpha = 0.6f;
    private const float PulseMaxAlpha = 1.0f;
    private const float PulseMinScale = 0.95f;
    private const float PulseMaxScale = 1.1f;

    private static class Reflectors
    {
        public static readonly FieldAccessor<NoteController, NoteMovement>.Accessor NoteMovement
        = FieldAccessor<NoteController, NoteMovement>.GetAccessor("_noteMovement");
        public static readonly FieldAccessor<NoteMovement, NoteJump>.Accessor NoteJump
        = FieldAccessor<NoteMovement, NoteJump>.GetAccessor("_jump");
        public static readonly FieldAccessor<NoteJump, IVariableMovementDataProvider>.Accessor MovementProvider
        = FieldAccessor<NoteJump, IVariableMovementDataProvider>.GetAccessor("_variableMovementDataProvider");
        public static readonly FieldAccessor<NoteJump, Vector3>.Accessor StartOffset
        = FieldAccessor<NoteJump, Vector3>.GetAccessor("_startOffset");
        public static readonly FieldAccessor<NoteJump, Vector3>.Accessor EndOffset
        = FieldAccessor<NoteJump, Vector3>.GetAccessor("_endOffset");
        public static readonly FieldAccessor<NoteJump, float>.Accessor GravityBase
        = FieldAccessor<NoteJump, float>.GetAccessor("_gravityBase");
        public static readonly FieldAccessor<NoteJump, Quaternion>.Accessor EndRotation
        = FieldAccessor<NoteJump, Quaternion>.GetAccessor("_endRotation");
    }

    [UsedImplicitly]
    internal class Pool : MonoMemoryPool<NoteController, WarningMarkerBehavior>
    {
        [Inject, UsedImplicitly]
        private readonly ColorManager _colorManager = null!;

        [Inject, UsedImplicitly]
        private readonly ViewVisibilityService _viewVis = null!;

        protected override void Reinitialize(NoteController noteController, WarningMarkerBehavior marker)
        {
            var noteMovement = Reflectors.NoteMovement(ref noteController);
            var noteJump = Reflectors.NoteJump(ref noteMovement);

            var movementProvider = Reflectors.MovementProvider(ref noteJump);
            var startOffset = Reflectors.StartOffset(ref noteJump);
            var endOffset = Reflectors.EndOffset(ref noteJump);
            var gravityBase = Reflectors.GravityBase(ref noteJump);

            var jumpDuration = movementProvider.jumpDuration;
            var gravity = movementProvider.CalculateCurrentNoteJumpGravity(gravityBase);
            var startPos = movementProvider.moveEndPosition + startOffset;
            var endPos = movementProvider.jumpEndPosition + endOffset;

            float halfJump = jumpDuration * 0.5f;
            float startVelocity = gravity * halfJump;

            var beatPos = (startPos + endPos) * 0.5f;
            var pos = beatPos;
            pos.x = endPos.x;
            pos.z += MarkerOffset;
            pos.y = startPos.y + startVelocity * halfJump - gravity * halfJump * halfJump * 0.5f;

            marker._baseScale = new Vector3(MarkerSize, MarkerSize, MarkerSize);
            marker.transform.localScale = marker._baseScale;
            marker.transform.SetPositionAndRotation(pos, Reflectors.EndRotation(ref noteJump));

            var saberColor = _colorManager.ColorForType(noteController.noteData.colorType);
            marker.Setup(saberColor);

            _viewVis.ApplyLayers(marker.gameObject, ViewFeature.WarningMarkers);

            if (marker._meshFilter != null)
            marker._meshFilter.sharedMesh = _proceduralMesh;
        }
    }

    private static Mesh CreateProceduralMesh()
    {
        var mesh = new Mesh { name = "SaberSense_WarningMarker" };

        Vector3[] vertices =
        [
        new(-0.15f,  0.1f,  0f), new( 0.15f,  0.1f,  0f), new( 0.0f, -0.15f, 0f),
        new(-0.25f,  0.4f,  0f), new( 0.25f,  0.4f,  0f), new( 0.0f,  0.2f,  0f),
        new(-0.2f,  -0.25f, 0f), new( 0.2f,  -0.25f, 0f), new( 0.0f, -0.5f,  0f),
        new(-0.45f,  0.15f, 0f), new(-0.35f,  0.1f,  0f), new(-0.15f,-0.3f,  0f), new(-0.25f,-0.25f, 0f),
        new( 0.35f,  0.1f,  0f), new( 0.45f,  0.15f, 0f), new( 0.25f,-0.25f, 0f), new( 0.15f,-0.3f,  0f)
        ];

        int[] triangles = [2, 1, 0, 5, 4, 3, 8, 7, 6, 11, 10, 9, 11, 9, 12, 16, 14, 13, 16, 15, 14];

        var uvs = new Vector2[vertices.Length];
        for (int i = 0; i < uvs.Length; i++)
        uvs[i] = new Vector2((vertices[i].x + 0.45f) / 0.9f, (vertices[i].y + 0.5f) / 0.9f);

        var doubleTris = new int[triangles.Length * 2];
        triangles.CopyTo(doubleTris, 0);
        for (int i = 0; i < triangles.Length; i += 3)
        {
            doubleTris[triangles.Length + i] = triangles[i];
            doubleTris[triangles.Length + i + 1] = triangles[i + 2];
            doubleTris[triangles.Length + i + 2] = triangles[i + 1];
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = doubleTris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}