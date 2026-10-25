using System.Globalization;
using TMPro;
using UnityEngine;

namespace BlackHole.Unity
{
    // 전투 화면의 시간 증가 알림: 남은 시간이 늘어난 순간 숫자 오른쪽에 "+0.3"을 띄우고, 그 순간 남은 시간 숫자를 살짝 튕긴다.
    // 짧게 연달아 늘면 하나로 합쳐 "+0.9"처럼 보이고, 마지막 증가 뒤 잠시 있다가 흐려진다. 화면이 매 프레임 Track → Animate 순으로 부른다.
    // 알림 글자(gain)는 프리팹에서 꺼져 있고 여기서 켠다. 수치는 화면에서 보고 맞출 값이다.
    internal sealed class TimeGainNotice
    {
        private const float GainMergeWindow = 0.5f; // 이 안에 다시 늘면 합친다(초)
        private const float GainHold = 0.5f;        // 마지막 증가 뒤 그대로 보이는 시간(초)
        private const float GainFade = 0.3f;        // 그 뒤 흐려지는 시간(초)
        private const float GainGap = 12f;          // 숫자 오른쪽 끝과의 틈(px)
        private const float MinGain = 0.05f;        // 이보다 작은 증가는 모아 두었다가 알린다(초)
        private const float PopDuration = 0.15f;
        private const float PopScale = 0.1f;        // 1.0 → 1.1 → 1.0

        // 남은 시간 숫자(튕김의 대상, 알림의 기준 위치)와 알림 글자. 둘 다 없을 수 있다.
        private readonly TMP_Text _remaining;
        private readonly TMP_Text _gain;

        private float _shownAdded = float.NaN;      // 마지막으로 알린 "더해진 시간" 합. NaN이면 기준이 없다(새 판).
        private float _gainSum;
        private float _sinceGain = float.PositiveInfinity;
        private float _popTime = PopDuration;

        public TimeGainNotice(TMP_Text remaining, TMP_Text gain)
        {
            _remaining = remaining;
            _gain = gain;
            Reset();
        }

        public bool IsVisible => _gain != null && _gain.gameObject.activeSelf;

        // 더해진 시간 합이 늘었으면 알림 글자를 고치고 true. 새 판(합이 줄었거나 기준이 없음)이면 기준만 잡는다.
        public bool Track(float addedSeconds)
        {
            if (float.IsNaN(_shownAdded) || addedSeconds < _shownAdded)
            {
                _shownAdded = addedSeconds;
                return false;
            }

            float gained = addedSeconds - _shownAdded;
            if (gained < MinGain)
                return false;

            _shownAdded = addedSeconds;
            _gainSum = _sinceGain <= GainMergeWindow ? _gainSum + gained : gained;
            _sinceGain = 0;
            _popTime = 0;

            if (_gain == null)
                return false;

            _gain.text = "+" + _gainSum.ToString("0.0", CultureInfo.InvariantCulture);
            _gain.alpha = 1;
            _gain.gameObject.SetActive(true);
            return true;
        }

        // 알림을 숫자 오른쪽 끝 바로 옆에 둔다. 숫자 폭은 자릿수·글자 크기 설정에 따라 바뀐다.
        // 숫자 글자는 Timer 전체에 펼쳐져 있고 기준점이 가운데라, 그 지역 좌표의 x가 곧 Timer 가운데 기준 x다.
        public void Place()
        {
            if (_gain == null || _remaining == null)
                return;

            _remaining.ForceMeshUpdate();
            RectTransform gain = _gain.rectTransform;
            gain.anchoredPosition = new Vector2(_remaining.textBounds.max.x + GainGap, gain.anchoredPosition.y);
        }

        public void Animate(float deltaTime)
        {
            if (_popTime < PopDuration && _remaining != null)
            {
                _popTime += deltaTime;
                float t = Mathf.Clamp01(_popTime / PopDuration);
                float scale = UIMotion.Pop(t, PopScale);
                _remaining.rectTransform.localScale = new Vector3(scale, scale, 1);
            }

            if (float.IsPositiveInfinity(_sinceGain))
                return;

            _sinceGain += deltaTime;
            float alpha = 1 - Mathf.Clamp01((_sinceGain - GainHold) / GainFade);

            if (alpha > 0)
            {
                if (_gain != null)
                    _gain.alpha = alpha;

                return;
            }

            _sinceGain = float.PositiveInfinity;

            if (_gain != null)
                _gain.gameObject.SetActive(false);
        }

        // 알림·튕김을 지우고 기준을 버린다(새 판·화면 닫힘).
        public void Reset()
        {
            _shownAdded = float.NaN;
            _gainSum = 0;
            _sinceGain = float.PositiveInfinity;
            _popTime = PopDuration;

            if (_gain != null)
                _gain.gameObject.SetActive(false);

            if (_remaining != null)
                _remaining.rectTransform.localScale = Vector3.one;
        }
    }
}
