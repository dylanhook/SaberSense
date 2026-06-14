// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;

namespace SaberSense.App;

public enum SessionPhase
{
    Uninitialized,

    Idle,

    LoadingConfig,

    Editing,

    TransitionToGameplay,

    InGameplay,
}

internal sealed class SessionController
{
    public SessionPhase Phase { get; private set; } = SessionPhase.Uninitialized;

    private readonly IModLogger _log;

    public SessionController(IModLogger log)
    {
        _log = log.ForSource(nameof(SessionController));
    }

    public void TransitionTo(SessionPhase next)
    {
        if (!IsLegalTransition(Phase, next))
        {
            _log.Warn($"Illegal transition {Phase} -> {next} -- allowing but logging for debugging");
        }

        var prev = Phase;
        Phase = next;
        _log.Info($"{prev} -> {next}");
    }

    public bool CanPersist => Phase is SessionPhase.Editing
    or SessionPhase.Idle
    or SessionPhase.TransitionToGameplay;

    public bool ShouldUpdatePreview => Phase is SessionPhase.Editing;

    public bool IsEditing => Phase is SessionPhase.Editing;

    private static bool IsLegalTransition(SessionPhase from, SessionPhase to)
    {
        return (from, to) switch
        {
            (SessionPhase.Uninitialized, SessionPhase.Idle) => true,

            (SessionPhase.Idle, SessionPhase.Editing) => true,
            (SessionPhase.Editing, SessionPhase.Idle) => true,

            (SessionPhase.Idle, SessionPhase.LoadingConfig) => true,
            (SessionPhase.Editing, SessionPhase.LoadingConfig) => true,
            (SessionPhase.LoadingConfig, SessionPhase.Idle) => true,
            (SessionPhase.LoadingConfig, SessionPhase.Editing) => true,

            (SessionPhase.Idle, SessionPhase.TransitionToGameplay) => true,
            (SessionPhase.Editing, SessionPhase.TransitionToGameplay) => true,
            (SessionPhase.TransitionToGameplay, SessionPhase.InGameplay) => true,
            (SessionPhase.InGameplay, SessionPhase.Idle) => true,
            (SessionPhase.InGameplay, SessionPhase.Editing) => true,

            _ when from == to => true,

            _ => false,
        };
    }
}