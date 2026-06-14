// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SaberSense.GUI.Framework.Core;

public abstract class BindableSettings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _suppressNotifications;

    protected void Notify([CallerMemberName] string? prop = null)
    {
        if (!_suppressNotifications)
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    protected void Notify(params string[] props)
    {
        foreach (var p in props) Notify(p);
    }

    public void BatchUpdate(System.Action action)
    {
        _suppressNotifications = true;
        try { action(); }
        finally
        {
            _suppressNotifications = false;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? prop = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Notify(prop);
        return true;
    }

    protected bool SetClamped(ref float field, float value, float min, float max, [CallerMemberName] string? prop = null)
    {
        var clamped = Mathf.Clamp(value, min, max);
        if (Mathf.Approximately(field, clamped)) return false;
        field = clamped;
        Notify(prop);
        return true;
    }

    protected bool SetClamped(ref int field, int value, int min, int max, [CallerMemberName] string? prop = null)
    {
        var clamped = Mathf.Clamp(value, min, max);
        if (field == clamped) return false;
        field = clamped;
        Notify(prop);
        return true;
    }

    internal void RaisePropertyChanged([CallerMemberName] string? prop = null) => Notify(prop);
}