using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public abstract class UIBase : MonoBehaviour, IUIPresentationRefProvider
{
    private bool _initialized;

    protected virtual void Awake()
    {
        EnsureInitialized();
    }

    public void EnsureInitialized()
    {
        if (_initialized)
            return;

        _initialized = true;
        PreInitialize();
        OnInitialize();
    }

    protected virtual void PreInitialize() { }
    protected virtual void OnInitialize() { }

    protected static void BindEvent(
        Button button,
        Action<PointerEventData> action,
        ETouchEvent type = ETouchEvent.Click)
    {
        if (button == null || action == null)
            return;

        BindEvent(button.gameObject, action, type);
    }

    private static void BindEvent(
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

    public abstract bool TryGetRect(
        string refId,
        out RectTransform rect);

    public abstract bool TryGetText(
        string refId,
        out TMP_Text text);

    public abstract bool TryGetImage(
        string refId,
        out Image image);
}

public abstract class UIBase<TRefs> : UIBase
    where TRefs : struct, Enum
{
    protected sealed class RefView
    {
        private readonly UIBase<TRefs> _ui;

        internal RefView(UIBase<TRefs> ui)
        {
            _ui = ui;
        }

        public RectTransform Rect(TRefs key) => _ui.GetRectCached(key);
        public TMP_Text Text(TRefs key) => _ui.GetCached<TMP_Text>(key);
        public Image Image(TRefs key) => _ui.GetCached<Image>(key);
        public Graphic Graphic(TRefs key) => _ui.GetCached<Graphic>(key);
        public Button Button(TRefs key) => _ui.GetCached<Button>(key);
        public CanvasGroup CanvasGroup(TRefs key) => _ui.GetCached<CanvasGroup>(key);
        public T Component<T>(TRefs key) where T : Component => _ui.GetCached<T>(key);
        public T Widget<T>(TRefs key) where T : UIBase => _ui.GetCached<T>(key);
    }

    private GameObject[] _objects;
    private readonly Dictionary<(int index, Type type), Component> _componentCache = new();
    private bool _refsBuilt;

    protected RefView View { get; private set; }

    protected override void PreInitialize()
    {
        if (_refsBuilt)
            return;

        BindObjects();
        View = new RefView(this);
        _refsBuilt = true;
    }

    protected virtual void OnDestroy()
    {
        _componentCache.Clear();
    }

    public override bool TryGetRect(string refId, out RectTransform rect)
    {
        rect = null;

        if (!UIRefKeyCache<TRefs>.TryGetKey(refId, out TRefs key))
            return false;

        EnsureInitialized();
        rect = GetRectCached(key);
        return rect != null;
    }

    public override bool TryGetText(string refId, out TMP_Text text)
    {
        text = null;

        if (!UIRefKeyCache<TRefs>.TryGetKey(refId, out TRefs key))
            return false;

        EnsureInitialized();
        text = GetCached<TMP_Text>(key);
        return text != null;
    }

    public override bool TryGetImage(string refId, out Image image)
    {
        image = null;

        if (!UIRefKeyCache<TRefs>.TryGetKey(refId, out TRefs key))
            return false;

        EnsureInitialized();
        image = GetCached<Image>(key);
        return image != null;
    }

    private void BindObjects()
    {
        string[] refNames = Enum.GetNames(typeof(TRefs));
        _objects = new GameObject[refNames.Length];

        for (int i = 0; i < refNames.Length; i++)
            _objects[i] = FindChildGameObjectRecursive(gameObject, refNames[i]);
    }

    private GameObject TryGetBoundGameObject(TRefs key)
    {
        if (!_refsBuilt || _objects == null)
            return null;

        int index = Convert.ToInt32(key);
        if ((uint)index >= (uint)_objects.Length)
        {
            Debug.LogWarning(
                $"[UIBase] Ref index out of range. key={key}, index={index}, count={_objects.Length}",
                this);
            return null;
        }

        return _objects[index];
    }

    private T GetCached<T>(TRefs key) where T : Component
    {
        int index = Convert.ToInt32(key);
        var cacheKey = (index, typeof(T));

        if (_componentCache.TryGetValue(cacheKey, out Component cached) && cached != null)
            return (T)cached;

        GameObject go = TryGetBoundGameObject(key);
        if (go == null || !go.TryGetComponent(out T component))
            return null;

        _componentCache[cacheKey] = component;
        return component;
    }

    private RectTransform GetRectCached(TRefs key)
    {
        RectTransform rect = GetCached<RectTransform>(key);
        if (rect != null)
            return rect;

        Graphic graphic = GetCached<Graphic>(key);
        if (graphic == null)
            return null;

        rect = graphic.rectTransform;
        if (rect != null)
            _componentCache[(Convert.ToInt32(key), typeof(RectTransform))] = rect;

        return rect;
    }

    private static GameObject FindChildGameObjectRecursive(
        GameObject root,
        string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            if (child.name == name)
                return child.gameObject;
        }

        return null;
    }
}
