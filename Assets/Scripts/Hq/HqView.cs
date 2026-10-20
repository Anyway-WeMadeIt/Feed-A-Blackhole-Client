using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BlackHole.Unity
{
    // 블랙홀(HQ)의 화면. 판 동안 씬의 블랙홀 오브젝트(검은 중심 원·배경 소용돌이)를 켜고 판의 Level에 맞춰 키운다. 게임 상태를 바꾸지 않는다.
    // 크기는 판 Level이 정한다. Level이 오르면 성장 공급의 바퀴(GrowthPacer)를 따라 한 바퀴(LevelGaugeSeconds)마다 다음 Level의 크기로 커지고,
    // 바퀴가 끝나는 순간(보여줄 Level이 오르는 순간) 바깥으로 한 번 번쩍인다. 그림일 뿐이다 — 출현 띠·공전·Breaker와 무관하다(GAME_RULES 3.1).
    // 보여줄 Level = Hq.Level − 아직 안 돈 바퀴 수. 이정표에 닿아 바퀴가 없으면 바로 그 Level로 간다.
    // 크기는 원작 실측(검은 중심의 C0 화면 지름)을 Level에 맞춘 것이다: Level 0 → 12px, 10 → 47px, 20 → 86px, 30 → 158px, 35 → 232px,
    // 40 → 약 292px(그 구간 카메라 배율이 추정값이라 오차 약 ±12%).
    // 1 unit = C0 54px(카메라 크기 10)로 바꾼 반지름을 Level 사이에서 직선으로 잇고, 마지막 점 뒤는 그대로 둔다.
    // 블랙홀 오브젝트는 검은 중심 원의 반지름이 이 값이 되도록 처음 크기에 비례해 키운다(배경 소용돌이도 같은 비율).
    // 번쩍임 선 굵기는 화면에서 고정이라 판의 전장 배율을 곱한다(오브젝트는 월드 크기라 카메라가 넓어지면 작아 보인다).
    // 붕괴(엔딩, Collapse 0 → 1): 적을 빨아들이는 동안 블랙홀이 CollapseGrowth배까지 커지고, 시작할 때 충격파, 다 빨아들이면 크게 한 번 번쩍인다.
    //   Black Hole Pulse 설정과 무관하게 낸다(엔딩의 마무리라서).
    internal sealed class HqView : IDisposable
    {
        // (Level, 반지름) 실측점. Level이 커지는 순서.
        private static readonly (int Level, float Radius)[] MeasuredRadii =
        {
            (0, 0.111f),
            (10, 0.435f),
            (20, 0.796f),
            (30, 1.463f),
            (40, 2.148f),
            (50, 2.704f),
        };

        // 블랙홀 오브젝트의 검은 중심 원을 찾지 못했을 때 쓰는 처음 크기의 반지름(Circle 스프라이트 지름 1 × 0.8 ÷ 2).
        private const float DefaultCoreRadius = 0.4f;

        private const float FlashWidth = 0.12f;
        private const float FlashSeconds = 0.5f;
        private static readonly Color FlashColor = new Color(0.9f, 0.85f, 1f, 1f);

        // 붕괴 연출 [임시]: 다 빨아들였을 때 반지름 배율(1 + 이 값), 마지막 번쩍임의 굵기·시간·크기(블랙홀 반지름 배).
        private const float CollapseGrowth = 0.9f;
        private const float BurstWidth = 0.3f;
        private const float BurstSeconds = 1.2f;
        private const float BurstRadius = 2.2f;

        private readonly LineStrokes _strokes;
        private readonly Shockwaves _shockwaves;
        // Black Hole Pulse 설정. 끄면 레벨업 때의 원형 플래시·충격파를 내지 않는다(블랙홀이 커지는 것은 그대로).
        private readonly GameSettings _settings;
        private readonly GameObject _blackHole;
        // 블랙홀 오브젝트의 처음 크기와, 그 크기에서 검은 중심 원의 반지름(월드 단위). Level에 맞춰 이 비율로 키운다.
        private readonly Vector3 _blackHoleScale;
        private readonly float _blackHoleCoreRadius;
        private int _shownLevel = -1;

        // 지금 쓰는 (Level, 반지름) 표. Level이 커지는 순서. 튜토리얼 판은 자기 표를 넣는다(TutorialStage.BlackHoleRadii).
        // Reset이 실측 표로 되돌린다.
        public IReadOnlyList<(int Level, float Radius)> Radii { get; set; } = MeasuredRadii;
        // 마지막으로 적용한 크기의 Level(바퀴 진행도를 더한 값). 바뀔 때만 크기를 고친다.
        private float _shownLevelF = -1;
        // 지금 Level의 검은 중심 반지름(월드 단위). 붕괴 배율을 곱해 오브젝트 크기를 정한다.
        private float _radius;
        // 마지막으로 적용한 붕괴 진행도와, 마지막 번쩍임을 냈는가.
        private float _shownCollapse;
        private bool _collapseBurst;

        // 붕괴(엔딩) 진행도(0 ~ 1). BattleSystem이 넣는다. Reset이 0으로 되돌린다.
        public float Collapse { get; set; }

        //public HqView(Transform parent) => _strokes = new LineStrokes(parent, "Hq View");
        public HqView(Transform parent, GameObject blackHole, Camera camera, GameSettings settings, ScriptableRendererFeature shockwaveFeature = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _strokes = new LineStrokes(parent, "Hq View");
            _shockwaves = new Shockwaves(camera, shockwaveFeature);
            _blackHole = blackHole != null ? blackHole : throw new ArgumentNullException(nameof(blackHole));
            _blackHoleScale = _blackHole.transform.localScale;
            _blackHoleCoreRadius = CoreRadiusOf(_blackHole);
            _blackHole.SetActive(false);
        }

        public void Synchronize(World world, float delta)
        {
            _strokes.Age(delta);
            _shockwaves.Age(delta);

            float fieldScale = world.Hq.FieldScale;

            if (!_blackHole.activeSelf)
                _blackHole.SetActive(true);

            GrowthPacer pacer = world.GrowthPacer;
            int level = world.Hq.Level - pacer.PendingWheels;
            float levelF = pacer.IsAnimating ? level + pacer.WheelProgress : level;

            bool levelChanged = levelF != _shownLevelF;
            bool collapseChanged = Collapse != _shownCollapse;

            if (!levelChanged && !collapseChanged)
                return;

            if (levelChanged)
            {
                float radius = RadiusOf(levelF);
                _radius = radius;

                if (_shownLevel >= 0 && level > _shownLevel && _settings.IsOn(GameSettings.BlackHolePulse))
                {
                    LineStrokes.SetCircle(_strokes.Flash("Level Up", FlashWidth * fieldScale, FlashColor, FlashSeconds), BattleSpace.Origin, radius * 1.4f);

                    Vector3 center = _blackHole.transform.position;
                    _shockwaves.Spawn(center, strength: 1f, seconds: 1.2f);
                    _shockwaves.Spawn(center, strength: 0.45f, seconds: 1.4f, delay: 0.14f);
                    Haptics.Play();
                }

                _shownLevel = level;
                _shownLevelF = levelF;
            }

            if (collapseChanged)
                AdvanceCollapse(fieldScale);

            float t = Mathf.Clamp01(_shownCollapse);
            float grow = 1 + CollapseGrowth * t * t * (3 - 2 * t);
            _blackHole.transform.localScale = _blackHoleScale * (_radius * grow / _blackHoleCoreRadius);
        }

        // 붕괴가 시작되는 순간 충격파, 다 빨아들이는 순간 큰 번쩍임과 충격파를 한 번씩 낸다.
        private void AdvanceCollapse(float fieldScale)
        {
            Vector3 center = _blackHole.transform.position;

            if (_shownCollapse <= 0 && Collapse > 0)
                _shockwaves.Spawn(center, strength: 0.7f, seconds: 1.6f);

            _shownCollapse = Collapse;

            if (_collapseBurst || Collapse < 1)
                return;

            _collapseBurst = true;
            float radius = _radius * (1 + CollapseGrowth);
            LineStrokes.SetCircle(_strokes.Flash("Collapse", BurstWidth * fieldScale, FlashColor, BurstSeconds), BattleSpace.Origin, radius * BurstRadius);
            _shockwaves.Spawn(center, strength: 1.6f, seconds: 1.8f, maxRadius: 2f);
            _shockwaves.Spawn(center, strength: 0.8f, seconds: 2.0f, delay: 0.2f, maxRadius: 2f);
        }

        // 판이 바뀌거나 판을 정리할 때 지운다. 다음 판의 블랙홀은 Level 0에서 다시 그린다.
        public void Reset()
        {
            _strokes.Reset();
            _shockwaves.Reset();
            _shownLevel = -1;
            _shownLevelF = -1;
            _radius = 0;
            _shownCollapse = 0;
            _collapseBurst = false;
            Collapse = 0;
            Radii = MeasuredRadii;
            _blackHole.SetActive(false);
        }

        //public void Dispose() => _strokes.Dispose();
        public void Dispose()
        {
            _strokes.Dispose();
            _shockwaves.Dispose();
            if (_blackHole != null)          // 추가: 씬 오브젝트라 파괴하지 않고 끄기만 한다.
            {
                _blackHole.transform.localScale = _blackHoleScale;
                _blackHole.SetActive(false);
            }
        }

        // 처음 크기의 블랙홀 오브젝트에서 검은 중심 원(자식의 SpriteRenderer)의 반지름. 없으면 DefaultCoreRadius.
        // 꺼진 오브젝트도 잴 수 있게 렌더러의 bounds가 아니라 스프라이트 크기와 크기 비율로 잰다.
        private static float CoreRadiusOf(GameObject blackHole)
        {
            SpriteRenderer core = blackHole.GetComponentInChildren<SpriteRenderer>(true);
            float rootScale = blackHole.transform.lossyScale.x;

            if (core == null || core.sprite == null || rootScale <= 0)
                return DefaultCoreRadius;

            float extent = core.drawMode == SpriteDrawMode.Simple ? core.sprite.bounds.extents.x : core.size.x * 0.5f;
            float radius = extent * core.transform.lossyScale.x / rootScale;
            return radius > 0 ? radius : DefaultCoreRadius;
        }

        // Level의 반지름(지금 표 Radii): 점 사이는 직선, 첫 점 앞·마지막 점 뒤는 끝 점의 값. 소수 Level(바퀴 진행 중)도 같은 직선 위다.
        private float RadiusOf(float level)
        {
            IReadOnlyList<(int Level, float Radius)> points = Radii;

            if (level <= points[0].Level)
                return points[0].Radius;

            for (int i = 1; i < points.Count; i++)
            {
                (int toLevel, float toRadius) = points[i];

                if (level > toLevel)
                    continue;

                (int fromLevel, float fromRadius) = points[i - 1];
                return fromRadius + (toRadius - fromRadius) * (level - fromLevel) / (toLevel - fromLevel);
            }

            return points[points.Count - 1].Radius;
        }
    }
}
