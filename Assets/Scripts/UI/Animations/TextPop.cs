using TMPro;
using UnityEngine;

namespace BlackHole.Unity
{
    // 값이 바뀐 글자를 잠깐 키우는 강조 효과(결산 화면의 숫자 올리기). 한 번에 하나 — 다른 글자가 커지는 중이었으면 그것은 원래 크기로.
    // 앞 절반은 HighlightScale까지 커지고 뒤 절반은 1로 돌아온다. 시간은 부르는 쪽이 Tick으로 흘린다. 순수 C#.
    internal sealed class TextPop
    {
        private const float Duration = 0.15f;
        private const float HighlightScale = 1.2f;

        private TMP_Text _target;
        private float _timer;

        public void Play(TMP_Text text)
        {
            Stop();
            _target = text;
            _timer = 0f;
        }

        public void Stop()
        {
            if (_target != null)
                _target.transform.localScale = Vector3.one;

            _target = null;
        }

        public void Tick(float delta)
        {
            if (_target == null)
                return;

            _timer += delta;
            float t = _timer / Duration;
            float scale = t <= 0.5f
                ? Mathf.Lerp(1f, HighlightScale, t * 2f)
                : Mathf.Lerp(HighlightScale, 1f, (t - 0.5f) * 2f);
            _target.transform.localScale = Vector3.one * scale;

            if (t >= 1f)
                Stop();
        }
    }
}
