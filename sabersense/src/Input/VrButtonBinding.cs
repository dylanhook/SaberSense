// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEngine.XR;

namespace SaberSense.Input;

internal enum VrButtonBinding
{
    None = 0,
    LeftTrigger = 1,
    RightTrigger = 2,
    LeftGrip = 3,
    RightGrip = 4,
    LeftPrimary = 5,
    RightPrimary = 6,
    LeftSecondary = 7,
    RightSecondary = 8,
    LeftStick = 9,
    RightStick = 10,
}

internal readonly struct VrButtonDescriptor
{
    public VrButtonDescriptor(InputFeatureUsage<bool> usage, XRNode node, string displayName)
    {
        Usage = usage;
        Node = node;
        DisplayName = displayName;
    }

    public InputFeatureUsage<bool> Usage { get; }

    public XRNode Node { get; }

    public string DisplayName { get; }
}

internal static class VrButtonBindings
{
    private static readonly VrButtonDescriptor[] _table =
    {
        new(default, XRNode.LeftHand, "None"),
        new(CommonUsages.triggerButton, XRNode.LeftHand, "L-Trigger"),
        new(CommonUsages.triggerButton, XRNode.RightHand, "R-Trigger"),
        new(CommonUsages.gripButton, XRNode.LeftHand, "L-Grip"),
        new(CommonUsages.gripButton, XRNode.RightHand, "R-Grip"),
        new(CommonUsages.primaryButton, XRNode.LeftHand, "L-Primary"),
        new(CommonUsages.primaryButton, XRNode.RightHand, "R-Primary"),
        new(CommonUsages.secondaryButton, XRNode.LeftHand, "L-Secondary"),
        new(CommonUsages.secondaryButton, XRNode.RightHand, "R-Secondary"),
        new(CommonUsages.primary2DAxisClick, XRNode.LeftHand, "L-Stick Click"),
        new(CommonUsages.primary2DAxisClick, XRNode.RightHand, "R-Stick Click"),
    };

    public static VrButtonDescriptor Get(VrButtonBinding binding) => _table[(int)binding];

    public static IEnumerable<VrButtonBinding> Pressable
    {
        get
        {
            for (int i = 1; i < _table.Length; i++)
            yield return (VrButtonBinding)i;
        }
    }

    public static string DisplayName(VrButtonBinding binding)
    {
        int index = (int)binding;
        return index >= 0 && index < _table.Length ? _table[index].DisplayName : "None";
    }
}