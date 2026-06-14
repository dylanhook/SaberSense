// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using System.Threading.Tasks;

namespace SaberSense.Core;

internal interface IAsyncLoadable
{
    Task? CurrentTask { get; }
}