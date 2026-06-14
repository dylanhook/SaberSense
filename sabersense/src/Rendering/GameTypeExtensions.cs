// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;

namespace SaberSense.Rendering;

public static class GameTypeExtensions
{
    public static GameObject CreateGameObject(this Transform parent, string name, bool keepWorldPos = false)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent, keepWorldPos);
        return child;
    }

    public static GameObject CreateGameObject(this GameObject parent, string name, bool keepWorldPos = false) =>
    parent.transform.CreateGameObject(name, keepWorldPos);

    public static void TryDestroy(this Object target)
    {
        if (target) Object.Destroy(target);
    }

    public static void TryDestroyImmediate(this Object target)
    {
        if (target) Object.DestroyImmediate(target);
    }

    public static float MeasureSpan(Transform first, Transform second) =>
    first && second ? Mathf.Abs(first.localPosition.z - second.localPosition.z) : 0f;

    public static float GetWidth(this SaberTrailMarker trail) =>
    trail ? MeasureSpan(trail.PointEnd!, trail.PointStart!) : 0f;

    public static float GetLastNoteTime(this BeatmapData map)
    {
        var last = 0f;
        foreach (var note in map.GetBeatmapDataItems<NoteData>(0))
        {
            if (note.colorType is ColorType.None) continue;
            if (note.time > last) last = note.time;
        }
        return last;
    }
}