// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using UnityEngine;

namespace SaberSense.Core.Utilities;

internal sealed class ShaderRegistry(IModLogger log)
{
    public Shader InternalColored { get; } = Resolve("Hidden/Internal-Colored", log);

    public Shader SpritesDefault { get; } = Resolve("Sprites/Default", log);

    public Shader LegacyParticlesAlpha { get; } = Resolve("Legacy Shaders/Particles/Alpha Blended", log);

    public Shader UIDefault { get; } = Resolve("UI/Default", log);

    public static Shader? FirstAvailable(params Shader[] candidates)
    {
        foreach (var s in candidates)
        if (s != null) return s;
        return null;
    }

    public Shader ParticleWithFallback =>
    FirstAvailable(LegacyParticlesAlpha, UIDefault)!;

    public Shader UnlitWithFallback => UIDefault;

    public Shader UINoGlowWithFallback => UIDefault;

    private static Shader Resolve(string name, IModLogger log)
    {
        var shader = Shader.Find(name);
        if (shader == null)
        log?.Warn($"Shader '{name}' not found - features using it will fall back.");
        return shader!;
    }
}