// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Configuration;
using SaberSense.Core.Logging;
using System;
using UnityEngine;
using Zenject;

namespace SaberSense.Gameplay;

internal sealed class SaberEventRouter : IDisposable
{
    [Inject] private readonly BeatmapObjectManager _noteTracker = null!;
    [Inject] private readonly GameEnergyCounter _hpCounter = null!;
    [InjectOptional] private readonly ObstacleSaberSparkleEffectManager? _wallSparks = null;
    [Inject] private readonly ModSettings _settings = null!;
    [Inject] private readonly IScoreController _scoring = null!;
    [Inject] private readonly IComboController _combo = null!;
    [Inject] private readonly RelativeScoreAndImmediateRankCounter _accuracy = null!;
    [Inject] private readonly IReadonlyBeatmapData _beatmap = null!;
    [Inject] private readonly BeatmapCallbacksController _beatmapCallbacks = null!;
    [InjectOptional] private readonly GameCoreSceneSetupData? _sceneData = null;

    private static readonly IModLogger _log = ModLogger.ForSource(nameof(SaberEventRouter));

    public bool IsBound { get; private set; }

    private SaberEventDispatcher? _dispatcher;
    private float? _finalNoteTimestamp;
    private SaberType _trackedHand;
    private float _cachedAcc;
    private int _prevMultiplier = 1;
    private BeatmapDataCallbackWrapper? _lightColorCallback;

    public void BindEvents(SaberEventDispatcher dispatcher, SaberType hand)
    {
        _dispatcher = dispatcher;
        _trackedHand = hand;

        if (!_settings.EnableEventManager || _sceneData == null || !dispatcher.HasAnyCalls)
        return;

        IsBound = true;

        _finalNoteTimestamp = GetLastNoteTime();
        if (!_finalNoteTimestamp.HasValue)
        _log?.Warn("Could not determine final note timestamp; level-end events may not fire");

        _noteTracker.noteWasCutEvent += HandleNoteCut;
        _noteTracker.noteWasMissedEvent += HandleNoteMissed;
        _combo.comboBreakingEventHappenedEvent += HandleComboBreak;

        if (_wallSparks)
        {
            _wallSparks!.sparkleEffectDidStartEvent += HandleWallContact;
            _wallSparks.sparkleEffectDidEndEvent += HandleWallRelease;
        }

        _hpCounter.gameEnergyDidReach0Event += HandleDeath;
        _scoring.multiplierDidChangeEvent += HandleMultiplier;
        _accuracy.relativeScoreOrImmediateRankDidChangeEvent += HandleAccuracyTick;
        _combo.comboDidChangeEvent += HandleComboChange;

        _lightColorCallback = _beatmapCallbacks.AddBeatmapCallback<LightColorBeatmapEventData>(
        HandleLightColorEvent);

        _dispatcher.Fire(SaberEventType.OnLevelStart);
    }

    public void Dispose()
    {
        if (!IsBound) return;

        _noteTracker.noteWasCutEvent -= HandleNoteCut;
        _noteTracker.noteWasMissedEvent -= HandleNoteMissed;
        _combo.comboBreakingEventHappenedEvent -= HandleComboBreak;

        if (_wallSparks)
        {
            _wallSparks!.sparkleEffectDidStartEvent -= HandleWallContact;
            _wallSparks.sparkleEffectDidEndEvent -= HandleWallRelease;
        }

        _hpCounter.gameEnergyDidReach0Event -= HandleDeath;
        _scoring.multiplierDidChangeEvent -= HandleMultiplier;
        _accuracy.relativeScoreOrImmediateRankDidChangeEvent -= HandleAccuracyTick;
        _combo.comboDidChangeEvent -= HandleComboChange;

        if (_lightColorCallback is not null)
        _beatmapCallbacks.RemoveBeatmapCallback(_lightColorCallback);
    }

    private void HandleLightColorEvent(LightColorBeatmapEventData data)
    {
        if (data.brightness <= 0f) return;

        switch (data.colorType)
        {
            case EnvironmentColorType.Color0:
            _dispatcher?.Fire(SaberEventType.OnBlueLightOn);
            break;
            case EnvironmentColorType.Color1:
            _dispatcher?.Fire(SaberEventType.OnRedLightOn);
            break;
        }
    }

    private void HandleDeath() => _dispatcher?.Fire(SaberEventType.OnLevelFail);

    private void HandleComboBreak() => _dispatcher?.Fire(SaberEventType.OnComboBreak);

    private void HandleComboChange(int current) => _dispatcher?.FireComboChanged(current);

    private void HandleAccuracyTick()
    {
        var current = _accuracy.relativeScore;
        if (Math.Abs(_cachedAcc - current) < 0.001f) return;
        _cachedAcc = current;
        _dispatcher?.FireAccuracyChanged(current);
    }

    private void HandleMultiplier(int mult, float progress)
    {
        if (mult > _prevMultiplier)
        _dispatcher?.Fire(SaberEventType.MultiplierUp);
        _prevMultiplier = mult;
    }

    private void HandleWallContact(SaberType type)
    {
        if (type == _trackedHand)
        _dispatcher?.Fire(SaberEventType.SaberStartColliding);
    }

    private void HandleWallRelease(SaberType type)
    {
        if (type == _trackedHand)
        _dispatcher?.Fire(SaberEventType.SaberStopColliding);
    }

    private void HandleNoteCut(NoteController note, in NoteCutInfo info)
    {
        if (!_finalNoteTimestamp.HasValue) return;

        if (info.allIsOK && info.saberType == _trackedHand)
        _dispatcher?.Fire(SaberEventType.OnSlice);

        CheckForLevelEnd(note.noteData.time);
    }

    private void HandleNoteMissed(NoteController note)
    {
        if (!_finalNoteTimestamp.HasValue) return;
        CheckForLevelEnd(note.noteData.time);
    }

    private void CheckForLevelEnd(float noteTime)
    {
        if (!Mathf.Approximately(noteTime, _finalNoteTimestamp!.Value)) return;
        _finalNoteTimestamp = 0;
        _dispatcher?.Fire(SaberEventType.OnLevelEnded);
    }

    private float? GetLastNoteTime()
    {
        try
        {
            var lastNote = System.Linq.Enumerable.LastOrDefault(
            _beatmap.GetBeatmapDataItems<NoteData>(0),
            data => data.colorType != ColorType.None);
            return lastNote?.time;
        }
        catch (System.Exception ex)
        {
            _log?.Warn($"Failed to compute last note time: {ex.Message}");
            return null;
        }
    }
}