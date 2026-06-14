// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

namespace SaberSense.Input;

internal sealed class PauseKeyInputBehavior : XRButtonPoller<PauseKeyInputBehavior>
{
    private const string ObjectName = "SaberSense_PauseKeyTracker";

    public static bool IsPressed => Instance != null && Instance.IsButtonPressed;

    public static bool IsPressedDown => Instance != null && Instance.IsButtonPressedDown;

    public static VrButtonBinding Binding
    {
        get => Instance?.ButtonBinding ?? VrButtonBinding.None;
        set { if (Instance != null) Instance.ButtonBinding = value; }
    }

    public static void Initialize() => EnsureInstance(ObjectName);

    private void Update()
    {
        if (ButtonBinding is VrButtonBinding.None)
        {
            CommitState(false);
            return;
        }

        CommitState(PollVRButton());
    }
}