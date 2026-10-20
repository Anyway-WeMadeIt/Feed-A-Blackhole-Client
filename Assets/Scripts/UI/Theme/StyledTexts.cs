using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BlackHole.Unity
{
    // 글자 스타일(크기 배율, 언어별 글꼴, 테마 색)을 TextBindings가 닿지 않는 글자에 넣는다(설정 행, 결산 통계 칸, 안내 글자 등).
    // - 글자마다 역할(Title/Body/Caption)을 함께 받는다. 글꼴은 글꼴 묶음(UIFontSet)에서 "지금 언어 × 역할"로, 색은 팔레트에서 "지금 테마 × 역할"로 고른다.
    // - 처음 넣을 때의 크기·글꼴·색을 기준으로 기억한다. 크기는 "기준 × 배율"이라 다시 넣어도 배율이 겹치지 않고,
    //   글꼴 묶음·팔레트에 맞는 값이 없으면 기준으로 되돌린다. 색은 알파를 프리팹 값으로 지킨다.
    // - 자동 크기(Auto Size) 글자는 크기 대신 최소·최대 크기를 같은 배율로 바꾼다.
    // - 틀(template)은 넣지 않는다. 복제본이 틀의 원래 크기·글꼴을 물려받아 기준이 된다.
    // 복제해 만든 글자(행, 선택지)는 지울 때 Clear로 함께 비운다.
    internal sealed class StyledTexts
    {
        private readonly Dictionary<TMP_Text, Base> _bases = new();
        private readonly List<TMP_Text> _gone = new();

        public UIStyle Style { get; private set; } = UIStyle.Default;

        public void Add(TMP_Text text, UITextRole role)
        {
            if (text == null || _bases.ContainsKey(text))
                return;

            var source = new Base(role, text.font, text.color, text.fontSize, text.fontSizeMin, text.fontSizeMax);
            _bases.Add(text, source);
            Apply(text, source);
        }

        public void SetStyle(in UIStyle style)
        {
            Style = style;

            foreach (KeyValuePair<TMP_Text, Base> pair in _bases)
            {
                // 지워진 글자(Unity의 가짜 null)는 건너뛰고 뒤에서 뺀다.
                if (pair.Key == null)
                    _gone.Add(pair.Key);
                else
                    Apply(pair.Key, pair.Value);
            }

            foreach (TMP_Text text in _gone)
                _bases.Remove(text);

            _gone.Clear();
        }

        public void Clear() => _bases.Clear();

        private void Apply(TMP_Text text, Base source)
        {
            TMP_FontAsset font = Style.FontFor(source.Role);
            text.font = font != null ? font : source.Font;

            Color color = Style.TextColorFor(source.Role) ?? source.Color;
            color.a = source.Color.a;
            text.color = color;

            if (text.enableAutoSizing)
            {
                text.fontSizeMin = source.Min * Style.Scale;
                text.fontSizeMax = source.Max * Style.Scale;
            }
            else
            {
                text.fontSize = source.Size * Style.Scale;
            }
        }

        private readonly struct Base
        {
            public readonly UITextRole Role;
            public readonly TMP_FontAsset Font;
            public readonly Color Color;
            public readonly float Size;
            public readonly float Min;
            public readonly float Max;

            public Base(UITextRole role, TMP_FontAsset font, Color color, float size, float min, float max)
            {
                Role = role;
                Font = font;
                Color = color;
                Size = size;
                Min = min;
                Max = max;
            }
        }
    }
}
