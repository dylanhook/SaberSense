// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace SaberSense.GUI.Framework.Core;

internal sealed class UIPropertyBinding : IDisposable
{
    private readonly INotifyPropertyChanged _source;
    private readonly PropertyChangedEventHandler _handler;

    public UIPropertyBinding(INotifyPropertyChanged source, PropertyChangedEventHandler handler)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _source.PropertyChanged += _handler;
    }

    public void Dispose() => _source.PropertyChanged -= _handler;
}

internal sealed class BindingScope : IDisposable
{
    private readonly List<UIPropertyBinding> _bindings = [];

    public UIPropertyBinding Add(INotifyPropertyChanged source, PropertyChangedEventHandler handler)
    {
        var binding = new UIPropertyBinding(source, handler);
        _bindings.Add(binding);
        return binding;
    }

    public void Add(UIPropertyBinding binding) => _bindings.Add(binding);

    public void Dispose()
    {
        foreach (var b in _bindings) b.Dispose();
        _bindings.Clear();
    }
}