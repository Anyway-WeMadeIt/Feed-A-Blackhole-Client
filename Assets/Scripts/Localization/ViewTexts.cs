using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BlackHole.Unity
{
    // 등록된 화면의 프리팹 글자를 지금 언어로 바꾼다. 키는 {화면 타입 이름}.{Ref 이름}(예: PausePanel.Title_Text).
    // - 표에 키가 있는 글자만 바꾼다. 코드가 쓰는 글자(Gold_Text 등)는 표에 넣지 않는다 — 넣으면 언어를 바꿀 때 덮어쓴다.
    // - 시작할 때 한 번, 언어가 바뀔 때마다 등록된 화면 전체(닫힌 화면 포함)에 넣는다.
    //   프리팹 글자는 화면을 열고 닫아도 바뀌지 않으므로 열 때마다 넣지 않는다.
    // - 처음 바꿀 때 프리팹 글자를 기억해 둔다. 지금 언어와 영어 칸이 모두 비면 그 글자로 되돌린다.
    // - 코드가 글자를 만드는 화면(ILocalizedView)은 프리팹 글자를 넣은 뒤 Relocalize로 제 글자를 다시 쓰게 한다.
    // Ref 이름은 TextBinding(글자 역할)과 같은 주소를 쓰지만, 이쪽은 글자 내용만 다룬다(글꼴·크기·색은 Presentation).
    internal sealed class ViewTexts : IDisposable
    {
        private readonly IReadOnlyList<UIBase> _views;
        private readonly Localizer _localizer;
        private readonly Dictionary<TMP_Text, string> _authored = new();
        private readonly HashSet<string> _missing = new(StringComparer.Ordinal);

        public ViewTexts(IReadOnlyList<UIBase> views, Localizer localizer)
        {
            _views = views ?? Array.Empty<UIBase>();
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));

            _localizer.Changed += Apply;
            Apply();
        }

        public void Dispose() => _localizer.Changed -= Apply;

        private void Apply()
        {
            foreach (UIBase view in _views)
            {
                if (view == null)
                    continue;

                Apply(view);

                if (view is ILocalizedView localized)
                    localized.Relocalize(_localizer);
            }
        }

        private void Apply(UIBase view)
        {
            string prefix = view.GetType().Name + ".";

            foreach (string key in _localizer.Keys)
            {
                if (!key.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                string refId = key.Substring(prefix.Length);

                if (!view.TryGetText(refId, out TMP_Text text) || text == null)
                {
                    if (_missing.Add(key))
                        Debug.LogWarning($"[글자] '{key}': {view.GetType().Name}에 '{refId}' 글자(TMP_Text)가 없다.", view);

                    continue;
                }

                if (!_authored.TryGetValue(text, out string authored))
                {
                    authored = text.text;
                    _authored.Add(text, authored);
                }

                text.text = _localizer.TryGet(key, out string localized) ? localized : authored;
            }
        }
    }
}
