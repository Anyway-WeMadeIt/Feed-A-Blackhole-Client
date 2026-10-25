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
    // 모든 종류가 같은 구조다: Container(위치·크기·자전) > 몸체 스프라이트(색·음영·피격 펀치) > [성질 표식]. 달은 형제.
    // 종류마다의 외형 결정(도형·자전·사망 연출·파편 속도)은 적 종류 에셋의 외형 칸(EnemyLook)이 가진다. 이 코드는 종류를 가르지 않는다.
    // 은하는 실루엣(몸체)이 자전하고(공전과 별개, 화면에서만), 죽으면 중심으로 말려 들어가며 사라진다(EnemyVanishes, DeathMotion.Spiral).
    // 혜성을 뺀 적은 출현할 때 점에서 시작해 제 크기보다 살짝 커졌다가 제 크기로 돌아온다(AdvanceSpawn). 일시정지 중에는 멈춘다.
    // 붙잡힌 적(World.IsHeld: 조준점이 있고 Breaker 링 안이라 거의 멈춘 적)은 색이 BreakerLook.HoldTint 쪽으로 밝아지고 제자리에서 잘게 떤다.
    //   켜지고 꺼지는 것은 HoldFadeSeconds 동안 부드럽게. 떨림은 Container의 위치에만 더하므로 피격 흔들림·자전과 부딪히지 않는다. 일시정지 중에는 멈춘다.
    // 판 시작 연출 동안에는 원점에서 RevealRadius 안의 적만 화면을 만든다. 반경이 넓어지면 바깥 적이 그때 출현 연출과 함께 나타난다(BattleSystem).
    // 픽업(혜성)은 CometView가 통째로 맡는다(스프라이트가 아니라 셰이더). 사망 파편·골드·피해량 텍스트도 없다 — 한 방에 부서지는 버프 아이템이라 필요 없다.
    // 목록에서 빠진 적(사망)의 스프라이트는 바로 지운다.
    // 붕괴(엔딩, Collapse > 0): 판이 끝나 멈춘 적들이 소용돌이치며 원점(블랙홀)으로 빨려 들어가고, 가까워질수록 작아져 사라진다.
    //   가까운 적이 먼저, 먼 적은 늦게 출발해 같은 시간 안에 모두 닿는다. 혜성은 곧바로 지운다. 화면만이다(BattleSystem이 0 → 1로 흘린다).
    // 체력 음영: 몸체(와 성질 속 채움)는 '남은 체력 비율'만큼의 면적에 음영(검정 α ShadeStrength를 덮은 색)이 씌워져 있고,
    //   피해를 입은 만큼 몸체의 한쪽(로컬 위)부터 기본색이 드러나며, 경계선은 자전과 함께 돈다(BlackHole/Enemy Shade 셰이더, EnemyShade). 체력 50%면 절반은 기본색, 절반은 음영.
    // 피격·사망 연출(흔들림·파편·피해량·Gold 텍스트·파괴음)은 Step이 끝난 뒤 판의 피격 기록(World.Hits)과 사망 기록(World.Deaths)을 읽어 낸다.
    // 일시정지 중에는 판이 기록을 비우지 않으므로, 기록마다 번호(Sequence)로 걸러 한 번씩만 낸다.
    // 규칙 평면은 장면의 z = 0이고 x·y는 같다. HQ(원점)가 장면의 원점이다.
    internal sealed class EnemyView : IDisposable
    {
        // 출현 연출 [임시]: SpawnSeconds 동안 크기 0(점)에서 제 크기로 키운다. OutBack 곡선이라 도중에 제 크기를 살짝 넘었다가 돌아온다.
        private const float SpawnSeconds = 0.35f;
        // OutBack 곡선의 넘침 정도. 1.70158이면 가장 클 때 제 크기의 약 1.1배다. 키우면 더 크게 튀었다가 돌아온다.
        private const float SpawnOvershoot = 1.70158f;

        // 이 판의 전장 배율(Hq.FieldScale). 화면 크기가 고정인 달과 떠오르는 숫자에 곱한다. Synchronize가 매번 읽는다.
        private float _fieldScale = HqGrowthDefinition.StartFieldScale;

        private readonly Transform _root;
        private readonly EnemyLooks _looks;
        // 표시 설정(피해량·Gold 텍스트, 물질 파편). 끄면 그다음 기록부터 내지 않는다 — 이미 떠 있는 것은 제 수명대로 사라진다.
        private readonly GameSettings _settings;
        private readonly BreakerLook _breakerLook;
        private readonly CometView _comets;
        // 공용 처리기: 달 성질의 달, 체력 음영. 상태는 EnemyVisual에 있다.
        private readonly EnemyMoons _moons;
        private readonly EnemyShade _shade;
        private readonly EnemyHitParticles _hitParticles;
        private readonly EnemyGoldText _goldText;
        private readonly BattleSounds _sounds;
        private readonly EnemyDamageText _damageText;
        private readonly Dictionary<EnemyId, EnemyVisual> _visuals = new Dictionary<EnemyId, EnemyVisual>();
        // 붙잡힘 떨림이 흐르는 시간(초). 일시정지 중에는 멈춘다.
        private float _holdTime;
        private readonly HashSet<EnemyId> _seen = new HashSet<EnemyId>();
        private readonly List<EnemyId> _gone = new List<EnemyId>();
        // 이번 프레임에 사망 기록으로 죽음을 안, 소멸 연출(DeathMotion.Spiral)이 있는 적. 화면을 지우는 대신 소멸 연출로 넘긴다.
        private readonly HashSet<EnemyId> _collapsing = new HashSet<EnemyId>();
        private readonly EnemyVanishes _vanishes = new EnemyVanishes();

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
            _moons = new EnemyMoons(_root, breakerLook);
            _hitParticles = _root.gameObject.AddComponent<EnemyHitParticles>();
            _goldText = new EnemyGoldText(_root);
            _sounds = sounds;
            _damageText = new EnemyDamageText(_root);
            _shade = new EnemyShade();
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

                    _shade.Update(visual, enemy);
                    continue;
                }

                AdvanceHold(visual, world.IsHeld(enemy), holdStep);
                visual.Container.localPosition = new Vector3(enemy.Position.X, enemy.Position.Y, 0) + HoldJitterOf(visual, enemy.Stats.Radius);

                // 일시정지 중에는 연출을 멈춘다. 다만 일시정지 중에 새로 생긴 적(테스트 도구 소환)은 크기 0으로 숨어 있지 않게 바로 제 크기로 보인다.
                if (visual.SpawnElapsed < SpawnSeconds)
                    AdvanceSpawn(visual, paused ? (visual.SpawnElapsed == 0 ? SpawnSeconds : 0) : delta);

                if (visual.Moon != null)
                    _moons.Follow(visual, enemy.Position, paused, delta);

                // 공전(이동)과 별개의 자전. Container를 돌리므로 Hit의 펀치 회전(Renderer.transform)과 서로 다른 트랜스폼이라 부딪히지 않는다.
                if (visual.SpinSpeed != 0 && !paused)
                    visual.Container.Rotate(0, 0, visual.SpinSpeed * delta);

                _shade.Update(visual, enemy);
            }

            // 이번 Step의 피격·사망 연출. 피격 흔들림은 아직 살아 있는 적에만 낸다(죽은 적의 스프라이트는 아래에서 지운다).
            ReadHits(world.Hits);
            ReadDeaths(world.Deaths);

            // 사망 파편의 흡입 스월, 떠오르는 텍스트 둘 다 개별 적이 아니라 공용 연출 하나를 매 프레임
            // 진행시키는 일이라, 위치 동기화와 같은 자리에서 한 번만 호출한다(따로 Update를 두지 않는다).
            _hitParticles.Advance(delta);
            _goldText.Age(delta);
            _damageText.Age(delta);
            _vanishes.Advance(paused, delta);
            _gone.Clear();

            foreach (EnemyId id in _visuals.Keys)
            {
                if (!_seen.Contains(id))
                    _gone.Add(id);
            }

            foreach (EnemyId id in _gone)
            {
                EnemyVisual visual = _visuals[id];

                if (_collapsing.Contains(id))
                    _vanishes.Play(visual.Container, visual.SpinSpeed);
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

                EnemyLook look = _looks.LookOf(death.EnemyType);

                if (particles)
                {
                    _hitParticles.EmitDeath(new Vector3(death.Position.X, death.Position.Y, 0),
                        _looks.ColorOf(death.EnemyType, death.Tier, death.Tone), look.debrisSuction, death.Tier, death.Size, death.Radius);
                }

                if (goldText)
                    _goldText.Show(death, _settings.NumberFormatValue);

                if (look.deathMotion == DeathMotion.Spiral)
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
            _vanishes.Reset();
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
            _vanishes.Reset();
            _comets.Dispose();
            Object.Destroy(_root.gameObject);
            _moons.Dispose();
            _shade.Dispose();
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
                _moons.Place(visual, position, scale, visible);
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
                _moons.Destroy(visual);
        }

        private EnemyVisual Create(Enemy enemy)
        {
            EnemyType kind = enemy.Definition.Type;
            EnemyTraitType? trait = enemy.Trait?.Type;
            string name = trait != null ? $"{kind} #{enemy.Id.Value} ({trait})" : $"{kind} #{enemy.Id.Value}";
            EnemyLook look = _looks.LookOf(kind);

            // 바깥에 Container를 두고 그 안에 "Sprite"(몸체)를 자식으로 둔다 — Container(위치·크기·자전)와 Sprite(Hit 펀치 회전)가
            // 서로 다른 트랜스폼이라 부딪히지 않는다. 모든 종류가 같다.
            var containerObject = new GameObject(name);
            containerObject.transform.SetParent(_root, false);
            Transform container = containerObject.transform;

            var view = new GameObject("Sprite");
            view.transform.SetParent(container, false);

            var renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = _looks.SpriteOf(kind, enemy.Id.Value);
            renderer.color = _looks.ColorOf(kind, enemy.Tier, enemy.Tone);

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

                // 표식 모양: 외형 칸이 정한다(기본은 몸체와 같은 스프라이트. 은하는 실루엣이 아니라 중심 원).
                var fillRenderer = fill.AddComponent<SpriteRenderer>();
                fillRenderer.sprite = _looks.MarkerSpriteOf(kind, renderer.sprite, enemy.Id.Value);
                fillRenderer.color = marker;
                fillRenderer.sortingOrder = renderer.sortingOrder + 1;
                traitFill = fillRenderer;
            }

            var visual = new EnemyVisual
            {
                Container = container,
                Renderer = renderer,
                Hit = new EnemyHitAnimation(renderer.transform),
                BaseColor = renderer.color,
                HoldPhase = UnityEngine.Random.Range(0f, Mathf.PI * 2),
                Fill = traitFill,
            };

            // 자전: 외형 칸의 범위에서 속도를 고르고, 방향은 Random이면 반반(소행성·행성·별), 아니면 고정(은하 시계방향).
            if (look.spinSpeedMax > 0)
            {
                float speed = UnityEngine.Random.Range(look.spinSpeedMin, look.spinSpeedMax);
                float sign = look.spinDirection switch
                {
                    SpinDirection.Clockwise => -1f,
                    SpinDirection.CounterClockwise => 1f,
                    _ => UnityEngine.Random.value < 0.5f ? -1f : 1f,
                };
                visual.SpinSpeed = speed * sign;
            }

            if (trait == EnemyTraitType.Moon)
                _moons.Create(visual, enemy, _fieldScale);

            // 방향이 있는 모양(은하)은 시작 각도를 무작위로.
            if (look.randomStartAngle)
                container.localRotation = Quaternion.Euler(0, 0, UnityEngine.Random.Range(0f, 360f));

            // 체력 음영: 몸체와 성질 속 채움에 음영 셰이더를 쓴다. 이후엔 체력이 바뀔 때 문턱값만 고친다(EnemyShade.Update).
            _shade.Attach(visual);

            // 출현 연출은 점에서 시작한다(AdvanceSpawn). 자식(성질 표식·은하 실루엣)은 부모 크기를 따라 함께 커진다.
            visual.BaseScale = container.localScale;
            container.localScale = Vector3.zero;

            if (visual.Moon != null)
                _moons.BeginSpawn(visual);

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
            visual.Renderer.color = color;
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
        // 피격 흔들림은 회전만 바꾸므로 겹쳐도 된다. 연출 중에 죽은 은하는 그때 크기에서 소멸을 시작한다(EnemyVanishes).
        private static void AdvanceSpawn(EnemyVisual visual, float delta)
        {
            visual.SpawnElapsed = Mathf.Min(SpawnSeconds, visual.SpawnElapsed + delta);
            float scale = OutBack(visual.SpawnElapsed / SpawnSeconds);
            visual.Container.localScale = visual.BaseScale * scale;

            if (visual.Moon != null)
                _moons.SetScale(visual, scale);
        }

        // 0 → 1로 가며 끝 무렵 1을 살짝 넘었다가 1에서 멈추는 곡선.
        private static float OutBack(float t)
        {
            float u = t - 1;
            return 1 + (SpawnOvershoot + 1) * u * u * u + SpawnOvershoot * u * u;
        }
    }
}
