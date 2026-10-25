using UnityEngine;

namespace BlackHole.Unity
{
    // 한 트랜스폼을 잠깐 키웠다 돌아오게 하는 강조(숫자가 바뀔 때, 줄 수가 줄 때 등). 한 번에 하나 — 다른 대상이 커지는 중이었으면 그것은 원래 크기로.
    // 시간은 부르는 쪽이 Tick으로 흘린다(실제 시간·게임 시간은 부르는 쪽이 정한다). 순수 C#.
    internal sealed class ScalePop
    {
        public enum Curve
        {
            // 1 → 1 + amount → 1을 sin 반 바퀴로(부드럽게).
            Sine,
            // 앞 절반은 곧게 커지고 뒤 절반은 곧게 돌아온다(정점이 뾰족하다).
            Triangle,
        }

        private readonly float _seconds;
        private readonly float _amount;
        private readonly Curve _curve;

        private Transform _target;
        private float _elapsed;

        // seconds: 전체 시간. amount: 가장 클 때 1에 더하는 배율(0.2 → 1.2배).
        public ScalePop(float seconds, float amount, Curve curve = Curve.Sine)
        {
            _seconds = seconds;
            _amount = amount;
            _curve = curve;
        }

        public bool IsPlaying => _target != null;

        public void Play(Transform target)
        {
            Stop();
            _target = target;
            _elapsed = 0f;
        }

        // 커지던 대상을 원래 크기로 되돌리고 멈춘다.
        public void Stop()
        {
            if (_target != null)
                _target.localScale = Vector3.one;

            _target = null;
        }

        public void Tick(float delta)
        {
            if (_target == null)
                return;

            _elapsed += delta;
            float t = _elapsed / _seconds;
            _target.localScale = Vector3.one * ScaleAt(t);

            if (t >= 1f)
                Stop();
        }

        private float ScaleAt(float t)
        {
            if (_curve == Curve.Sine)
                return UIMotion.Pop(t, _amount);

            return t <= 0.5f
                ? Mathf.Lerp(1f, 1f + _amount, t * 2f)
                : Mathf.Lerp(1f + _amount, 1f, (t - 0.5f) * 2f);
        }
    }
}
