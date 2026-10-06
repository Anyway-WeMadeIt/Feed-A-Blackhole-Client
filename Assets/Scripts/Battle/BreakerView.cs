using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // Breaker의 화면. 매 프레임 판의 참가자를 읽어 참가자마다 점선 링 하나를 조준점에 그린다.
    // 게임 상태를 바꾸지 않고, 피해를 다시 계산하지 않는다. 외형은 BreakerLook이 가진다.
    // - 링의 기본 반지름은 판정 반지름이다(GAME_RULES 6절). 달 버프로 판정 반지름이 바뀌면 매 프레임 따라간다(BreakerSkill.CurrentRadius).
    //   조준점이 없거나 일시정지 중이면 숨긴다.
    // - 기본: 천천히 돈다.
    // - 타격: Tick 기록마다 링이 빠르게 커졌다 돌아온다(반지름·굵기·밝기). 커지는 반지름은 화면에서만 커진다.
    //   헛친 Tick(맞은 적 없음)은 약하게 튄다. 치명타 Tick은 금색으로 번쩍인다. 빈 Tick(조준점 없음)은 튀지 않는다.
    // - 버프 구체: 달·혜성 중첩 하나마다 구체 하나가 링 바깥 궤도를 시계방향으로 돈다. 두 버프가 한 궤도를 나눠 쓰고,
    //   궤도를 중첩 수만큼 균등하게 나눈 자리에 받은 순서(BreakerBuff.Number)대로 놓인다. 중첩이 바뀌면 곧바로 새 자리로 옮긴다.
    //   구체 크기는 화면에서 고정이다(저작 값 × 판의 전장 배율, 원작 달은 카메라와 무관하게 같은 크기). 궤도는 화면의 링(튐 포함) 바로 바깥을 따라간다.
    //   표시는 64개까지다.
    // - 링 굵기는 반지름에 비례한다(BreakerLook.ThicknessRatio). 링 자체는 판정 반지름 그대로라 카메라가 넓어지면 적과 함께 작아 보인다.
    // - 혜성 배경 원: 혜성 중첩이 하나라도 있으면 링을 덮는 반투명 무지개 원을 적 위, 링 아래에 그린다. 곧바로 켜고 끈다.
    // - 일시정지 중에는 회전과 튀김을 멈추고, 재개하면 이어간다.
    // 정지 중에는 판이 기록을 비우지 않으므로, 이미 본 Tick은 번호로 걸러 두 번 튀지 않는다.
    internal sealed class BreakerView : IDisposable
    {
        private const int SortingOrder = 10;
        // 혜성 배경 원은 적(0 ~ 1) 위, 링 아래에 그린다.
        private const int AuraSortingOrder = 5;
        // 구체는 링 위에 그린다.
        private const int OrbSortingOrder = 11;
        // 셰이더(BlackHole/Breaker Orbs)의 MAX_ORBS와 같다.
        private const int MaxOrbs = 64;
        private const float MoonKind = 0;
        private const float CometKind = 1;
        // 사각형이 링의 바깥 가장자리까지 담도록 두는 여유 비율.
        private const float MeshMargin = 1.1f;

        private static readonly int _radiusId = Shader.PropertyToID("_Radius");
        private static readonly int _thicknessId = Shader.PropertyToID("_Thickness");
        private static readonly int _dashCountId = Shader.PropertyToID("_DashCount");
        private static readonly int _dashRatioId = Shader.PropertyToID("_DashRatio");
        private static readonly int _rotationId = Shader.PropertyToID("_Rotation");
        private static readonly int _colorId = Shader.PropertyToID("_Color");
        private static readonly int _flashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int _flashId = Shader.PropertyToID("_Flash");
        private static readonly int _orbitRadiusId = Shader.PropertyToID("_OrbitRadius");
        private static readonly int _orbRadiusId = Shader.PropertyToID("_OrbRadius");
        private static readonly int _orbCountId = Shader.PropertyToID("_OrbCount");
        private static readonly int _phaseId = Shader.PropertyToID("_Phase");
        private static readonly int _moonFillId = Shader.PropertyToID("_MoonFill");
        private static readonly int _moonOutlineId = Shader.PropertyToID("_MoonOutline");
        private static readonly int _outlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int _orbKindsId = Shader.PropertyToID("_OrbKinds");
        private static readonly int _auraRadiusId = Shader.PropertyToID("_Radius");

        private readonly Transform _root;
        private readonly BreakerLook _look;
        private readonly Mesh _quad;
        private readonly MaterialPropertyBlock _properties = new();
        // 칸마다 구체 종류(MoonKind·CometKind). 매 프레임 채워 셰이더에 넘긴다(길이는 늘 MaxOrbs).
        private readonly float[] _orbKinds = new float[MaxOrbs];
        private readonly Dictionary<PlayerId, Ring> _rings = new();

        // 참가자 하나의 링과 버프 구체. Root는 조준점에 놓이고 크기를 바꾸지 않는다 — 링과 구체의 사각형은 각자 크기를 맞춘다.
        private sealed class Ring
        {
            public Transform Root;
            public MeshRenderer Renderer;
            public MeshRenderer Orbs;
            public MeshRenderer Aura;
            public float Rotation;
            // 구체 궤도의 위상(바퀴). 시계방향으로 돌므로 줄어든다.
            public float OrbPhase;
            // 사각형의 지금 한 변(월드 단위). 바뀔 때만 transform에 쓴다.
            public float Size;
            public float OrbSize;
            public float AuraSize;
            public int DrawnTick;
            // 튐이 시작된 뒤 지난 시간. 튐 시간 이상이면 튀지 않는 중이다.
            public float PunchElapsed = float.MaxValue;
            public float PunchStrength;
            public bool PunchCritical;
            // 마지막으로 획득음을 낸 버프 번호
            public int SoundedBuff;
        }

        public BreakerView(Transform parent, BreakerLook look)
        {
            _root = new GameObject("Breaker View").transform;
            _root.SetParent(parent, false);
            _look = look;
            _quad = QuadRenderers.CreateMesh();
        }

        public void Synchronize(World world, bool paused, float delta)
        {
            IReadOnlyList<BattlePlayer> players = world.Players;
            float fieldScale = world.Hq.FieldScale;

            // 매 프레임 경로: IReadOnlyList를 인덱스로 돈다(인터페이스 foreach는 열거자를 할당한다).
            for (int i = 0; i < players.Count; i++)
            {
                BattlePlayer player = players[i];

                if (player.Breaker != null)
                    Show(player, player.Breaker, paused, delta, fieldScale);
            }
        }

        // 링이 없고, 지운 객체도 장면에서 모두 사라졌는가.
        // 지운 객체는 프레임 끝에 사라지므로, Reset 뒤 한 프레임이 지나야 true가 된다.
        public bool IsClear => _rings.Count == 0 && _root.childCount == 0;

        // 판이 바뀌거나 판을 정리할 때 모든 링을 지운다.
        public void Reset()
        {
            foreach (Ring ring in _rings.Values)
                Object.Destroy(ring.Root.gameObject);

            _rings.Clear();
        }

        public void Dispose()
        {
            Object.Destroy(_root.gameObject);
            Object.Destroy(_quad);
        }

        // fieldScale: 판의 전장 배율. 화면 크기가 고정인 구체에만 곱한다.
        private void Show(BattlePlayer player, BreakerSkill breaker, bool paused, float delta, float fieldScale)
        {
            if (!_rings.TryGetValue(player.Id, out Ring ring))
            {
                ring = Create(player.Id);
                _rings.Add(player.Id, ring);
            }

            if (!paused)
            {
                ring.Rotation = Mathf.Repeat(ring.Rotation + _look.RotationSpeed * delta, 1);
                ring.OrbPhase = Mathf.Repeat(ring.OrbPhase - _look.OrbitSpeed * delta, 1);
                ring.PunchElapsed += delta;
            }

            ReadTicks(ring, breaker);
            ReadBuffs(ring, breaker);

            // 링·구체·혜성 배경 원의 표시 여부는 여기서만 정한다. 구체와 배경 원은 링이 보일 때만 보인다.
            bool visible = !paused && player.AimPoint.HasValue;
            int orbCount = visible ? FillOrbKinds(breaker) : 0;
            bool aura = visible && breaker.IsGuaranteedCritical;
            ring.Renderer.enabled = visible;
            ring.Orbs.enabled = orbCount > 0;
            ring.Aura.enabled = aura;

            if (!visible)
                return;

            Point2 aim = player.AimPoint.Value;
            ring.Root.localPosition = new Vector3(aim.X, aim.Y, 0);
            Apply(ring, breaker.CurrentRadius, out float shownRadius, out float shownThickness);

            float ringEdge = shownRadius + shownThickness * 0.5f;

            if (orbCount > 0)
                ApplyOrbs(ring, orbCount, ringEdge, fieldScale);

            if (aura)
                ApplyAura(ring, ringEdge);
        }

        // 새 Tick이 있으면 튐을 처음부터 다시 한다. 한 프레임에 여러 Tick이면 가장 센 튐으로, 하나라도 치명타면 금색으로 튄다.
        private void ReadTicks(Ring ring, BreakerSkill breaker)
        {
            IReadOnlyList<BreakerTick> ticks = breaker.Ticks;
            bool punched = false;

            for (int i = 0; i < ticks.Count; i++)
            {
                BreakerTick tick = ticks[i];

                if (tick.Number <= ring.DrawnTick)
                    continue;

                ring.DrawnTick = tick.Number;

                if (!tick.Center.HasValue)
                    continue;

                // 맞췄으면 타격음, 못 맞췄으면 헛침 소리
                if (tick.HitCount > 0) SoundManager.Instance?.RequestHitSound();
                else SoundManager.Instance.PlayWhiff();

                    float strength = tick.HitCount > 0 ? 1 : _look.MissStrength;

                if (!punched)
                {
                    punched = true;
                    ring.PunchElapsed = 0;
                    ring.PunchStrength = strength;
                    ring.PunchCritical = tick.IsCritical;
                }
                else
                {
                    ring.PunchStrength = Mathf.Max(ring.PunchStrength, strength);
                    ring.PunchCritical |= tick.IsCritical;
                }
            }
        }

        // radius: 판정 반지름(달 버프 포함). 튐으로 커지는 반지름은 화면에서만 커진다.
        // 링의 화면 반지름·굵기(튐 포함)를 돌려준다. 구체 궤도가 이 링 바로 바깥을 따라간다.
        private void Apply(Ring ring, float radius, out float shownRadius, out float shownThickness)
        {
            float punch = ring.PunchElapsed < _look.PunchDuration
                ? _look.Punch(ring.PunchElapsed / _look.PunchDuration) * ring.PunchStrength
                : 0;
            shownRadius = radius * (1 + _look.PunchRadiusScale * punch);
            shownThickness = radius * _look.ThicknessRatio * (1 + _look.PunchThicknessScale * punch);

            // 셰이더는 링 중심에서의 월드 거리로 그리므로, 사각형 크기는 링의 크기가 아니라 셰이더가 도는 픽셀 범위만 정한다.
            // 그릴 링이 딱 들어가게 맞춘다. 대부분의 프레임은 크기가 그대로라 쓰지 않는다.
            float size = 2 * (shownRadius + shownThickness * 0.5f) * MeshMargin;

            if (size != ring.Size)
            {
                ring.Size = size;
                ring.Renderer.transform.localScale = new Vector3(size, size, 1);
            }

            ring.Renderer.GetPropertyBlock(_properties);
            _properties.SetFloat(_radiusId, shownRadius);
            _properties.SetFloat(_thicknessId, shownThickness);
            _properties.SetFloat(_dashCountId, _look.DashCount);
            _properties.SetFloat(_dashRatioId, _look.DashRatio);
            _properties.SetFloat(_rotationId, ring.Rotation);
            _properties.SetColor(_colorId, _look.Color);
            _properties.SetColor(_flashColorId, ring.PunchCritical ? _look.CriticalFlashColor : _look.HitFlashColor);
            _properties.SetFloat(_flashId, Mathf.Clamp01(punch));
            ring.Renderer.SetPropertyBlock(_properties);
        }

        // 버프 중첩을 받은 순서(Number)대로 _orbKinds 칸에 채우고, 채운 칸 수(표시할 구체 수, 최대 MaxOrbs)를 돌려준다.
        // 두 목록은 각자 받은 순서라 앞에서부터 번호를 비교해 합친다.
        private int FillOrbKinds(BreakerSkill breaker)
        {
            IReadOnlyList<BreakerBuff> moon = breaker.MoonBuffs;
            IReadOnlyList<BreakerBuff> comet = breaker.CometBuffs;
            int count = 0;
            int m = 0;
            int c = 0;

            while (count < MaxOrbs && (m < moon.Count || c < comet.Count))
            {
                bool takeMoon = c == comet.Count || (m < moon.Count && moon[m].Number < comet[c].Number);
                _orbKinds[count++] = takeMoon ? MoonKind : CometKind;

                if (takeMoon)
                    m++;
                else
                    c++;
            }

            return count;
        }

        // count: FillOrbKinds가 채운 칸 수(1 이상). ringEdge: 화면의 링 바깥 가장자리(링 반지름 + 굵기/2).
        // fieldScale: 판의 전장 배율. 구체 크기·간격·테두리에 곱해 화면에서 같은 크기로 보이게 한다.
        private void ApplyOrbs(Ring ring, int count, float ringEdge, float fieldScale)
        {
            float orbRadius = _look.OrbRadius * fieldScale;
            float orbitRadius = ringEdge + _look.OrbitOffset * fieldScale + orbRadius;
            float size = 2 * (orbitRadius + orbRadius) * MeshMargin;

            if (size != ring.OrbSize)
            {
                ring.OrbSize = size;
                ring.Orbs.transform.localScale = new Vector3(size, size, 1);
            }

            ring.Orbs.GetPropertyBlock(_properties);
            _properties.SetFloat(_orbitRadiusId, orbitRadius);
            _properties.SetFloat(_orbRadiusId, orbRadius);
            _properties.SetFloat(_orbCountId, count);
            _properties.SetFloat(_phaseId, ring.OrbPhase);
            _properties.SetColor(_moonFillId, _look.MoonFill);
            _properties.SetColor(_moonOutlineId, _look.MoonOutline);
            _properties.SetFloat(_outlineWidthId, _look.MoonOutlineWidth * fieldScale);
            _properties.SetFloatArray(_orbKindsId, _orbKinds);
            ring.Orbs.SetPropertyBlock(_properties);
        }

        // ringEdge: 화면의 링 바깥 가장자리(링 반지름 + 굵기/2). 원은 튐을 따라 링과 함께 커졌다 돌아온다.
        private void ApplyAura(Ring ring, float ringEdge)
        {
            float radius = ringEdge + _look.CometAuraOffset;
            float size = 2 * radius * MeshMargin;

            if (size != ring.AuraSize)
            {
                ring.AuraSize = size;
                ring.Aura.transform.localScale = new Vector3(size, size, 1);
            }

            ring.Aura.GetPropertyBlock(_properties);
            _properties.SetFloat(_auraRadiusId, radius);
            ring.Aura.SetPropertyBlock(_properties);
        }

        // 사각형 크기는 그릴 때마다 Apply·ApplyOrbs·ApplyAura가 맞춘다.
        private Ring Create(PlayerId player)
        {
            Transform root = new GameObject($"Breaker ({player})").transform;
            root.SetParent(_root, false);

            return new Ring
            {
                Root = root,
                Renderer = CreateQuadRenderer("Ring", root, _look.Material, SortingOrder),
                Orbs = CreateQuadRenderer("Buff Orbs", root, _look.OrbMaterial, OrbSortingOrder),
                Aura = CreateQuadRenderer("Comet Aura", root, _look.CometAuraMaterial, AuraSortingOrder),
            };
        }

        private void ReadBuffs(Ring ring, BreakerSkill breaker)
        {
            int latest = ring.SoundedBuff;

            IReadOnlyList<BreakerBuff> moon = breaker.MoonBuffs;
            for (int i = 0; i < moon.Count; i++)
            {
                if (moon[i].Number > latest) latest = moon[i].Number;
            }

            IReadOnlyList<BreakerBuff> comet = breaker.CometBuffs;
            for (int i = 0; i <comet.Count; i++)
            {
                if (comet[i].Number > latest) latest = comet[i].Number;
            }

            if (latest > ring.SoundedBuff)
            {
                SoundManager.Instance?.PlayMoonComet();
                ring.SoundedBuff = latest;
            }
        }

        private MeshRenderer CreateQuadRenderer(string name, Transform parent, Material material, int sortingOrder) =>
            QuadRenderers.Create(name, parent, _quad, material, sortingOrder);
    }
}
