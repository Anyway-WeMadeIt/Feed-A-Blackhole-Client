using BlackHole.Core;
using PrimeTween;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    internal sealed class EnemyHitParticles : MonoBehaviour
    {
        private const int FragmentCount = 4;
        private const float Lifetime = 0.24f;
        private const float FragmentSize = 0.12f;
        private const float BaseSpeed = 1.5f;
        private const int FragmentTextureSize = 16;

        // 파편 수 = (색 등급+1) × 크기(최대 28). 버퍼는 이 최댓값에 동시 사망 여유를 곱해 둠(넘치면 오래된 파편부터 버려짐).
        private const int MaxDeathFragmentsPerEnemy = 28;
        private const int DeathConcurrencyHeadroom = 16;
        private const float DeathSuctionDelay = 0.12f;
        private const float DeathFragmentSize = 0.65f; // 파편 기준 크기(여러 차례 조정됨). 필요시 에디터에서 추가 조절.
        // 폭발하듯 튀지 않고 죽은 자리 둘레 이 반경 안에 '배치'만 한다(초기 속도 0, DeathSuctionDelay 후 공전 진입).
        private const float FragmentSpreadRadius = 0.5f;
        // ── 적 크기에 따른 파편 배율 ──
        // 판이 진행될수록(성장도↑) 카메라가 넓어져(Hq.FieldScale 1 → 1.68 → 2.27 → 2.83 → 3.3, BattleCameraFit) 기존 적은
        // 화면에서 작아지고, 새로 풀리는 종류는 월드 반지름 자체가 크다(소행성 0.45 · 행성 1.4 · 별 3.1 · 은하 4.0).
        // 파편이 월드 크기 고정이면 새 종류의 파편이 몸집에 비해 너무 작고 넓어진 화면에서 점처럼 보인다.
        // 그래서 파편 배율 = (적 반지름 ÷ ReferenceRadius)^FragmentSizeExponent 로 정한다(피격·사망 파편 공통).
        //  - 기준(배율 1)은 크기 1 소행성. 지수 0.5(제곱근)면 소행성 크기 2·3·4가 1.15·1.30·1.43배로,
        //    예전 '크기 1당 +15%'(1.15·1.30·1.45) 체감과 거의 같다 — 소행성 쪽 체감은 그대로 유지된다.
        //  - 행성 ≈ 1.76배, 별 ≈ 2.62배, 은하 ≈ 2.98배. 각 종류가 주력이 되는 시점의 넓어진 화면에서도 파편이 처음 소행성
        //    파편과 비슷한 화면 크기로 보이고, 이전 종류(소행성)의 파편은 그 몸집과 함께 작아져 비중이 줄어든다.
        //  - 지수를 1로 올리면 반지름에 정비례(별 파편이 지금의 약 2.6배 더 커짐), 0으로 내리면 크기 무관 고정.
        // 배치 반경(FragmentSpreadRadius)과 피격 파편이 튀는 속도도 같은 배율을 곱해, 커진 파편끼리 겹쳐 뭉치지 않게 한다.
        private const float ReferenceRadius = 0.45f;
        private const float FragmentSizeExponent = 0.5f;

        // ── 사망 파편 흡입 궤적 ──
        // 모든 속도는 "월드 단위/초" 선속도로 정한다. 각속도는 접선 선속도 ÷ 현재 반지름으로 매 프레임 구한다.
        // 예전처럼 각속도를 거리와 무관한 하나의 값으로 두면(= 강체 회전) 세 가지 문제가 한꺼번에 생겼다:
        //  1) 멀리서 죽은 적의 파편은 선속도가 거리에 비례해 커져(반지름 9면 초당 수십 단위) 화면을 가로질러
        //     튕겨 나가는 것처럼 보였다(원심력 착시).
        //  2) 같은 적의 파편이 회전 대열을 그대로 유지한 채 반지름 방향으로만 줄어들어 일직선(스포크)이 됐다.
        //  3) 가까이서 함께 죽은 적들의 파편이 한 덩어리로 굳은 채 같이 돌다 같이 빨려 들어갔다.
        // 지금은 안쪽일수록 빨리 도는 차등 회전이라 당겨지는 파편이 앞질러 나가며 나선 팔을 만들고, 덩어리는
        // 시간이 지날수록 호(arc) 모양으로 찢어져 흩어진다. 반지름은 줄기만 하고 늘지 않는다(바깥으로 안 나감).
        private const float WaitOrbitSpeed = 0.9f;      // 자기 차례 전, 죽은 자리 근처에서 천천히 도는 접선 선속도
        private const float WaitInwardSpeed = 0.12f;    // 자기 차례 전에도 아주 천천히 안쪽으로 스며드는 선속도
        private const float PullStartSpeed = 0.6f;      // 당김 시작 순간의 구심 선속도
        private const float PullAcceleration = 7f;      // 당김 중 구심 선속도의 가속도(단위/초²) — 끝으로 갈수록 빨라짐
        // 당김 중 접선 선속도 = 구심 선속도 × 이 값(+ 대기 공전 속도). 두 성분이 같은 비율로 커지므로 궤적은
        // 로그 나선이 되고, 값이 클수록 많이 감긴다(2 ≈ 반지름 7에서 중심까지 약 한 바퀴).
        private const float SpiralTwist = 2f;
        // 각속도 계산에 쓰는 최소 반지름 — 중심 바로 앞에서 각속도가 폭주해 프레임마다 크게 튀는 걸 막는다.
        private const float MinSwirlRadius = 0.35f;
        private const float CenterRadius = 0.06f;
        // 공전 진입을 부드럽게 하는 이징(전원 DeathSuctionDelay 시점에 동시 진입).
        private const float OrbitEaseInDuration = 0.35f;
        // 파편 하나씩 차례로 당긴다. 순서 간격에 파편마다 ±StaggerRandomness(비율)만큼, 적마다 0~1 간격만큼
        // 위상을 흔들어 — 가까이서 함께 죽은 적들의 i번째 파편끼리 짝지어 같이 날아가지 않게 한다.
        private const float SuctionStaggerInterval = 0.18f;
        private const float StaggerRandomness = 0.35f;
        // 적(사망 1회)마다 한 번 뽑는 배율 + 파편마다 조금 더 흔드는 배율. 회전(감김)과 당김 속도 양쪽에 준다.
        // 수명은 EmitDeath가 이 값들까지 넣어 파편마다 정확히 계산하므로 도착 타이밍이 어긋나지 않는다.
        private const float EnemySwirlJitter = 0.2f;
        private const float FragmentSwirlJitter = 0.15f;
        private const float EnemyPullJitter = 0.15f;
        private const float FragmentPullJitter = 0.1f;
        // 시스템 기본 수명(폴백). 실제 수명은 EmitDeath에서 파편마다 지정된다.
        private const float FallbackDeathLifetime = 3f;

        // randomSeed(uint) 비트 패킹 — EmitDeath가 파편별 궤적 값을 실어 보내고 Advance가 꺼내 쓴다
        // (이 파티클 시스템에는 randomSeed를 쓰는 모듈이 없어 안전하게 재사용 가능).
        //  [0..9]   당김 시작 나이(1/100초 단위, 최대 10.23초)
        //  [10..17] 속도 배율(종류별 배율 × 당김 지터, 0~SpeedScaleMax를 8비트로)
        //  [18..25] 회전 배율(회전 지터, SwirlScaleMin~SwirlScaleMax를 8비트로)
        private const uint PullStartMask = (1u << 10) - 1;
        private const int SpeedShift = 10;
        private const int SwirlShift = 18;
        private const uint ByteMask = 0xFF;
        private const float SpeedScaleMax = 1.5f;
        private const float SwirlScaleMin = 0.5f;
        private const float SwirlScaleMax = 1.5f;

        private static readonly Vector2[] FragmentVertices =
        {
            new Vector2(-0.46f, 0.24f),
            new Vector2(-0.16f, 0.47f),
            new Vector2(0.44f, 0.28f),
            new Vector2(0.29f, -0.18f),
            new Vector2(-0.08f, -0.45f),
            new Vector2(-0.42f, -0.22f),
        };

        private ParticleSystem _particles;
        private ParticleSystem _deathParticles;
        private readonly ParticleSystem.Particle[] _deathParticleBuffer = new ParticleSystem.Particle[MaxDeathFragmentsPerEnemy * DeathConcurrencyHeadroom];
        private Texture2D _fragmentTexture;
        private Material _fragmentMaterial;
        private int _burstIndex;

        private void Awake()
        {
            _particles = gameObject.AddComponent<ParticleSystem>();
            // playOnAwake 기본값(true)으로 즉시 재생되므로, main.duration 등을 건드리기 전에 반드시 멈춰야 함
            // (안 그러면 "Setting the duration while system is still playing" 에러).
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = Lifetime;
            main.startLifetime = Lifetime;
            main.startSpeed = 0;
            main.startSize = FragmentSize;
            main.startColor = Color.white;
            main.maxParticles = FragmentCount * 8;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.rateOverTime = 0;
            emission.rateOverDistance = 0;

            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = _particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            colorOverLifetime.color = fade;

            _fragmentTexture = CreateFragmentTexture();
            _fragmentMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                name = "Enemy Hit Fragment",
                mainTexture = _fragmentTexture,
            };

            ParticleSystemRenderer particleRenderer = _particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = _fragmentMaterial;
            particleRenderer.sortingOrder = 2;

            var deathParticleObject = new GameObject("Enemy Death Particles");
            deathParticleObject.transform.SetParent(transform, false);
            _deathParticles = deathParticleObject.AddComponent<ParticleSystem>();
            _deathParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule deathMain = _deathParticles.main;
            deathMain.playOnAwake = false;
            deathMain.loop = false;
            // 실제 수명은 EmitDeath에서 파편마다 개별 지정됨 — 여기 값은 폴백일 뿐.
            deathMain.duration = FallbackDeathLifetime;
            deathMain.startLifetime = FallbackDeathLifetime;
            deathMain.startSpeed = 0;
            deathMain.startSize = DeathFragmentSize;
            deathMain.startColor = Color.white;
            deathMain.maxParticles = _deathParticleBuffer.Length;
            deathMain.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule deathEmission = _deathParticles.emission;
            deathEmission.rateOverTime = 0;
            deathEmission.rateOverDistance = 0;

            ParticleSystem.ShapeModule deathShape = _deathParticles.shape;
            deathShape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule deathColor = _deathParticles.colorOverLifetime;
            deathColor.enabled = true;
            deathColor.color = fade;

            ParticleSystemRenderer deathRenderer = _deathParticles.GetComponent<ParticleSystemRenderer>();
            deathRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            deathRenderer.sharedMaterial = _fragmentMaterial;
            deathRenderer.sortingOrder = 2;
        }

        public void Clear()
        {
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _deathParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDestroy()
        {
            Object.Destroy(_fragmentMaterial);
            Object.Destroy(_fragmentTexture);
        }

        // 적 반지름 → 파편 배율(위 '적 크기에 따른 파편 배율' 참고).
        private static float FragmentScaleOf(float radius)
        {
            return Mathf.Pow(Mathf.Max(radius, 0.01f) / ReferenceRadius, FragmentSizeExponent);
        }

        // 피격 파편: 맞은 자리에서 작은 파편 몇 개를 사방으로 튀긴다. color는 맞은 적의 색, radius는 맞은 적의 반지름이다.
        public void EmitHit(Vector3 position, Color color, float radius)
        {
            if (!_particles.isPlaying)
                _particles.Play(false);

            float baseAngle = (_burstIndex++ % 8) * Mathf.PI / 4f;
            float scale = FragmentScaleOf(radius);

            for (int i = 0; i < FragmentCount; i++)
            {
                float angle = baseAngle + i * Mathf.PI * 2f / FragmentCount;
                float speed = (BaseSpeed + (i % 2) * 0.5f) * scale;
                var parameters = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * speed,
                    startLifetime = Lifetime,
                    startSize = FragmentSize * (i % 2 == 0 ? 1f : 0.75f) * scale,
                    startColor = color,
                    rotation = angle + i * 0.73f,
                };

                _particles.Emit(parameters, 1);
            }
        }

        // 적 종류별 흡입 속도 배율(소행성 > 행성 > 별 > 은하 순으로 빠름). 대기 공전·당김 속도 전부에 곱한다.
        private static float SuctionSpeedMultiplierOf(EnemyType type)
        {
            switch (type)
            {
                case EnemyType.Asteroid:
                    return 1f;
                case EnemyType.Star:
                    return 0.5f;
                case EnemyType.Galaxy:
                    return 0.35f;
                case EnemyType.Planet:
                default:
                    return 0.7f;
            }
        }

        private static uint EncodePullStart(float age)
        {
            return (uint)Mathf.Clamp(Mathf.RoundToInt(age * 100f), 0, (int)PullStartMask);
        }

        private static float DecodePullStart(uint seed)
        {
            return (seed & PullStartMask) / 100f;
        }

        private static uint EncodeSpeed(float scale)
        {
            return (uint)Mathf.Clamp(Mathf.RoundToInt(scale / SpeedScaleMax * 255f), 1, 255);
        }

        private static float DecodeSpeed(uint seed)
        {
            return ((seed >> SpeedShift) & ByteMask) / 255f * SpeedScaleMax;
        }

        private static uint EncodeSwirl(float scale)
        {
            return (uint)Mathf.RoundToInt(Mathf.InverseLerp(SwirlScaleMin, SwirlScaleMax, scale) * 255f);
        }

        private static float DecodeSwirl(uint seed)
        {
            return Mathf.Lerp(SwirlScaleMin, SwirlScaleMax, ((seed >> SwirlShift) & ByteMask) / 255f);
        }

        // 시작 거리에서 중심(CenterRadius)에 닿기까지의 나이 — Advance의 반지름 변화와 같은 식이다
        // (대기 중 등속으로 스며들기 → 당김 시작 후 등가속). colorOverLifetime이 수명 끝에서 완전히 투명해지게
        // 짜여 있으므로 수명을 도착 시점에 맞춘다. 회전은 반지름을 바꾸지 않으므로 계산에 끼지 않는다.
        private static float LifetimeOf(float startDistance, uint seed)
        {
            float pullStart = DecodePullStart(seed);
            float speed = DecodeSpeed(seed);
            float distanceAtPull = startDistance - WaitInwardSpeed * speed * (pullStart - DeathSuctionDelay);
            float remaining = Mathf.Max(0f, distanceAtPull - CenterRadius);
            float startSpeed = PullStartSpeed * speed;
            float acceleration = PullAcceleration * speed;
            float travel = (Mathf.Sqrt(startSpeed * startSpeed + 2f * acceleration * remaining) - startSpeed) / acceleration;
            return pullStart + travel;
        }

        // 사망 파편: 폭발 없이 죽은 자리 둘레에 배치만 하고, DeathSuctionDelay 후 그 자리에서 천천히 돌기
        // 시작하며, 자기 차례가 되면 Advance가 나선을 그리며 HQ로 빨아들인다. type은 종류별 흡입 속도 차등에 쓰인다.
        // radius(죽은 적의 반지름)가 파편 크기와 배치 반경을 정한다(FragmentScaleOf). size는 파편 수에만 쓴다.
        public void EmitDeath(Vector3 position, Color color, EnemyType type, int tier, int size, float radius)
        {
            if (!_deathParticles.isPlaying)
                _deathParticles.Play(false);

            // 파편 수 = (Tier+1) × 크기. 예: Tier0·크기2 → 2개, Tier4·크기3 → 15개.
            int fragmentCount = (tier + 1) * size;
            float baseAngle = (_burstIndex++ % 16) * Mathf.PI / 8f;
            float fragmentSizeMultiplier = FragmentScaleOf(radius);
            float spreadRadius = FragmentSpreadRadius * fragmentSizeMultiplier;
            float typeSpeed = SuctionSpeedMultiplierOf(type);

            // 이 적(사망 1회) 전체에 공통인 값. 가까이서 함께 죽은 다른 적과 감김·속도·당김 타이밍이 서로 달라져
            // 한 덩어리로 같이 움직이지 않는다.
            float enemySwirl = 1f + UnityEngine.Random.Range(-EnemySwirlJitter, EnemySwirlJitter);
            float enemyPull = 1f + UnityEngine.Random.Range(-EnemyPullJitter, EnemyPullJitter);
            float staggerPhase = UnityEngine.Random.value * SuctionStaggerInterval;

            for (int i = 0; i < fragmentCount; i++)
            {
                float angle = baseAngle + i * Mathf.PI * 2f / fragmentCount;
                Vector3 start = position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * spreadRadius;

                float order = i + UnityEngine.Random.Range(-StaggerRandomness, StaggerRandomness);
                float pullStart = Mathf.Max(DeathSuctionDelay, DeathSuctionDelay + staggerPhase + order * SuctionStaggerInterval);
                float speed = typeSpeed * enemyPull * (1f + UnityEngine.Random.Range(-FragmentPullJitter, FragmentPullJitter));
                float swirl = enemySwirl * (1f + UnityEngine.Random.Range(-FragmentSwirlJitter, FragmentSwirlJitter));
                uint seed = EncodePullStart(pullStart) | (EncodeSpeed(speed) << SpeedShift) | (EncodeSwirl(swirl) << SwirlShift);

                var parameters = new ParticleSystem.EmitParams
                {
                    position = start,
                    velocity = Vector3.zero,
                    // 인코딩된 값으로 계산해야 Advance가 실제로 쓰는 값과 정확히 같다.
                    startLifetime = LifetimeOf(new Vector2(start.x, start.y).magnitude, seed),
                    startSize = (DeathFragmentSize + (i % 3) * 0.13f) * fragmentSizeMultiplier,
                    startColor = color,
                    rotation = angle + i * 0.61f,
                    randomSeed = seed,
                };

                _deathParticles.Emit(parameters, 1);
            }
        }

        // EnemyView.Synchronize가 매 프레임 delta와 함께 호출. 위치 동기화와 같은 타이밍/순서로 돌아야 해서
        // (스크립트 실행 순서에 기대지 않도록) Update() 대신 외부에서 직접 구동.
        // 위치는 유니티의 velocity 적분에 맡기지 않고 매 프레임 정확한 회전 변환 + 반지름 감소로 직접 계산한다 —
        // 접선 속도를 엔진이 직선으로 적분하면 프레임마다 조금씩 바깥으로 밀리는 오차가 누적되기 때문.
        public void Advance(float delta)
        {
            if (delta <= 0)
                return;

            int count = _deathParticles.GetParticles(_deathParticleBuffer);
            if (count == 0)
                return;

            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle particle = _deathParticleBuffer[i];
                float age = particle.startLifetime - particle.remainingLifetime;
                if (age < DeathSuctionDelay)
                    continue;

                Vector2 offset = new Vector2(particle.position.x, particle.position.y);
                float distance = offset.magnitude;
                if (distance <= CenterRadius)
                {
                    particle.remainingLifetime = 0;
                    _deathParticleBuffer[i] = particle;
                    changed = true;
                    continue;
                }

                uint seed = particle.randomSeed;
                float pullStart = DecodePullStart(seed);
                float speed = DecodeSpeed(seed);
                float swirl = DecodeSwirl(seed);

                float orbitStrength = Easing.Evaluate(Mathf.Clamp01((age - DeathSuctionDelay) / OrbitEaseInDuration), Ease.OutQuad);
                float tangentialSpeed = WaitOrbitSpeed * orbitStrength * speed;
                float inwardSpeed;
                if (age < pullStart)
                {
                    // 차례 전: 죽은 자리 근처에서 천천히 돌며 아주 조금씩 안으로.
                    inwardSpeed = WaitInwardSpeed * speed;
                }
                else
                {
                    // 차례 후: 구심 속도가 등가속으로 붙고, 접선 속도도 같은 비율로 붙어 나선을 그린다.
                    inwardSpeed = (PullStartSpeed + PullAcceleration * (age - pullStart)) * speed;
                    tangentialSpeed += inwardSpeed * SpiralTwist;
                }

                // 각속도 = 선속도 ÷ 반지름 — 안쪽일수록 빨리 돈다(차등 회전). 이게 나선 팔과 흩어짐을 만든다.
                float deltaAngle = tangentialSpeed / Mathf.Max(distance, MinSwirlRadius) * swirl * delta;
                float cos = Mathf.Cos(deltaAngle);
                float sin = Mathf.Sin(deltaAngle);
                Vector2 rotated = new Vector2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos);

                float newDistance = Mathf.Max(0f, distance - inwardSpeed * delta);
                Vector2 newOffset = rotated * (newDistance / distance);

                particle.position = new Vector3(newOffset.x, newOffset.y, particle.position.z);
                particle.velocity = Vector3.zero;
                _deathParticleBuffer[i] = particle;
                changed = true;
            }

            if (changed)
                _deathParticles.SetParticles(_deathParticleBuffer, count);
        }

        private static Texture2D CreateFragmentTexture()
        {
            var texture = new Texture2D(FragmentTextureSize, FragmentTextureSize, TextureFormat.RGBA32, false)
            {
                name = "Enemy Hit Fragment Shape",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[FragmentTextureSize * FragmentTextureSize];
            for (int y = 0; y < FragmentTextureSize; y++)
            {
                for (int x = 0; x < FragmentTextureSize; x++)
                {
                    float pointX = (x + 0.5f) / FragmentTextureSize * 2f - 1f;
                    float pointY = (y + 0.5f) / FragmentTextureSize * 2f - 1f;
                    bool inside = false;

                    for (int i = 0, j = FragmentVertices.Length - 1; i < FragmentVertices.Length; j = i++)
                    {
                        Vector2 start = FragmentVertices[j];
                        Vector2 end = FragmentVertices[i];
                        if ((start.y > pointY) != (end.y > pointY) && pointX < (end.x - start.x) * (pointY - start.y) / (end.y - start.y) + start.x)
                            inside = !inside;
                    }

                    pixels[y * FragmentTextureSize + x] = new Color32(255, 255, 255, inside ? (byte)255 : (byte)0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}