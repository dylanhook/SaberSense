// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.App;
using Zenject;

namespace SaberSense.Gameplay;

internal sealed class GameplaySessionInitializer : IInitializable
{
    private readonly SessionController _session;

    public GameplaySessionInitializer(SessionController session) => _session = session;

    public void Initialize() => _session.TransitionTo(SessionPhase.InGameplay);
}