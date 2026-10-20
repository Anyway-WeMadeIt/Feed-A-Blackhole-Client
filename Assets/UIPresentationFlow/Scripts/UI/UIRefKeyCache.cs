using System;
using System.Collections.Generic;

public static class UIRefKeyCache<TRefs>
    where TRefs : struct, Enum
{
    private static readonly Dictionary<string, TRefs> KeysById;

    static UIRefKeyCache()
    {
        string[] refIds = Enum.GetNames(typeof(TRefs));

        KeysById = new Dictionary<string, TRefs>(
            refIds.Length,
            StringComparer.Ordinal);

        foreach (string refId in refIds)
        {
            if (Enum.TryParse(refId, out TRefs key))
                KeysById.Add(refId, key);
        }
    }

    public static bool TryGetKey(string refId, out TRefs key)
    {
        if (string.IsNullOrEmpty(refId))
        {
            key = default;
            return false;
        }

        return KeysById.TryGetValue(refId, out key);
    }
}
