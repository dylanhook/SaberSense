// Copyright (c) 2026 dylanhook. All rights reserved.
// Licensed under the SaberSense Proprietary License. See LICENSE file in the project root.

using UnityEngine;
using UnityEngine.EventSystems;

namespace SaberSense.GUI.Framework.Core;

internal sealed class PreviewClickDrag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private const float MaxDeltaSqr = 6400f;
    public bool Held { get; private set; }
    public bool JustReleased { get; private set; }
    private Vector2 _delta;
    private Vector2 _lastLocalPosition;
    private RectTransform _rectTransform = null!;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    public Vector2 ConsumeDelta() { Vector2 d = _delta; _delta = Vector2.zero; return d; }

    public void ClearRelease() => JustReleased = false;

    public void OnPointerDown(PointerEventData eventData)
    {
        Held = true;
        JustReleased = false;
        _delta = Vector2.zero;

        if (eventData is not null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, eventData.pressEventCamera, out _lastLocalPosition);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (Held)
        {
            Held = false;
            JustReleased = true;
        }
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        if (eventData is not null)
        eventData.useDragThreshold = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        Held = true;
        if (eventData is not null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, eventData.pressEventCamera, out _lastLocalPosition);
            eventData.Use();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!Held || eventData is null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPosition))
        {
            Vector2 currentDelta = localPosition - _lastLocalPosition;

            if (currentDelta.sqrMagnitude < MaxDeltaSqr)
            {
                _delta += currentDelta;
            }

            _lastLocalPosition = localPosition;
        }
        eventData.Use();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (Held)
        {
            Held = false;
            JustReleased = true;
        }
        if (eventData is not null)
        eventData.Use();
    }
}