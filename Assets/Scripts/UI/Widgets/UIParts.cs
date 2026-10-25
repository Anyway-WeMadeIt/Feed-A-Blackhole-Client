using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 프리팹 없이 코드로 UI 부품을 만들 때 쓰는 도우미(은하 체력바, 엔딩 화면). 새 오브젝트는 부모의 레이어를 따르고 레이캐스트를 받지 않는다.
    internal static class UIParts
    {
        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static Image NewImage(string name, Transform parent, Color color)
        {
            Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = color;
            return image;
        }

        // 부모에 꽉 채운다(앵커 0 ~ 1, 여백 0).
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // 글자. 넘침은 자르지 않는다(Overflow). font가 없으면 TMP 기본 글꼴.
        public static TMP_Text NewText(string name, Transform parent, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment, TextWrappingModes wrapping)
        {
            TextMeshProUGUI text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.textWrappingMode = wrapping;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = alignment;
            text.fontSize = size;
            text.color = color;

            if (font != null)
                text.font = font;

            return text;
        }
    }
}
