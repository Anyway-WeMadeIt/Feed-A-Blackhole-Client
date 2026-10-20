using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 테마(다크 모드)를 ImageBindings가 닿지 않는 그림에 넣는다(설정 행의 토글·슬라이더, 카드, 화면 바탕색, 구분선 등).
    // - 그림(Sprite): 프리팹 그림을 기준으로 기억하고 UISpriteSet에서 지금 테마의 짝으로 바꾼다. 같은 버튼(Sprite Swap)의 상태 그림도 함께.
    // - 색(Color): 단색 Image의 색을 팔레트의 역할 색으로 바꾼다. 알파는 프리팹 값을 지킨다(구분선 25% 등).
    // 처음 넣을 때 값을 기준으로 기억하므로 다시 넣어도 겹치지 않고, 팔레트·그림 묶음이 없으면 기준으로 돌아간다.
    // 복제해 만든 그림(행, 선택지)은 지울 때 Clear로 함께 비운다.
    internal sealed class ThemedImages
    {
        private readonly Dictionary<Image, Entry> _entries = new();
        private readonly List<Image> _gone = new();

        public UIStyle Style { get; private set; } = UIStyle.Default;

        // 그림을 테마 짝으로 바꾼다. 그림이 없는 Image는 넣지 않는다.
        public void AddSprite(Image image)
        {
            if (image == null || image.sprite == null || _entries.ContainsKey(image))
                return;

            Selectable selectable = UIImageThemePatch.SelectableOf(image);
            var entry = new Entry(image.sprite, null, selectable, selectable != null ? selectable.spriteState : default);
            _entries.Add(image, entry);
            Apply(image, entry);
        }

        // root 아래 모든 Image의 그림을 테마 짝으로 바꾼다(틀을 복제한 행·카드).
        public void AddSprites(Component root)
        {
            if (root == null)
                return;

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
                AddSprite(image);
        }

        // 단색 Image의 색을 역할 색으로 바꾼다.
        public void AddColor(Image image, UIColorRole role)
        {
            if (image == null || _entries.ContainsKey(image))
                return;

            var entry = new Entry(null, role, null, default) { BaseColor = image.color };
            _entries.Add(image, entry);
            Apply(image, entry);
        }

        public void SetStyle(in UIStyle style)
        {
            Style = style;

            foreach (KeyValuePair<Image, Entry> pair in _entries)
            {
                if (pair.Key == null)
                    _gone.Add(pair.Key);
                else
                    Apply(pair.Key, pair.Value);
            }

            foreach (Image image in _gone)
                _entries.Remove(image);

            _gone.Clear();
        }

        public void Clear() => _entries.Clear();

        private void Apply(Image image, Entry entry)
        {
            if (entry.BaseSprite != null)
            {
                image.sprite = Style.SpriteFor(entry.BaseSprite);

                if (entry.Selectable != null && entry.Selectable.transition == Selectable.Transition.SpriteSwap)
                {
                    SpriteState state = entry.BaseState;
                    state.highlightedSprite = Style.SpriteFor(state.highlightedSprite);
                    state.pressedSprite = Style.SpriteFor(state.pressedSprite);
                    state.selectedSprite = Style.SpriteFor(state.selectedSprite);
                    state.disabledSprite = Style.SpriteFor(state.disabledSprite);
                    entry.Selectable.spriteState = state;
                }
            }

            if (entry.ColorRole is UIColorRole role)
            {
                Color? themed = Style.ColorFor(role);
                Color color = themed ?? entry.BaseColor;
                color.a = entry.BaseColor.a;
                image.color = color;
            }
        }

        private sealed class Entry
        {
            public readonly Sprite BaseSprite;
            public readonly UIColorRole? ColorRole;
            public readonly Selectable Selectable;
            public readonly SpriteState BaseState;
            public Color BaseColor;

            public Entry(Sprite baseSprite, UIColorRole? colorRole, Selectable selectable, SpriteState baseState)
            {
                BaseSprite = baseSprite;
                ColorRole = colorRole;
                Selectable = selectable;
                BaseState = baseState;
            }
        }
    }
}
