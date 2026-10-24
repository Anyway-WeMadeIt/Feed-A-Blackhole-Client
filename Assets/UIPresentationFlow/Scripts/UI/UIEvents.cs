using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 버튼의 포인터 사건에 처리를 묶는다. 버튼에 UI_EventHandler를 붙이고(없으면 만든다) 사건 종류별 핸들러에 더한다.
// 뷰 안에서는 UIBase.BindEvent가 이것을 부르고, 뷰 밖의 도우미(버튼 연출·소리를 한 번에 묶는 것 등)는 직접 부른다.
public static class UIEvents
{
    // 버튼의 포인터 사건 하나에 처리를 묶는다. 같은 처리를 두 번 묶어도 한 번만 불린다.
    public static void Bind(
        Button button,
        Action<PointerEventData> action,
        ETouchEvent type = ETouchEvent.Click)
    {
        if (button == null || action == null)
            return;

        Bind(button.gameObject, action, type);
    }

    private static void Bind(
        GameObject target,
        Action<PointerEventData> action,
        ETouchEvent type)
    {
        if (target == null || action == null)
            return;

        UI_EventHandler eventHandler =
            GetOrAddComponent<UI_EventHandler>(target);

        switch (type)
        {
            case ETouchEvent.Click:
                eventHandler.OnClickHandler -= action;
                eventHandler.OnClickHandler += action;
                break;

            case ETouchEvent.PointerDown:
                eventHandler.OnPointerDownHandler -= action;
                eventHandler.OnPointerDownHandler += action;
                break;

            case ETouchEvent.PointerUp:
                eventHandler.OnPointerUpHandler -= action;
                eventHandler.OnPointerUpHandler += action;
                break;

            case ETouchEvent.LongPressed:
                eventHandler.OnLongPressHandler -= action;
                eventHandler.OnLongPressHandler += action;
                break;

            case ETouchEvent.BeginDrag:
                eventHandler.OnBeginDragHandler -= action;
                eventHandler.OnBeginDragHandler += action;
                break;

            case ETouchEvent.Drag:
                eventHandler.OnDragHandler -= action;
                eventHandler.OnDragHandler += action;
                break;

            case ETouchEvent.EndDrag:
                eventHandler.OnEndDragHandler -= action;
                eventHandler.OnEndDragHandler += action;
                break;

            case ETouchEvent.PointerEnter:
                eventHandler.OnPointerEnterHandler -= action;
                eventHandler.OnPointerEnterHandler += action;
                break;

            case ETouchEvent.PointerExit:
                eventHandler.OnPointerExitHandler -= action;
                eventHandler.OnPointerExitHandler += action;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, null);
        }
    }

    private static T GetOrAddComponent<T>(GameObject target)
        where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
            component = target.AddComponent<T>();

        return component;
    }
}
