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
    // 픽업(혜성)은 CometView가 통째로 맡는다(스프라이트가 아니라 셰이더). 사망 파편·골드·피해량 텍스트도 없다 — 한 방에 부서지는 버프 아이템이라 필요 없다.
    // 목록에서 빠진 적(사망)의 스프라이트는 바로 지운다. 파괴·흡수 연출은 연출 작업에서 사망 기록을 읽어 더한다.
    // 규칙 평면은 장면의 z = 0이고 x·y는 같다. HQ(원점)가 장면의 원점이다.
    internal sealed class EnemyView : IDisposable
    {
        // 적 하나의 화면 상태: 스프라이트와, 그 스프라이트에 적용되는 짧은 피격 흔들림 연출.
        // 모두 한 쌍으로 묶어 두면 EnemyId 하나당 여러 딕셔너리를 오가며 맞출 필요가 없다. 연출 등록을 풀 때 쓰려고 적도 같이 든다.
        private sealed class EnemyVisual
        {
            public Enemy Model;
            public SpriteRenderer Renderer;
            public EnemyHitAnimation Hit;
            // 달 성질이 붙은 적만 가진다(없으면 null). 적 스프라이트의 형제라 피격 흔들림·배율의 영향을 받지 않는다.
            public MeshRenderer Moon;
            // 달 공전의 위상(바퀴)과 속도(바퀴/초, 시계방향). 적마다 출현 때 한 번 정해진다.
            public float MoonPhase;
            public float MoonSpeed;
        }

        // 달 성질 ID. 달을 가진 적에는 달 하나가 공전한다(EnemyKind의 성질 ID와 같다).
        private const string MoonTrait = "moon";
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

        private readonly Transform _root;
        private readonly EnemyLooks _looks;
        private readonly BreakerLook _breakerLook;
        private readonly CometView _comets;
        private readonly Mesh _quad;
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private readonly float[] _moonKinds = new float[MaxOrbs];
        private readonly EnemyHitParticles _hitParticles;
        private readonly EnemyGoldText _goldText;
        private readonly EnemyDamageText _damageText;
        private readonly Dictionary<EnemyId, EnemyVisual> _visuals = new Dictionary<EnemyId, EnemyVisual>();
        private readonly HashSet<EnemyId> _seen = new HashSet<EnemyId>();
        private readonly List<EnemyId> _gone = new List<EnemyId>();

        private long _lastDeathSequence; // 마지막으로 소리를 낸 사망 기록의 번호.

        // breakerLook: 달의 외형(머티리얼·색·간격·기본 공전 속도)을 Breaker 달과 같게 맞추려고 받는다.
        // cometLook: 혜성(픽업)의 외형. 혜성의 화면은 CometView가 맡는다.
        public EnemyView(Transform parent, EnemyLooks looks, BreakerLook breakerLook, CometLook cometLook)
        {
            _root = new GameObject("Enemy View").transform;
            _root.SetParent(parent, false);
            _looks = looks;
            _breakerLook = breakerLook;
            _comets = new CometView(parent, cometLook);
            _quad = QuadRenderers.CreateMesh();
            _hitParticles = _root.gameObject.AddComponent<EnemyHitParticles>();
            _goldText = new EnemyGoldText(_root, _looks);
            _damageText = new EnemyDamageText(_root);
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

            // 매 프레임 경로: IReadOnlyList를 인덱스로 돈다(인터페이스 foreach는 열거자를 할당한다).
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                _seen.Add(enemy.Id);

                if (enemy.Definition.IsPickup)
                {
                    _comets.Show(enemy, paused, delta);
                    continue;
                }

                if (!_visuals.TryGetValue(enemy.Id, out EnemyVisual visual))
                {
                    visual = Create(enemy);
                    _visuals.Add(enemy.Id, visual);
                    enemy.Damaged += visual.Hit.Play;
                    _hitParticles.Register(enemy, _looks.ColorOf(enemy.Definition.Id, enemy.Tier));
                    _goldText.Register(enemy);
                    _damageText.Register(enemy);
                }

                visual.Renderer.transform.localPosition = new Vector3(enemy.Position.X, enemy.Position.Y, 0);
                visual.Hit.Advance(delta, visual.Renderer.transform);

                if (visual.Moon != null)
                    AdvanceMoon(visual, enemy, paused, delta);
            }

            // 사망 파편의 흡입 스월, 골드 획득 텍스트 둘 다 개별 적이 아니라 공용 연출 하나를 매 프레임
            // 진행시키는 일이라, 위치/피격 흔들림과 같은 자리에서 한 번만 호출한다(따로 Update를 두지 않는다).
            _hitParticles.Advance();
            _goldText.Age(delta);
            _damageText.Age(delta);
            _gone.Clear();

            foreach (EnemyId id in _visuals.Keys)
            {
                if (!_seen.Contains(id))
                    _gone.Add(id);
            }

            foreach (EnemyId id in _gone)
            {
                Release(_visuals[id]);
                _visuals.Remove(id);
            }

            _comets.Retain(_seen);
            ReadDeaths(world); // 이번 프레임에 새로 확정된 사망이 있으면 파괴음을 낸다.
        }

        // world.Deaths에서 아직 처리하지 않은 새 사망 기록(Sequence가 더 큰 것)이 있는지 확인하고 소리를 낸다.
        // 일시정지 중에는 판이 기록을 비우지 않아 같은 기록이 매 프레임 남아 있으므로, 번호로 걸러 한 번만 소리를 낸다.
        private void ReadDeaths(World world)
        {
            IReadOnlyList<DeathRecord> deaths = world.Deaths;

            // 이번 프레임에서 확인한 가장 큰 번호. 루프가 끝난 뒤 _lastDeathSequence에 반영한다.
            long latest = _lastDeathSequence;
            bool anynew = false;

            // 매 프레임 경로: IReadOnlyList를 인덱스로 돈다
            for (int i = 0; i < deaths.Count; i++)
            {
                long sequence = deaths[i].Sequence;

                if (sequence <= _lastDeathSequence) continue; // 이미 소리를 낸 기록이면 건너뛴다.

                if (sequence > latest) latest = sequence;

                anynew = true;

                // 한 프레임에 여러 마리가 죽어도 소리는 한 번만 낸다(소리가 겹쳐 커지는 것을 막는다).
                if (anynew) SoundManager.Instance?.RequestDestroyed();

                _lastDeathSequence = latest; // 다음 프레임에는 여기까지 처리한 기록을 건너뛴다.
            }
        }

        // 관리하는 적 스프라이트가 없고, 지운 객체도 장면에서 모두 사라졌는가.
        // 지운 객체는 프레임 끝에 사라지므로, Reset 뒤 한 프레임이 지나야 true가 된다.
        public bool IsClear => _visuals.Count == 0 && _root.childCount == 3
            && _comets.IsClear && _hitParticles.IsClear && _goldText.IsClear && _damageText.IsClear;

        // 판이 바뀌거나 판을 정리할 때 모든 적 스프라이트를 지운다. 정리는 처치가 아니므로 연출도 없다.
        public void Reset()
        {
            foreach (EnemyVisual visual in _visuals.Values)
                Release(visual);

            _visuals.Clear();
            _comets.Reset();
            _hitParticles.Clear();
            _goldText.Reset();
            _damageText.Reset();

            // 새 판의 사망 번호는 1부터 다시 시작하므로 함께 되돌린다. 빠뜨리면 다음 판에서 소리가 나지 않는다.
            _lastDeathSequence = 0;
        }

        public void Dispose()
        {
            _comets.Dispose();
            Object.Destroy(_root.gameObject);
            Object.Destroy(_quad);
        }

        // 적의 연출 등록(피격 흔들림·사망 파편·골드·피해량 텍스트)을 풀고 스프라이트·달을 지운다. _visuals에서 빼는 것은 호출자가 한다.
        // 혜성은 여기에 없다 — 화면은 CometView가 지운다(Retain·Reset).
        private void Release(EnemyVisual visual)
        {
            Enemy enemy = visual.Model;
            enemy.Damaged -= visual.Hit.Play;
            _hitParticles.Unregister(enemy);
            _goldText.Unregister(enemy);
            _damageText.Unregister(enemy);

            Object.Destroy(visual.Renderer.gameObject);

            if (visual.Moon != null)
                Object.Destroy(visual.Moon.gameObject);
        }

        private EnemyVisual Create(Enemy enemy)
        {
            string kind = enemy.Definition.Id;
            string trait = enemy.Trait?.Id;
            var view = new GameObject(trait != null ? $"{kind} #{enemy.Id.Value} ({trait})" : $"{kind} #{enemy.Id.Value}");
            view.transform.SetParent(_root, false);

            var renderer = view.AddComponent<SpriteRenderer>();
            renderer.sprite = _looks.SpriteOf(kind, enemy.Id.Value);
            renderer.color = _looks.ColorOf(kind, enemy.Tier);

            // 크기는 규칙 수치(반지름)를 그대로 쓴다: 스프라이트의 긴 변이 지름이 되게 맞춘다.
            Vector3 bounds = renderer.sprite.bounds.size;
            float longest = Mathf.Max(bounds.x, bounds.y);
            view.transform.localScale = Vector3.one * (enemy.Stats.Radius * 2 / longest);

            Color marker = _looks.TraitColorOf(kind, trait);

            if (marker.a > 0)
            {
                var fill = new GameObject(trait);
                fill.transform.SetParent(view.transform, false);
                fill.transform.localScale = Vector3.one * EnemyLooks.TraitFillScale;

                var fillRenderer = fill.AddComponent<SpriteRenderer>();
                fillRenderer.sprite = renderer.sprite;
                fillRenderer.color = marker;
                fillRenderer.sortingOrder = renderer.sortingOrder + 1;
            }

            var visual = new EnemyVisual { Model = enemy, Renderer = renderer, Hit = new EnemyHitAnimation() };

            if (trait == MoonTrait)
                CreateMoon(visual, enemy);

            return visual;
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
