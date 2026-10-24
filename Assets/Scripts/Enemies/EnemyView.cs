using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 적 시스템의 화면. 매 프레임 World의 살아 있는 적을 읽어 스프라이트를 맞추고, 짧은 피격 흔들림 연출도 같이 진행시킨다.
    // 게임 상태를 바꾸지 않는다. 외형은 적 종류 에셋이 가진다(EnemyLooks). 색은 적의 색 등급으로, 크기는 적의 수치로 정한다.
    // 특수 성질이 붙었으면 그 색의 윤곽 안에 성질의 표식 색으로 속을 한 겹 더 그린다(황금이면 노란 속, 임시 표식). 스프라이트가 없는 종류는 적 ID에 맞는 다각형으로 그린다.
    // 달 성질이 붙은 적은 Breaker 달과 같은 모양의 달 하나가 주위를 공전한다. 크기·위상·속도는 출현 때 정해진다(일시정지 중에는 멈춘다).
    // 은하는 중심과 세 팔이 하나의 실루엣으로 자전하고(공전과 별개, 화면에서만), 죽으면 나선이 중심으로 말려 들어가며 사라진다(GalaxyCollapses).
    // 혜성을 뺀 적은 출현할 때 점에서 시작해 제 크기보다 살짝 커졌다가 제 크기로 돌아온다(AdvanceSpawn). 일시정지 중에는 멈춘다.
    // 붙잡힌 적(World.IsHeld: 조준점이 있고 Breaker 링 안이라 거의 멈춘 적)은 색이 BreakerLook.HoldTint 쪽으로 밝아지고 제자리에서 잘게 떤다.
    //   켜지고 꺼지는 것은 HoldFadeSeconds 동안 부드럽게. 떨림은 Container의 위치에만 더하므로 피격 흔들림·자전과 부딪히지 않는다. 일시정지 중에는 멈춘다.
    // 판 시작 연출 동안에는 원점에서 RevealRadius 안의 적만 화면을 만든다. 반경이 넓어지면 바깥 적이 그때 출현 연출과 함께 나타난다(BattleSystem).
    // 픽업(혜성)은 CometView가 통째로 맡는다(스프라이트가 아니라 셰이더). 사망 파편·골드·피해량 텍스트도 없다 — 한 방에 부서지는 버프 아이템이라 필요 없다.
    // 목록에서 빠진 적(사망)의 스프라이트는 바로 지운다.
    // 붕괴(엔딩, Collapse > 0): 판이 끝나 멈춘 적들이 소용돌이치며 원점(블랙홀)으로 빨려 들어가고, 가까워질수록 작아져 사라진다.
    //   가까운 적이 먼저, 먼 적은 늦게 출발해 같은 시간 안에 모두 닿는다. 혜성은 곧바로 지운다. 화면만이다(BattleSystem이 0 → 1로 흘린다).
    // 체력 음영: 몸체(와 성질 속 채움)는 '남은 체력 비율'만큼의 면적에 음영(검정 α ShadeStrength를 덮은 색)이 씌워져 있고,
    //   피해를 입은 만큼 몸체의 한쪽(로컬 위)부터 기본색이 드러나며, 경계선은 자전과 함께 돈다(BlackHole/Enemy Shade 셰이더, UpdateShade). 체력 50%면 절반은 기본색, 절반은 음영.
    // 피격·사망 연출(흔들림·파편·피해량·Gold 텍스트·파괴음)은 Step이 끝난 뒤 판의 피격 기록(World.Hits)과 사망 기록(World.Deaths)을 읽어 낸다.
    // 일시정지 중에는 판이 기록을 비우지 않으므로, 기록마다 번호(Sequence)로 걸러 한 번씩만 낸다.
    // 규칙 평면은 장면의 z = 0이고 x·y는 같다. HQ(원점)가 장면의 원점이다.
    internal sealed class EnemyView : IDisposable
    {
        // 적 하나의 화면 상태: 스프라이트와, 그 스프라이트에 적용되는 짧은 피격 흔들림 연출.
        private sealed class EnemyVisual
        {
            // 위치·크기(출현 연출)·자전을 맡는 바깥 트랜스폼. 소행성/행성/별은 Renderer(스프라이트)를 감싸는
            // 별도 부모(아래 Create 참고)라서, 자전 회전과 Hit의 펀치 회전(Renderer.transform)이 서로 다른
            // 트랜스폼을 써서 부딪히지 않는다. 그 외 종류는 Renderer.transform과 같은 객체다(구조 변화 없음).
            public Transform Container;
            public SpriteRenderer Renderer;
            public EnemyHitAnimation Hit;
            // 소행성/행성/별만 0이 아니다(출현 때 무작위로 정함). 공전(이동)과 별개로 제자리에서 도는 자전
            // 속도(도/초, 부호가 방향).
            public float SpinSpeed;
            // 달 성질이 붙은 적만 가진다(없으면 null). 적 스프라이트의 형제라 피격 흔들림·배율의 영향을 받지 않는다.
            public MeshRenderer Moon;
            // 달 공전의 위상(바퀴)과 속도(바퀴/초, 시계방향). 적마다 출현 때 한 번 정해진다.
            public float MoonPhase;
            public float MoonSpeed;
            // 은하만 가진다(없으면 null). 적 스프라이트(중심)의 자식이라 피격 흔들림과 크기를 함께 받고, 자기 자전만 따로 한다.
            public Transform Arms;
            // 나선팔 자전 속도(도/초, 음수 = 시계방향). 출현 때 한 번 정해진다.
            public float ArmSpin;
            // 출현 연출이 끝났을 때의 크기(적 스프라이트·달). 연출 중에는 여기에 배율을 곱해 키운다.
            public Vector3 BaseScale;
            public Vector3 MoonBaseScale;
            // 출현 연출이 흐른 시간(초). SpawnSeconds에 닿으면 연출이 끝난다.
            public float SpawnElapsed;
            // 색을 바꾸는 렌더러(은하는 실루엣 팔, 그 외는 Renderer)와 그 기본 색(색 등급).
            public SpriteRenderer Body;
            public Color BaseColor;
            // 붙잡힘 표시의 세기(0 = 없음, 1 = 완전히 붙잡힘). HoldFadeSeconds에 걸쳐 목표로 간다.
            public float Hold;
            // 떨림의 위상(적마다 다르게).
            public float HoldPhase;
            // 성질 속 채움(황금 등). 없으면 null. 체력 음영을 몸체와 같이 받는다.
            public SpriteRenderer Fill;
            // 마지막으로 음영 문턱값을 계산한 체력 비율(처음엔 −1 = 아직 계산 안 함)과 그 문턱값.
            public float ShadeRatio = -1;
        }

        // 출현 연출 [임시]: SpawnSeconds 동안 크기 0(점)에서 제 크기로 키운다. OutBack 곡선이라 도중에 제 크기를 살짝 넘었다가 돌아온다.
        private const float SpawnSeconds = 0.35f;
        // OutBack 곡선의 넘침 정도. 1.70158이면 가장 클 때 제 크기의 약 1.1배다. 키우면 더 크게 튀었다가 돌아온다.
        private const float SpawnOvershoot = 1.70158f;

        // 은하 나선팔의 자전 속도(도/초)와 적마다의 배율 범위 [임시]. 팔이 반시계로 감겨 있어 시계방향으로 돌면 팔이 뒤로 끌린다(EnemyLooks).
        private const float GalaxySpin = 40f;
        private const float GalaxySpinMin = 0.8f;
        private const float GalaxySpinMax = 1.2f;

        // 소행성/행성/별의 자전 속도 범위(도/초) [임시]. 공전(이동)과 별개로 제자리에서 천천히 도는 연출 —
        // 방향(시계/반시계)은 적마다 무작위.
        private const float SpinSpeedMin = 10f;
        private const float SpinSpeedMax = 25f;

        // 달 구체의 반지름(전장 배율 1에서의 월드 단위). 모든 행성이 같은 크기의 달을 가진다. Breaker 달(BreakerLook.OrbRadius)보다 작다.
        // 원작 달은 카메라와 무관하게 화면에서 약 25px이라(C0 지름 25px ÷ 54px/unit ÷ 2) 판의 전장 배율을 곱한다.
        private const float MoonRadius = 0.236f;
        // 공전 속도 배율의 범위. 기본 속도(BreakerLook.OrbitSpeed)에 곱해 적마다 다르게 돈다.
        private const float MoonSpeedMin = 0.8f;
        private const float MoonSpeedMax = 1.2f;
        // 달은 적 스프라이트(0)와 성질 속 채움(1) 위, Breaker 링 계열(5 ~ 11) 아래에 그린다.
        private const int MoonSortingOrder = 2;

        // 이 판의 전장 배율(Hq.FieldScale). 화면 크기가 고정인 달과 떠오르는 숫자에 곱한다. Synchronize가 매번 읽는다.
        private float _fieldScale = HqGrowthDefinition.StartFieldScale;
        private const float MeshMargin = 1.1f;
        // 셰이더(BlackHole/Breaker Orbs)의 MAX_ORBS와 같다. 달만 그리므로 칸 종류는 모두 0(달)이다.
        private const int MaxOrbs = 64;

        private static readonly int _orbitRadiusId = Shader.PropertyToID("_OrbitRadius");
        private static readonly int _orbRadiusId = Shader.PropertyToID("_OrbRadius");
        private static readonly int _orbCountId = Shader.PropertyToID("_OrbCount");
        private static readonly int _phaseId = Shader.PropertyToID("_Phase");
        private static readonly int _moonFillId = Shader.PropertyToID("_MoonFill");
        private static readonly int _moonOutlineId = Shader.PropertyToID("_MoonOutline");
        private static readonly int _outlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int _orbKindsId = Shader.PropertyToID("_OrbKinds");

        // 체력 음영(BlackHole/Enemy Shade). 셰이더를 못 찾으면(빌드에 셰이더가 빠진 경우 등) 음영 없이 기존처럼 그린다.
        private const string ShadeShaderName = "BlackHole/Enemy Shade";
        private static readonly int _shadeCenterId = Shader.PropertyToID("_ShadeCenter");
        private static readonly int _shadeRadiusId = Shader.PropertyToID("_ShadeRadius");
        private static readonly int _shadeThresholdId = Shader.PropertyToID("_ShadeThreshold");
        // 문턱값이 이 값이면 경계(부드러움 포함)가 몸체 밖이라 전부 음영 / 전부 기본색이다.
        private const float ShadeAllThreshold = 2f;

        private readonly Transform _root;
        private readonly EnemyLooks _looks;
        // 표시 설정(피해량·Gold 텍스트, 물질 파편). 끄면 그다음 기록부터 내지 않는다 — 이미 떠 있는 것은 제 수명대로 사라진다.
        private readonly GameSettings _settings;
        private readonly BreakerLook _breakerLook;
        private readonly CometView _comets;
        private readonly Mesh _quad;
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock _shade = new MaterialPropertyBlock();
        private readonly Material _shadeMaterial;
        private readonly float[] _moonKinds = new float[MaxOrbs];
        private readonly EnemyHitParticles _hitParticles;
        private readonly EnemyGoldText _goldText;
        private readonly BattleSounds _sounds;
        private readonly EnemyDamageText _damageText;
        private readonly Dictionary<EnemyId, EnemyVisual> _visuals = new Dictionary<EnemyId, EnemyVisual>();
        // 붙잡힘 떨림이 흐르는 시간(초). 일시정지 중에는 멈춘다.
        private float _holdTime;
        private readonly HashSet<EnemyId> _seen = new HashSet<EnemyId>();
        private readonly List<EnemyId> _gone = new List<EnemyId>();
        // 이번 프레임에 사망 기록으로 죽음을 안 은하. 화면을 지우는 대신 붕괴 연출로 넘긴다.
        private readonly HashSet<EnemyId> _collapsing = new HashSet<EnemyId>();
        private readonly GalaxyCollapses _collapses = new GalaxyCollapses();

        // 붕괴 연출 [임시]: 가장 먼 적이 출발하는 시점(전체 0 ~ 1 중), 끝까지 감기는 각도(라디안, 먼 적일수록 더 감긴다).
        private const float CollapseStagger = 0.45f;
        private const float CollapseSwirl = 3.2f;
        // 붕괴를 시작할 때 원점에서 가장 먼 적까지의 거리(시작 순서의 기준). 0이면 아직 재지 않았다.
        private float _collapseReach;

        // 마지막으로 연출을 낸 피격·사망 기록의 번호.
        private long _lastHitSequence;
        private long _lastDeathSequence;

        // 원점(HQ)에서 적 중심까지 이 거리보다 먼 적은 아직 화면을 만들지 않는다(월드 단위, 기본은 무한 = 모두 보임).
        // 판 시작 연출이 0에서 넓힌다(BattleSystem). 한 번 보인 적은 다시 숨기지 않는다. 연출 동안 판이 멈춰 있어 숨은 적의 피격·사망은 없다.
        public float RevealRadius { get; set; } = float.PositiveInfinity;

        // 붕괴(엔딩) 진행도. 0이면 없음(평소), 1이면 모든 적이 블랙홀에 닿아 보이지 않는다. Reset이 0으로 되돌린다.
        public float Collapse { get; set; }

        // breakerLook: 달의 외형(머티리얼·색·간격·기본 공전 속도)을 Breaker 달과 같게 맞추려고 받는다.
        // cometLook: 혜성(픽업)의 외형. 혜성의 화면은 CometView가 맡는다.
        public EnemyView(Transform parent, EnemyLooks looks, BreakerLook breakerLook, CometLook cometLook, GameSettings settings, BattleSounds sounds)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _root = new GameObject("Enemy View").transform;
            _root.SetParent(parent, false);
            _looks = looks;
            _breakerLook = breakerLook;
            _comets = new CometView(parent, cometLook);
            _quad = QuadRenderers.CreateMesh();
            _hitParticles = _root.gameObject.AddComponent<EnemyHitParticles>();
            _goldText = new EnemyGoldText(_root);
            _sounds = sounds;
            _damageText = new EnemyDamageText(_root);

            Shader shadeShader = Shader.Find(ShadeShaderName);
            if (shadeShader != null)
                _shadeMaterial = new Material(shadeShader) { name = "Enemy Shade" };
            else
                Debug.LogWarning($"[적 화면] '{ShadeShaderName}' 셰이더를 찾지 못해 체력 음영 없이 그린다. 빌드라면 Project Settings > Graphics > Always Included Shaders에 추가할 것.");
        }

        // paused: 일시정지 중이면 달 공전을 멈춘다.
        // delta: 이번 호출 사이 지난 시간. 피격 흔들림·달 공전 진행에 쓴다(즉시 스냅만 하고 싶을 때는 0을 넘긴다).
        public void Synchronize(World world, bool paused, float delta)
        {
            _seen.Clear();
            IReadOnlyList<Enemy> enemies = world.Enemies;
            _fieldScale = world.Hq.FieldScale;
            _goldText.Scale = _fieldScale;
            _damageText.Scale = _fieldScale;

            if (!paused)
                _holdTime += delta;

            float holdStep = paused ? 0 : delta / _breakerLook.HoldFadeSeconds;
            bool collapsing = Collapse > 0;

            if (collapsing && _collapseReach <= 0)
                _collapseReach = FarthestOf(enemies);

            // 매 프레임 경로: IReadOnlyList를 인덱스로 돈다(인터페이스 foreach는 열거자를 할당한다).
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];

                if (enemy.Definition.IsPickup)
                {
                    // 붕괴 중에는 혜성을 보이지 않는다 — 보인 목록에 넣지 않으면 아래 Retain이 지운다.
                    if (collapsing)
                        continue;

                    _seen.Add(enemy.Id);

                    if (IsRevealed(enemy))
                        _comets.Show(enemy, paused, delta);

                    continue;
                }

                _seen.Add(enemy.Id);

                if (!_visuals.TryGetValue(enemy.Id, out EnemyVisual visual))
                {
                    if (!IsRevealed(enemy))
                        continue;

                    visual = Create(enemy);
                    _visuals.Add(enemy.Id, visual);
                }

                if (collapsing)
                {
                    ApplyCollapse(visual, enemy);

                    if (visual.SpinSpeed != 0 && !paused)
                        visual.Container.Rotate(0, 0, visual.SpinSpeed * delta);

                    UpdateShade(visual, enemy);
                    continue;
                }

                AdvanceHold(visual, world.IsHeld(enemy), holdStep);
                visual.Container.localPosition = new Vector3(enemy.Position.X, enemy.Position.Y, 0) + HoldJitterOf(visual, enemy.Stats.Radius);

                // 일시정지 중에는 연출을 멈춘다. 다만 일시정지 중에 새로 생긴 적(테스트 도구 소환)은 크기 0으로 숨어 있지 않게 바로 제 크기로 보인다.
                if (visual.SpawnElapsed < SpawnSeconds)
                    AdvanceSpawn(visual, paused ? (visual.SpawnElapsed == 0 ? SpawnSeconds : 0) : delta);

                if (visual.Moon != null)
                    AdvanceMoon(visual, enemy, paused, delta);

                if (visual.Arms != null && !paused)
                    visual.Arms.Rotate(0, 0, visual.ArmSpin * delta);

                // 공전(이동)과 별개의 자전(소행성/행성/별만 SpinSpeed != 0). Container를 돌리므로 Hit의 펀치
                // 회전(Renderer.transform)과 서로 다른 트랜스폼이라 부딪히지 않는다.
                if (visual.SpinSpeed != 0 && !paused)
                    visual.Container.Rotate(0, 0, visual.SpinSpeed * delta);

                UpdateShade(visual, enemy);
            }

            // 이번 Step의 피격·사망 연출. 피격 흔들림은 아직 살아 있는 적에만 낸다(죽은 적의 스프라이트는 아래에서 지운다).
            ReadHits(world.Hits);
            ReadDeaths(world.Deaths);

            // 사망 파편의 흡입 스월, 떠오르는 텍스트 둘 다 개별 적이 아니라 공용 연출 하나를 매 프레임
            // 진행시키는 일이라, 위치 동기화와 같은 자리에서 한 번만 호출한다(따로 Update를 두지 않는다).
            _hitParticles.Advance(delta);
            _goldText.Age(delta);
            _damageText.Age(delta);
            _collapses.Advance(paused, delta);
            _gone.Clear();

            foreach (EnemyId id in _visuals.Keys)
            {
                if (!_seen.Contains(id))
                    _gone.Add(id);
            }

            foreach (EnemyId id in _gone)
            {
                EnemyVisual visual = _visuals[id];

                if (visual.Arms != null && _collapsing.Contains(id))
                    _collapses.Play(visual.Renderer.transform, visual.Arms, visual.ArmSpin);
                else
                    Release(visual);

                _visuals.Remove(id);
            }

            _collapsing.Clear();

            _comets.Retain(_seen);
        }

        // 아직 읽지 않은 피격 기록마다: 살아 있는 적이면 흔들림, 그리고 피격 파편과 피해량 텍스트. 픽업(혜성)은 연출이 없다.
        private void ReadHits(IReadOnlyList<HitRecord> hits)
        {
            // 설정은 프레임마다 한 번 읽는다(피격은 한 프레임에 많을 수 있다).
            bool particles = _settings.IsOn(GameSettings.MatterParticles);
            bool damageText = _settings.IsOn(GameSettings.ShowDamage);

            // 매 프레임 경로: IReadOnlyList를 인덱스로 돈다(인터페이스 foreach는 열거자를 할당한다).
            for (int i = 0; i < hits.Count; i++)
            {
                HitRecord hit = hits[i];

                if (hit.Sequence <= _lastHitSequence)
                    continue;

                _lastHitSequence = hit.Sequence;

                if (hit.IsPickup)
                    continue;

                if (_seen.Contains(hit.EnemyId) && _visuals.TryGetValue(hit.EnemyId, out EnemyVisual visual))
                    visual.Hit.Play();

                if (particles)
                    _hitParticles.EmitHit(new Vector3(hit.Position.X, hit.Position.Y, 0), _looks.ColorOf(hit.EnemyType, hit.Tier, hit.Tone), hit.Radius);

                if (damageText)
                    _damageText.Show(hit);
            }
        }

        // 아직 읽지 않은 사망 기록마다 사망 파편과 Gold 텍스트(픽업 제외). 새 사망이 하나라도 있으면 파괴음을 한 번 낸다(겹쳐 커지지 않게).
        private void ReadDeaths(IReadOnlyList<DeathRecord> deaths)
        {
            bool anyNew = false;
            bool particles = _settings.IsOn(GameSettings.MatterParticles);
            bool goldText = _settings.IsOn(GameSettings.ShowMoneyText);

            for (int i = 0; i < deaths.Count; i++)
            {
                DeathRecord death = deaths[i];

                if (death.Sequence <= _lastDeathSequence)
                    continue;

                _lastDeathSequence = death.Sequence;
                anyNew = true;

                if (death.IsPickup)
                    continue;

                if (particles)
                {
                    _hitParticles.EmitDeath(new Vector3(death.Position.X, death.Position.Y, 0),
                        _looks.ColorOf(death.EnemyType, death.Tier, death.Tone), death.EnemyType, death.Tier, death.Size, death.Radius);
                }

                if (goldText)
                    _goldText.Show(death);

                if (death.EnemyType == EnemyType.Galaxy)
                    _collapsing.Add(death.EnemyId);
            }

            if (anyNew)
                _sounds.RequestDestroyed();
        }

        // 판이 바뀌거나 판을 정리할 때 모든 적 스프라이트를 지운다. 정리는 처치가 아니므로 연출도 없다.
        public void Reset()
        {
            foreach (EnemyVisual visual in _visuals.Values)
                Release(visual);

            _visuals.Clear();
            _collapsing.Clear();
            _collapses.Reset();
            _comets.Reset();
            _hitParticles.Clear();
            _goldText.Reset();
            _damageText.Reset();
            RevealRadius = float.PositiveInfinity;
            Collapse = 0;
            _collapseReach = 0;

            // 새 판의 기록 번호는 1부터 다시 시작하므로 함께 되돌린다. 빠뜨리면 다음 판에서 연출이 나지 않는다.
            _lastHitSequence = 0;
            _lastDeathSequence = 0;
        }

        public void Dispose()
        {
            _collapses.Reset();
            _comets.Dispose();
            Object.Destroy(_root.gameObject);
            Object.Destroy(_quad);

            if (_shadeMaterial != null)
                Object.Destroy(_shadeMaterial);
        }

        private bool IsRevealed(Enemy enemy)
        {
            float x = enemy.Position.X;
            float y = enemy.Position.Y;
            return x * x + y * y <= RevealRadius * RevealRadius;
        }

        // 붕괴 중인 적 하나의 화면 자리·크기. 진행도 u(그 적의 출발 뒤)가 갈수록 빨라지며(u²) 반지름이 0으로, 각도는 공전 방향으로 감긴다.
        // 크기는 남은 반지름 비율만큼 줄고, 닿으면 숨긴다. 출현 연출 중이었으면 끝낸 것으로 본다. 달도 같은 자리·비율을 따른다.
        private void ApplyCollapse(EnemyVisual visual, Enemy enemy)
        {
            float x = enemy.Position.X;
            float y = enemy.Position.Y;
            float start = Mathf.Sqrt(x * x + y * y);
            float order = _collapseReach > 0 ? Mathf.Clamp01(start / _collapseReach) : 0;
            float u = Mathf.Clamp01((Collapse - CollapseStagger * order) / (1 - CollapseStagger));
            float pulled = u * u;
            float radius = start * (1 - pulled);
            float angle = Mathf.Atan2(y, x) + CollapseSwirl * (0.5f + order) * pulled;
            var position = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
            float scale = 1 - pulled;
            bool visible = pulled < 1;

            visual.SpawnElapsed = SpawnSeconds;
            visual.Container.localPosition = position;
            visual.Container.localScale = visual.BaseScale * scale;

            if (visual.Container.gameObject.activeSelf != visible)
                visual.Container.gameObject.SetActive(visible);

            if (visual.Moon != null)
            {
                Vector3 moon = visual.MoonBaseScale;
                visual.Moon.transform.localPosition = position;
                visual.Moon.transform.localScale = new Vector3(moon.x * scale, moon.y * scale, moon.z);
                visual.Moon.enabled = visible;
            }
        }

        // 원점에서 가장 먼 일반 적(픽업 제외) 중심까지의 거리. 없으면 0.
        private static float FarthestOf(IReadOnlyList<Enemy> enemies)
        {
            float farthest = 0;

            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].Definition.IsPickup)
                    continue;

                Point2 position = enemies[i].Position;
                farthest = Mathf.Max(farthest, position.X * position.X + position.Y * position.Y);
            }

            return Mathf.Sqrt(farthest);
        }

        // 스프라이트·달을 지운다. _visuals에서 빼는 것은 호출자가 한다.
        // 혜성은 여기에 없다 — 화면은 CometView가 지운다(Retain·Reset).
        private void Release(EnemyVisual visual)
        {
            Object.Destroy(visual.Container.gameObject);

            if (visual.Moon != null)
                Object.Destroy(visual.Moon.gameObject);
        }

        private EnemyVisual Create(Enemy enemy)
        {
            EnemyType kind = enemy.Definition.Type;
            EnemyTraitType? trait = enemy.Trait?.Type;
            string name = trait != null ? $"{kind} #{enemy.Id.Value} ({trait})" : $"{kind} #{enemy.Id.Value}";
            bool spins = kind == EnemyType.Asteroid || kind == EnemyType.Planet || kind == EnemyType.Star;

            // 자전하는 종류(소행성/행성/별)만 바깥에 별도 Container를 두고 그 안에 "Sprite"를 자식으로 둔다 —
            // Container(위치·크기·자전)와 Sprite(Hit 펀치 회전)가 서로 다른 트랜스폼이라 부딪히지 않는다.
            // 그 외 종류는 기존과 같은 평평한 구조를 유지한다(Container == Sprite 자신) — 은하의 붕괴 연출
            // (GalaxyCollapses)이 Renderer.transform의 게임오브젝트 하나만 파괴하므로, 감싸면 빈 부모가 남는다.
            GameObject view;
            Transform container;

            if (spins)
            {
                var containerObject = new GameObject(name);
                containerObject.transform.SetParent(_root, false);
                container = containerObject.transform;

                view = new GameObject("Sprite");
                view.transform.SetParent(container, false);
            }
            else
            {
                view = new GameObject(name);
                view.transform.SetParent(_root, false);
                container = view.transform;
            }

            var renderer = view.AddComponent<SpriteRenderer>();
            // 은하의 부모 스프라이트는 크기와 성질 표식의 기준이다. 실제 몸체는 자식의 완성 실루엣으로 그린다.
            renderer.sprite = kind == EnemyType.Galaxy ? _looks.GalaxyCore : _looks.SpriteOf(kind, enemy.Id.Value);
            renderer.color = _looks.ColorOf(kind, enemy.Tier, enemy.Tone);
            renderer.enabled = kind != EnemyType.Galaxy;

            // 크기는 규칙 수치(반지름)를 그대로 쓴다: 스프라이트의 긴 변이 지름이 되게 맞춘다.
            Vector3 bounds = renderer.sprite.bounds.size;
            float longest = Mathf.Max(bounds.x, bounds.y);
            container.localScale = Vector3.one * (enemy.Stats.Radius * 2 / longest);

            Color marker = _looks.TraitColorOf(kind, trait);
            SpriteRenderer traitFill = null;

            if (marker.a > 0)
            {
                var fill = new GameObject(trait.ToString());
                fill.transform.SetParent(view.transform, false);
                fill.transform.localScale = Vector3.one * EnemyLooks.TraitFillScale;

                var fillRenderer = fill.AddComponent<SpriteRenderer>();
                fillRenderer.sprite = renderer.sprite;
                fillRenderer.color = marker;
                fillRenderer.sortingOrder = renderer.sortingOrder + 1;
                traitFill = fillRenderer;
            }

            var visual = new EnemyVisual
            {
                Container = container,
                Renderer = renderer,
                Hit = new EnemyHitAnimation(renderer.transform),
                Body = renderer,
                BaseColor = renderer.color,
                HoldPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2),
                Fill = traitFill,
            };

            if (spins)
                visual.SpinSpeed = UnityEngine.Random.Range(SpinSpeedMin, SpinSpeedMax) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);

            if (trait == EnemyTraitType.Moon)
                CreateMoon(visual, enemy);

            if (kind == EnemyType.Galaxy)
            {
                CreateGalaxyArms(visual, enemy);
                visual.Body = visual.Arms.GetComponent<SpriteRenderer>();
            }

            // 체력 음영: 색을 내는 몸체(은하는 실루엣 팔)와 성질 속 채움에 음영 셰이더를 쓴다.
            // 중심·반지름은 스프라이트의 텍스처 좌표(UV)라 출현 때 한 번만 넣고, 이후엔 체력이 바뀔 때 문턱값만 고친다(UpdateShade).
            if (_shadeMaterial != null)
            {
                visual.Body.sharedMaterial = _shadeMaterial;
                InitShade(visual.Body, 1);

                // 성질 속 채움은 같은 스프라이트를 TraitFillScale로 줄인 자식이다. 반지름을 그만큼 키워 잡아야 경계선이 몸체와 같은 높이에 온다.
                if (visual.Fill != null)
                {
                    visual.Fill.sharedMaterial = _shadeMaterial;
                    InitShade(visual.Fill, 1 / EnemyLooks.TraitFillScale);
                }
            }

            // 출현 연출은 점에서 시작한다(AdvanceSpawn). 자식(성질 표식·은하 실루엣)은 부모 크기를 따라 함께 커진다.
            visual.BaseScale = container.localScale;
            container.localScale = Vector3.zero;

            if (visual.Moon != null)
            {
                visual.MoonBaseScale = visual.Moon.transform.localScale;
                visual.Moon.transform.localScale = new Vector3(0, 0, visual.MoonBaseScale.z);
            }

            return visual;
        }

        // 붙잡힘 표시의 세기를 목표(붙잡혔으면 1, 아니면 0)로 step만큼 옮기고, 바뀌었으면 색을 다시 쓴다.
        // 색은 색 등급 기본색에서 HoldTint 쪽으로 Hold × HoldTintStrength만큼 섞는다. 알파는 기본색의 것.
        private void AdvanceHold(EnemyVisual visual, bool held, float step)
        {
            float target = held ? 1 : 0;

            if (visual.Hold == target)
                return;

            visual.Hold = step <= 0 ? visual.Hold : Mathf.MoveTowards(visual.Hold, target, step);

            Color color = Color.Lerp(visual.BaseColor, _breakerLook.HoldTint, visual.Hold * _breakerLook.HoldTintStrength);
            color.a = visual.BaseColor.a;
            visual.Body.color = color;
        }

        // 붙잡힌 적의 제자리 떨림(월드 단위). Hold가 0이면 없다. 두 축의 주기를 조금 다르게 해 원을 그리지 않게 한다.
        private Vector3 HoldJitterOf(EnemyVisual visual, float radius)
        {
            if (visual.Hold <= 0)
                return Vector3.zero;

            float amplitude = _breakerLook.HoldJitter * radius * visual.Hold;
            float angle = _holdTime * _breakerLook.HoldJitterHz * Mathf.PI * 2 + visual.HoldPhase;
            return new Vector3(Mathf.Sin(angle) * amplitude, Mathf.Sin(angle * 1.37f + 1.1f) * amplitude, 0);
        }

        // 출현 연출을 delta만큼 진행한다. 끝나면 정확히 제 크기가 된다(OutBack(1) = 1).
        // 피격 흔들림은 회전만 바꾸므로 겹쳐도 된다. 연출 중에 죽은 은하는 그때 크기에서 붕괴를 시작한다(GalaxyCollapses).
        private static void AdvanceSpawn(EnemyVisual visual, float delta)
        {
            visual.SpawnElapsed = Mathf.Min(SpawnSeconds, visual.SpawnElapsed + delta);
            float scale = OutBack(visual.SpawnElapsed / SpawnSeconds);
            visual.Container.localScale = visual.BaseScale * scale;

            if (visual.Moon != null)
            {
                Vector3 moon = visual.MoonBaseScale;
                visual.Moon.transform.localScale = new Vector3(moon.x * scale, moon.y * scale, moon.z);
            }
        }

        // 체력 음영: 체력이 바뀐 프레임에만 문턱값을 다시 넣는다(평소 프레임에는 아무것도 하지 않는다).
        // 음영 면적 = 남은 체력 비율, 기본색 면적 = 잃은 체력 비율. 기본색 쪽은 몸체의 로컬 위쪽이고,
        // 셰이더가 스프라이트의 UV로 재므로 경계선은 자전·피격 흔들림·출현 크기 변화를 저절로 따라간다.
        private void UpdateShade(EnemyVisual visual, Enemy enemy)
        {
            if (_shadeMaterial == null)
                return;

            float ratio = Mathf.Clamp01(enemy.Health / enemy.Stats.MaxHealth);
            if (ratio == visual.ShadeRatio)
                return;

            visual.ShadeRatio = ratio;
            float threshold = ShadeThresholdOf(1 - ratio);
            SetShadeThreshold(visual.Body, threshold);

            if (visual.Fill != null)
                SetShadeThreshold(visual.Fill, threshold);
        }

        // 출현 때 한 번: 스프라이트 중심(bounds.center)과 반지름(긴 변의 절반 — Create가 크기를 맞추는 기준과 같다)을
        // 셰이더가 쓰는 UV 단위로 바꿔 넣는다. 로컬 y(단위) → 텍스처 픽셀 y = 스프라이트 원점 + pivot + y × PPU → ÷ 텍스처 높이.
        // 문턱값은 '전부 음영'으로 시작하고, 첫 Synchronize의 UpdateShade가 실제 체력으로 고친다.
        private void InitShade(SpriteRenderer renderer, float radiusScale)
        {
            Sprite sprite = renderer.sprite;
            Bounds bounds = sprite.bounds;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.y) * radiusScale;
            float pixelsPerUnit = sprite.pixelsPerUnit;
            float textureHeight = sprite.texture.height;

            float centerPixelY = SpriteOriginY(sprite) + sprite.pivot.y + bounds.center.y * pixelsPerUnit;
            float centerUv = centerPixelY / textureHeight;
            float radiusUv = radius * pixelsPerUnit / textureHeight;

            renderer.GetPropertyBlock(_shade);
            _shade.SetVector(_shadeCenterId, new Vector4(0, centerUv, 0, 0));
            _shade.SetFloat(_shadeRadiusId, Mathf.Max(radiusUv, 1e-6f));
            _shade.SetFloat(_shadeThresholdId, ShadeAllThreshold);
            renderer.SetPropertyBlock(_shade);
        }

        // 스프라이트 사각형의 아래쪽 끝이 텍스처에서 몇 번째 픽셀(y)인가. 아틀라스에 사각형으로 묶였으면 아틀라스 안 위치를 쓴다.
        // 아틀라스에 Tight(모양대로)로 묶인 스프라이트는 textureRect를 읽을 수 없어(예외) 원본 사각형으로 대신한다 —
        // 그 경우 경계 높이가 어긋날 수 있다(지금 적 스프라이트는 모두 코드로 만든 단독 텍스처라 해당 없음).
        private static float SpriteOriginY(Sprite sprite)
        {
            if (sprite.packed && sprite.packingMode == SpritePackingMode.Tight)
                return sprite.rect.y;

            return sprite.textureRect.y - sprite.textureRectOffset.y;
        }

        private void SetShadeThreshold(SpriteRenderer renderer, float threshold)
        {
            renderer.GetPropertyBlock(_shade);
            _shade.SetFloat(_shadeThresholdId, threshold);
            renderer.SetPropertyBlock(_shade);
        }

        // 기본색이 드러날 면적 비율(0 ~ 1) → 셰이더 문턱값 t(−1 ~ 1, 축 방향 거리 ÷ 반지름).
        // 단순히 t = 1 − 2·비율로 두면 원 모양에서 '높이' 비율이 되어 50%일 때만 맞고 10%·90%에서 면적이 크게 어긋난다.
        // 그래서 원에서 축 방향 t 바깥 활꼴의 면적 비율 (acos t − t·√(1 − t²)) / π 가 목표 비율이 되는 t를 이분법으로 구한다
        // (체력이 바뀐 프레임에만 계산한다). 적 도형은 대부분 원에 가까운 다각형이라 체감상 면적 비율과 맞는다.
        private static float ShadeThresholdOf(float brightFraction)
        {
            if (brightFraction <= 0)
                return ShadeAllThreshold;

            if (brightFraction >= 1)
                return -ShadeAllThreshold;

            float low = -1;
            float high = 1;

            for (int i = 0; i < 20; i++)
            {
                float t = (low + high) * 0.5f;
                float segment = (Mathf.Acos(t) - t * Mathf.Sqrt(1 - t * t)) / Mathf.PI;

                // 활꼴 면적은 t가 커질수록 줄어든다.
                if (segment > brightFraction)
                    low = t;
                else
                    high = t;
            }

            return (low + high) * 0.5f;
        }

        // 0 → 1로 가며 끝 무렵 1을 살짝 넘었다가 1에서 멈추는 곡선.
        private static float OutBack(float t)
        {
            float u = t - 1;
            return 1 + (SpawnOvershoot + 1) * u * u * u + SpawnOvershoot * u * u;
        }

        // 은하의 완성 실루엣: 중심과 팔을 함께 돌린다. 별도 중심 원이 윤곽을 덮지 않으며 성질 표식만 위에 겹친다.
        // 피격 흔들림은 부모를 돌리고, 자전과 붕괴 회전은 이 자식을 돌린다.
        private void CreateGalaxyArms(EnemyVisual visual, Enemy enemy)
        {
            var arms = new GameObject("Arms");
            arms.transform.SetParent(visual.Renderer.transform, false);
            arms.transform.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 360f));

            var armsRenderer = arms.AddComponent<SpriteRenderer>();
            armsRenderer.sprite = _looks.SpriteOf(EnemyType.Galaxy, enemy.Id.Value);
            armsRenderer.color = visual.Renderer.color;
            armsRenderer.sortingOrder = visual.Renderer.sortingOrder - 1;

            // 에셋 스프라이트로 바꿔도 중심과 같은 지름이 되게 맞춘다(중심 도형은 1 단위).
            Vector3 bounds = armsRenderer.sprite.bounds.size;
            float longest = Mathf.Max(bounds.x, bounds.y);
            Vector3 core = visual.Renderer.sprite.bounds.size;
            arms.transform.localScale = Vector3.one * (Mathf.Max(core.x, core.y) / longest);

            visual.Arms = arms.transform;
            visual.ArmSpin = -GalaxySpin * UnityEngine.Random.Range(GalaxySpinMin, GalaxySpinMax);
        }

        // 달은 출현 때 한 번 만든다. 적의 크기는 변하지 않으므로 궤도와 사각형 크기도 여기서 한 번만 정한다.
        // 외형은 Breaker 달과 같은 셰이더·머티리얼이다. 궤도는 적의 반지름 바로 바깥이다(Breaker는 링 바깥 가장자리).
        private void CreateMoon(EnemyVisual visual, Enemy enemy)
        {
            var moon = QuadRenderers.Create($"Moon #{enemy.Id.Value}", _root, _quad, _breakerLook.OrbMaterial, MoonSortingOrder);
            // 적(월드)에 붙어 돌지만 달 자체와 간격은 화면 크기가 고정이다: 전장 배율을 곱한다.
            float moonRadius = MoonRadius * _fieldScale;
            float orbitRadius = enemy.Stats.Radius + _breakerLook.OrbitOffset * _fieldScale + moonRadius;
            float size = 2 * (orbitRadius + moonRadius) * MeshMargin;
            moon.transform.localScale = new Vector3(size, size, 1);

            moon.GetPropertyBlock(_properties);
            _properties.SetFloat(_orbitRadiusId, orbitRadius);
            _properties.SetFloat(_orbRadiusId, moonRadius);
            _properties.SetFloat(_orbCountId, 1);
            _properties.SetColor(_moonFillId, _breakerLook.MoonFill);
            _properties.SetColor(_moonOutlineId, _breakerLook.MoonOutline);
            _properties.SetFloat(_outlineWidthId, _breakerLook.MoonOutlineWidth * _fieldScale);
            _properties.SetFloatArray(_orbKindsId, _moonKinds);
            moon.SetPropertyBlock(_properties);

            moon.enabled = true;
            visual.Moon = moon;
            visual.MoonPhase = UnityEngine.Random.value;
            visual.MoonSpeed = _breakerLook.OrbitSpeed * UnityEngine.Random.Range(MoonSpeedMin, MoonSpeedMax);
            moon.transform.localPosition = new Vector3(enemy.Position.X, enemy.Position.Y, 0);
            SetMoonPhase(visual);
        }

        private void AdvanceMoon(EnemyVisual visual, Enemy enemy, bool paused, float delta)
        {
            visual.Moon.transform.localPosition = new Vector3(enemy.Position.X, enemy.Position.Y, 0);

            if (paused || delta <= 0)
                return;

            // 시계방향으로 돌므로 위상이 줄어든다.
            visual.MoonPhase = Mathf.Repeat(visual.MoonPhase - visual.MoonSpeed * delta, 1);
            SetMoonPhase(visual);
        }

        private void SetMoonPhase(EnemyVisual visual)
        {
            visual.Moon.GetPropertyBlock(_properties);
            _properties.SetFloat(_phaseId, visual.MoonPhase);
            visual.Moon.SetPropertyBlock(_properties);
        }
    }
}
