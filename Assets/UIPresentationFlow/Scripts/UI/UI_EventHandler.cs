using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public enum ETouchEvent
{
    PointerUp,
    PointerDown,
    Click,
    LongPressed,
    BeginDrag,
    Drag,
    EndDrag,
    PointerEnter,
    PointerExit,
}

// Adapts Unity EventSystem callbacks into consistent UI interactions.
// Drag and long press suppress click, and starting a drag cancels long press.
//
// Drag confirmation follows EventSystem.pixelDragThreshold.
//
// Pointer enter and exit are always paired: exit is sent at most once per enter,
// and a pointer that is still inside when this object is disabled receives exit then.
public sealed class UI_EventHandler : MonoBehaviour,
    IPointerClickHandler,
    IPointerDownHandler,
    IPointerUpHandler,
    IDragHandler,
    IBeginDragHandler,
    IEndDragHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    public Action<PointerEventData> OnClickHandler;
    public Action<PointerEventData> OnPointerDownHandler;
    public Action<PointerEventData> OnPointerUpHandler;
    public Action<PointerEventData> OnDragHandler;
    public Action<PointerEventData> OnBeginDragHandler;
    public Action<PointerEventData> OnEndDragHandler;
    public Action<PointerEventData> OnLongPressHandler;
    public Action<PointerEventData> OnPointerEnterHandler;
    public Action<PointerEventData> OnPointerExitHandler;

    [Header("Long Press")]
    [SerializeField, Min(0f)]
    private float _longPressDuration = 1.0f;

    private bool _isDragging;
    private bool _isDragConfirmed;
    private bool _isLongPressTriggered;
    private bool _isPointerInside;

    private PointerEventData _cachedEventData;
    private PointerEventData _enterEventData;
    private Coroutine _longPressCoroutine;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_isDragConfirmed || _isLongPressTriggered)
            return;

        OnClickHandler?.Invoke(eventData);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        StopLongPressCheck();

        _isDragging = false;
        _isDragConfirmed = false;
        _isLongPressTriggered = false;

        _cachedEventData = eventData;

        OnPointerDownHandler?.Invoke(eventData);

        if (OnLongPressHandler != null)
            _longPressCoroutine = StartCoroutine(CheckLongPress());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        StopLongPressCheck();

        _cachedEventData = null;

        OnPointerUpHandler?.Invoke(eventData);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _isDragging = true;
        _isDragConfirmed = true;
        _cachedEventData = eventData;

        StopLongPressCheck();

        OnBeginDragHandler?.Invoke(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        _cachedEventData = eventData;

        if (!_isDragging || !_isDragConfirmed)
            return;

        OnDragHandler?.Invoke(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_isDragConfirmed)
            OnEndDragHandler?.Invoke(eventData);

        _isDragging = false;
        _isDragConfirmed = false;
        _isLongPressTriggered = false;
        _cachedEventData = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_isPointerInside)
            return;

        _isPointerInside = true;
        _enterEventData = eventData;

        OnPointerEnterHandler?.Invoke(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ExitIfInside(eventData);
    }

    // A disabled object receives no exit from the EventSystem, so disabling sends it here.
    private void ExitIfInside(PointerEventData eventData)
    {
        if (!_isPointerInside)
            return;

        _isPointerInside = false;
        _enterEventData = null;

        OnPointerExitHandler?.Invoke(eventData);
    }

    private IEnumerator CheckLongPress()
    {
        yield return new WaitForSecondsRealtime(_longPressDuration);

        if (_cachedEventData != null &&
            !_isLongPressTriggered &&
            !_isDragConfirmed)
        {
            _isLongPressTriggered = true;
            OnLongPressHandler?.Invoke(_cachedEventData);
        }

        _longPressCoroutine = null;
    }

    private void StopLongPressCheck()
    {
        if (_longPressCoroutine == null)
            return;

        StopCoroutine(_longPressCoroutine);
        _longPressCoroutine = null;
    }

    private void OnDisable()
    {
        ExitIfInside(_enterEventData);
        StopLongPressCheck();

        _isDragging = false;
        _isDragConfirmed = false;
        _isLongPressTriggered = false;
        _cachedEventData = null;
    }
}