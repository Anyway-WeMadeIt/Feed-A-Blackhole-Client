using System;
using System.Collections.Generic;

// Pins the data key of a View class. Use it to keep a class's bindings when the class is renamed.
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class UIViewKeyAttribute : Attribute
{
    public string Key { get; }

    public UIViewKeyAttribute(string key)
    {
        Key = key;
    }
}

// The key that names a View in data (UITextBindingSet.viewId, UIImageBindingSet.viewId).
// Default: the concrete type's short name, so moving a class to another namespace or folder keeps its assets.
// [UIViewKey("Name")] on the class overrides it. Two registered View types must not share a key
// (UIManager.Register and the editor check report it).
public static class UIViewKey
{
    private static readonly Dictionary<Type, string> Cache = new();

    public static string Of(UIBase view) => view != null ? Of(view.GetType()) : null;

    public static string Of(Type viewType)
    {
        if (viewType == null)
            return null;

        if (Cache.TryGetValue(viewType, out string key))
            return key;

        object[] attributes = viewType.GetCustomAttributes(typeof(UIViewKeyAttribute), inherit: false);
        key = attributes.Length > 0 && !string.IsNullOrWhiteSpace(((UIViewKeyAttribute)attributes[0]).Key)
            ? ((UIViewKeyAttribute)attributes[0]).Key.Trim()
            : viewType.Name;

        Cache[viewType] = key;
        return key;
    }
}
