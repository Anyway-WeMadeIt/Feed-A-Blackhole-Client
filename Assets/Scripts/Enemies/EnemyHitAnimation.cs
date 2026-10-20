using PrimeTween;
using UnityEngine;

namespace BlackHole.Unity
{
    // 피격 시 좌우로 흔들리는 짧은(0.3초) 연출. PrimeTween의 PunchLocalRotation(흔들림 전용 API)으로 구동한다.
    // Punch는 정해진 방향으로 튀었다가 원래 값으로 돌아오며 잦아드는 흔들림이라, 매번 반대 방향으로 튀는
    // 지금 연출에 Shake보다 더 맞는다 — strength의 부호를 매번 뒤집어서 방향을 바꾼다.
    // 같은 자리에서 연달아 맞으면 Stop 후 다시 시작해, 전에 하던 흔들림을 이어가지 않고 새 방향으로 다시 튄다.
    internal sealed class EnemyHitAnimation
    {
        private const float Duration = 0.3f;
        private const float Angle = 30f;
        // 0.3초 동안 2.5번 왕복하던 예전 사인파와 비슷한 체감이 되도록 잡은 진동수(Oscillations / Duration).
        private const float Oscillations = 2.5f;
        private const float Frequency = Oscillations / Duration;

        private readonly Transform _target;
        private float _direction = 1f;
        private Tween _tween;

        public EnemyHitAnimation(Transform target)
        {
            _target = target;
        }

        public void Play()
        {
            _direction = -_direction;
            _tween.Stop();
            _tween = Tween.PunchLocalRotation(_target, new Vector3(0, 0, Angle * _direction), Duration, Frequency);
        }
    }
}
