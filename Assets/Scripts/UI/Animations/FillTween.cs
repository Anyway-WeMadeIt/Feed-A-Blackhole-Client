using System;
using UnityEngine;

namespace BlackHole.Unity
{
    // 가로 막대 채움 연출: RectTransform의 anchorMax.x를 0에서 목표까지 SmoothStep으로 올린다(결산 화면의 이정표 막대). 순수 C#.
    internal sealed class FillTween
    {
        private RectTransform _rect;
        private float _target;
        private float _seconds;
        private float _elapsed;

        public bool IsRunning { get; private set; }

        // 다 찼다(또는 Finish로 끝냈다).
        public event Action Completed;

        public void Start(RectTransform rect, float target, float seconds)
        {
            _rect = rect;
            _target = Mathf.Clamp01(target);
            _seconds = Mathf.Max(seconds, 0.0001f);
            _elapsed = 0f;
            IsRunning = rect != null;
            Set(0f);
        }

        public void Tick(float deltaTime)
        {
            if (!IsRunning)
                return;

            _elapsed += deltaTime;
            float t = UIMotion.Smooth(_elapsed / _seconds);
            Set(_target * t);

            if (t >= 1f)
                Finish();
        }

        // 바로 목표까지 채우고 끝낸다.
        public void Finish()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            Set(_target);
            Completed?.Invoke();
        }

        private void Set(float fill)
        {
            if (_rect != null)
                _rect.anchorMax = new Vector2(fill, _rect.anchorMax.y);
        }
    }
}
