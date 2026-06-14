// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;
using System;
using UnityEngine;
using Zenject;

namespace SaberSense.Gameplay;

internal sealed class WarningMarkerManager : IInitializable, IDisposable
{
    [Inject] private readonly BeatmapObjectManager _beatmapObjectManager = null!;
    [Inject] private readonly WarningMarkerBehavior.Pool _pool = null!;
    [Inject] private readonly ModSettings _settings = null!;

    private readonly WarningMarkerBehavior?[] _activeMarkers = new WarningMarkerBehavior?[2];
    private readonly NoteController?[] _activeNotes = new NoteController?[2];

    private float _lastLeftTime;
    private float _lastRightTime;
    private NoteCutDirection _lastLeftDir = NoteCutDirection.Up;
    private NoteCutDirection _lastRightDir = NoteCutDirection.Up;

    public void Initialize()
    {
        _beatmapObjectManager.noteWasSpawnedEvent += OnNoteSpawned;
        _beatmapObjectManager.noteWasDespawnedEvent += OnNoteDespawned;
    }

    public void Dispose()
    {
        _beatmapObjectManager.noteWasSpawnedEvent -= OnNoteSpawned;
        _beatmapObjectManager.noteWasDespawnedEvent -= OnNoteDespawned;
    }

    private void OnNoteSpawned(NoteController note)
    {
        if (!ShouldMark(note)) return;

        var colorType = note.noteData.colorType;
        if (colorType is not (ColorType.ColorA or ColorType.ColorB)) return;

        int idx = (int)colorType;

        if (_activeMarkers[idx] != null)
        {
            _pool.Despawn(_activeMarkers[idx]!);
            _activeMarkers[idx] = null;
            _activeNotes[idx] = null;
        }

        _activeMarkers[idx] = _pool.Spawn(note);
        _activeNotes[idx] = note;
    }

    private void OnNoteDespawned(NoteController note)
    {
        var colorType = note.noteData.colorType;
        if (colorType is not (ColorType.ColorA or ColorType.ColorB)) return;

        int idx = (int)colorType;
        if (_activeNotes[idx] == note && _activeMarkers[idx] != null)
        {
            _pool.Despawn(_activeMarkers[idx]!);
            _activeMarkers[idx] = null;
            _activeNotes[idx] = null;
        }
    }

    private bool ShouldMark(NoteController note)
    {
        var data = note.noteData;
        if (data.colorType == ColorType.None) return false;
        if (!PassesLayerFilter(data.noteLineLayer)) return false;

        var types = _settings.WarningTypes;

        if (types.Contains(2)) return true;

        bool isReset = types.Contains(0) && IsReset(data);
        bool isHorizontal = types.Contains(1) && IsHorizontal(data);

        return isReset || isHorizontal;
    }

    private bool PassesLayerFilter(NoteLineLayer layer)
    {
        var filter = _settings.WarningLayerFilter;
        return layer switch
        {
            NoteLineLayer.Top => filter.Contains(0),
            NoteLineLayer.Upper => filter.Contains(1),
            NoteLineLayer.Base => filter.Contains(2),
            _ => true
        };
    }

    private bool IsHorizontal(NoteData data)
    {
        if (data.noteLineLayer is not (NoteLineLayer.Base or NoteLineLayer.Top)) return false;
        return data.cutDirection is NoteCutDirection.Left or NoteCutDirection.Right;
    }

    private bool IsReset(NoteData data)
    {
        return data.colorType switch
        {
            ColorType.ColorA => CheckAngle(data.cutDirection, data.time, ref _lastLeftDir, ref _lastLeftTime),
            ColorType.ColorB => CheckAngle(data.cutDirection, data.time, ref _lastRightDir, ref _lastRightTime),
            _ => false
        };
    }

    private static bool CheckAngle(
    NoteCutDirection current, float time,
    ref NoteCutDirection lastDir, ref float lastTime)
    {
        bool tooClose = Mathf.Abs(time - lastTime) < 0.15f;
        lastTime = time;

        if (current == NoteCutDirection.Any)
        {
            if (!tooClose) lastDir = Opposite(lastDir);
            return false;
        }

        float angle = AngleBetween(lastDir, current);
        lastDir = current;
        return !tooClose && angle < 90f;
    }

    private static NoteCutDirection Opposite(NoteCutDirection dir) => dir switch
    {
        NoteCutDirection.Up => NoteCutDirection.Down,
        NoteCutDirection.Down => NoteCutDirection.Up,
        NoteCutDirection.Left => NoteCutDirection.Right,
        NoteCutDirection.Right => NoteCutDirection.Left,
        NoteCutDirection.UpLeft => NoteCutDirection.DownRight,
        NoteCutDirection.UpRight => NoteCutDirection.DownLeft,
        NoteCutDirection.DownLeft => NoteCutDirection.UpRight,
        NoteCutDirection.DownRight => NoteCutDirection.UpLeft,
        _ => dir
    };

    private static float AngleBetween(NoteCutDirection a, NoteCutDirection b)
    {
        return Mathf.Abs(Mathf.DeltaAngle(DirectionToAngle(a), DirectionToAngle(b)));
    }

    private static float DirectionToAngle(NoteCutDirection dir) => dir switch
    {
        NoteCutDirection.Up => 0f,
        NoteCutDirection.UpRight => 45f,
        NoteCutDirection.Right => 90f,
        NoteCutDirection.DownRight => 135f,
        NoteCutDirection.Down => 180f,
        NoteCutDirection.DownLeft => 225f,
        NoteCutDirection.Left => 270f,
        NoteCutDirection.UpLeft => 315f,
        _ => 0f
    };
}