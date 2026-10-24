using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public abstract class UIBase : MonoBehaviour, IUIPresentationRefProvider, IUIStyledElementsOwner
{
    private bool _initialized;
    private UIStyledElements _styled;

    // Texts and images this View styles by code (cloned rows, cells made at runtime). Same theme as the catalog entries.
    public UIStyledElements Styled => _styled ??= new UIStyledElements();

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

    // A presentation was applied to this View (shown, context changed, display changed). Catalog entries and
    // Styled elements are already themed; refresh here what the View draws in code from the theme
    // (materials, computed colors, texts it owns outright). theme is also the value to keep for later drawing.
    protected virtual void OnPresented(in UIAppliedTheme theme) { }

    internal void NotifyPresented(in UIAppliedTheme theme) => OnPresented(theme);

    // 버튼의 포인터 사건 하나에 처리를 묶는다(UIEvents.Bind). 뷰가 자기 버튼을 묶을 때 쓴다.
    protected static void BindEvent(
        Button button,
        Action<PointerEventData> action,
        ETouchEvent type = ETouchEvent.Click)
        => UIEvents.Bind(button, action, type);

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
