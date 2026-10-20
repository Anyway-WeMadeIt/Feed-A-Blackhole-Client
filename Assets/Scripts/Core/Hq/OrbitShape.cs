using System;

namespace BlackHole.Core
{
    // 적의 공전 모양. 성장도(이정표)가 정하고 판 동안 같다 — 은하를 부술수록 공전이 타원으로 늘어나고 기울며, 소용돌이처럼 안쪽으로 다가가고 빨라진다 [사용자].
    // 공전은 "기울기만큼 되돌리고 가로를 Stretch로 나눈 원 좌표"에서 한 각도씩 도는 원운동이다(EnemyBehaviors.NextPosition). 그래서 궤도는 가로축이 Stretch배인
    // 타원을 Tilt만큼 돌린 모양이다. 안쪽 끌림(Inward)은 그 원 좌표의 반지름을 초마다 그 비율만큼 줄이되 바닥(출현 띠 안쪽 반지름)보다 작아지지는 않는다.
    public readonly struct OrbitShape
    {
        // 바뀌지 않은 공전: 원, 기울기 없음, 끌림 없음, 속도 1배.
        public static readonly OrbitShape Default = new(1, 0, 0, 1);

        public float Stretch { get; }   // 가로축 배율(1 이상). 1이면 원.
        public float Tilt { get; }      // 타원의 기울기(도). 0이면 가로축이 수평.
        public float Inward { get; }    // 반지름이 초마다 줄어드는 비율(0 이상). 0이면 반지름을 지킨다.
        public float Speed { get; }     // 공전 속도 배율(0 초과).

        public OrbitShape(float stretch, float tilt, float inward, float speed)
        {
            if (float.IsNaN(stretch) || float.IsInfinity(stretch) || stretch < 1)
                throw new ArgumentOutOfRangeException(nameof(stretch), $"공전 가로축 배율은 1 이상의 유한한 값이어야 한다. 받은 값: {stretch}.");

            if (float.IsNaN(tilt) || float.IsInfinity(tilt))
                throw new ArgumentOutOfRangeException(nameof(tilt), "공전 기울기는 유한한 값이어야 한다.");

            if (float.IsNaN(inward) || float.IsInfinity(inward) || inward < 0 || inward >= 1)
                throw new ArgumentOutOfRangeException(nameof(inward), $"공전 안쪽 끌림은 0 이상 1 미만이어야 한다(초마다 줄어드는 비율). 받은 값: {inward}.");

            if (float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0)
                throw new ArgumentOutOfRangeException(nameof(speed), $"공전 속도 배율은 0보다 큰 유한한 값이어야 한다. 받은 값: {speed}.");

            Stretch = stretch;
            Tilt = tilt;
            Inward = inward;
            Speed = speed;
        }

        public bool IsDefault => Stretch == 1 && Tilt == 0 && Inward == 0 && Speed == 1;
    }
}
