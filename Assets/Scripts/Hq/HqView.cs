using System;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 블랙홀(HQ)의 화면. 판 동안 씬의 블랙홀 오브젝트(검은 중심 원·배경 소용돌이)를 켜고 판의 Level에 맞춰 키운다. 게임 상태를 바꾸지 않는다.
    // 크기는 판 Level이 정하고, Level이 오른 순간 커지며 바깥으로 한 번 번쩍인다. 그림일 뿐이다 — 출현 띠·공전·Breaker와 무관하다(GAME_RULES 3.1).
    // 크기는 원작 실측(검은 중심의 C0 화면 지름)을 Level에 맞춘 것이다: Level 0 → 12px, 10 → 47px, 20 → 86px, 30 → 158px, 35 → 232px,
    // 40 → 약 292px(그 구간 카메라 배율이 추정값이라 오차 약 ±12%).
    // 1 unit = C0 54px(카메라 크기 10)로 바꾼 반지름을 Level 사이에서 직선으로 잇고, 마지막 점 뒤는 그대로 둔다.
    // 블랙홀 오브젝트는 검은 중심 원의 반지름이 이 값이 되도록 처음 크기에 비례해 키운다(배경 소용돌이도 같은 비율).
    // 번쩍임 선 굵기는 화면에서 고정이라 판의 전장 배율을 곱한다(오브젝트는 월드 크기라 카메라가 넓어지면 작아 보인다).
    internal sealed class HqView : IDisposable
    {
        // (Level, 반지름) 실측점. Level이 커지는 순서.
        private static readonly (int Level, float Radius)[] _radiusPoints =
        {
            (0, 0.111f),
            (10, 0.435f),
            (20, 0.796f),
            (30, 1.463f),
            (35, 2.148f),
            (40, 2.704f),
        };

        // 블랙홀 오브젝트의 검은 중심 원을 찾지 못했을 때 쓰는 처음 크기의 반지름(Circle 스프라이트 지름 1 × 0.8 ÷ 2).
        private const float DefaultCoreRadius = 0.4f;

        private const float FlashWidth = 0.12f;
        private const float FlashSeconds = 0.5f;
        private static readonly Color FlashColor = new Color(0.9f, 0.85f, 1f, 1f);

        private readonly LineStrokes _strokes;
        private readonly GameObject _blackHole;
        // 블랙홀 오브젝트의 처음 크기와, 그 크기에서 검은 중심 원의 반지름(월드 단위). Level에 맞춰 이 비율로 키운다.
        private readonly Vector3 _blackHoleScale;
        private readonly float _blackHoleCoreRadius;
        private int _shownLevel = -1;

        //public HqView(Transform parent) => _strokes = new LineStrokes(parent, "Hq View");
        public HqView(Transform parent, GameObject blackHole)
        {
            _strokes = new LineStrokes(parent, "Hq View");
            _blackHole = blackHole != null ? blackHole : throw new ArgumentNullException(nameof(blackHole));
            _blackHoleScale = _blackHole.transform.localScale;
            _blackHoleCoreRadius = CoreRadiusOf(_blackHole);
            _blackHole.SetActive(false);
        }

        public void Synchronize(World world, float delta)
        {
            _strokes.Age(delta);

            float fieldScale = world.Hq.FieldScale;

            if (!_blackHole.activeSelf)
                _blackHole.SetActive(true);

            int level = world.Hq.Level;

            if (level == _shownLevel)
                return;

            float radius = RadiusOf(level);
            _blackHole.transform.localScale = _blackHoleScale * (radius / _blackHoleCoreRadius);

            if (_shownLevel >= 0 && level > _shownLevel)
                LineStrokes.SetCircle(_strokes.Flash("Level Up", FlashWidth * fieldScale, FlashColor, FlashSeconds), BattleSpace.Origin, radius * 1.4f);

            _shownLevel = level;
        }

        // 그리는 선이 없고, 지운 객체도 장면에서 모두 사라졌는가(Reset 뒤 한 프레임).
        public bool IsClear => _strokes.IsClear;

        // 판이 바뀌거나 판을 정리할 때 지운다. 다음 판의 블랙홀은 Level 0에서 다시 그린다.
        public void Reset()
        {
            _strokes.Reset();
            _shownLevel = -1;
            _blackHole.SetActive(false);
        }

        //public void Dispose() => _strokes.Dispose();
        public void Dispose()
        {
            _strokes.Dispose();
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

        // Level의 반지름: 실측점 사이는 직선, 첫 점 앞·마지막 점 뒤는 끝 점의 값.
        private static float RadiusOf(int level)
        {
            if (level <= _radiusPoints[0].Level)
                return _radiusPoints[0].Radius;

            for (int i = 1; i < _radiusPoints.Length; i++)
            {
                (int toLevel, float toRadius) = _radiusPoints[i];

                if (level > toLevel)
                    continue;

                (int fromLevel, float fromRadius) = _radiusPoints[i - 1];
                return fromRadius + (toRadius - fromRadius) * (level - fromLevel) / (toLevel - fromLevel);
            }

            return _radiusPoints[_radiusPoints.Length - 1].Radius;
        }
    }
}
