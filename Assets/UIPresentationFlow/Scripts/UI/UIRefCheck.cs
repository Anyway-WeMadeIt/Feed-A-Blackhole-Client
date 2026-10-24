using System;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

// Checks the promise a View prefab makes: for every value of its TRefs enum there is a child with that name,
// because UIBase<TRefs> finds children by name. Editor and development builds only; compiled out otherwise.
// UIManager.Register runs the full check for every registered View. A widget that is not registered and uses
// only some of its refs (one prefab, several layouts) calls WarnMissing with the refs it needs.
public static class UIRefCheck
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void WarnMissing(UIBase view)
    {
        if (view == null)
            return;

        Type refs = RefsTypeOf(view.GetType());
        if (refs == null)
            return;

        foreach (string name in Enum.GetNames(refs))
        {
            if (!view.TryGetRect(name, out _))
                Debug.LogWarning($"[{view.GetType().Name}] 자식 '{name}'이 없다.", view);
        }
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void WarnMissing<TRefs>(UIBase<TRefs> view, params TRefs[] refs) where TRefs : struct, Enum
    {
        if (view == null)
            return;

        foreach (TRefs key in refs)
        {
            if (!view.TryGetRect(key.ToString(), out _))
                Debug.LogWarning($"[{view.GetType().Name}] 자식 '{key}'이 없다.", view);
        }
    }

    // The TRefs of a View: the type argument of the UIBase<> it derives from. Null if it is not a UIBase<>.
    private static Type RefsTypeOf(Type type)
    {
        for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(UIBase<>))
                return current.GetGenericArguments()[0];
        }

        return null;
    }
}
