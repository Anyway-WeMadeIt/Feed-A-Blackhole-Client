using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IUIPresentationRefProvider
{
    bool TryGetRect(string refId, out RectTransform rect);
    bool TryGetText(string refId, out TMP_Text text);
    bool TryGetImage(string refId, out Image image);
}
