// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using SaberSense.Core.Logging;
using System.Reflection;
using VRUIControls;

namespace SaberSense.GUI.Framework.Core;

internal static class VRRaycasterHelper
{
    private static readonly FieldInfo? PhysicsRaycasterField;

    static VRRaycasterHelper()
    {
        PhysicsRaycasterField = typeof(VRGraphicRaycaster).GetField(
        "_physicsRaycaster", BindingFlags.NonPublic | BindingFlags.Instance);
        if (PhysicsRaycasterField is null)
        ModLogger.ForSource("VRRaycaster").Warn("VRGraphicRaycaster._physicsRaycaster field not found - VR pointer may not work.");
    }

    public static void SetPhysicsRaycaster(VRGraphicRaycaster target, object physicsRaycaster)
    {
        PhysicsRaycasterField?.SetValue(target, physicsRaycaster);
    }

    public static void CopyPhysicsRaycaster(VRGraphicRaycaster source, VRGraphicRaycaster target)
    {
        if (PhysicsRaycasterField is null || source == null || target == null) return;
        PhysicsRaycasterField.SetValue(target, PhysicsRaycasterField.GetValue(source));
    }
}