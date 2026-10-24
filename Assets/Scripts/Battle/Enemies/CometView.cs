using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 혜성(픽업 적)의 화면. EnemyView가 만들어 갖고, 혜성을 만날 때마다 Show를, 사라진 혜성의 정리는 Retain을 부른다.
    // 게임 상태를 바꾸지 않는다. 외형은 CometLook이 가진다(스프라이트가 아니라 셰이더 BlackHole/Comet).
    // 혜성 하나는 사각형 하나에 무지개 구체 본체와, 블랙홀을 중심으로 한 공전 경로 뒤의 무지개 궤적을 같이 그린다.
    // 본체 반지름은 규칙 반지름이고, 궤적은 공전 방향(이동 속도의 부호) 반대쪽에 남는다. 피격 흔들림은 없다(HP 1이라 한 번에 부서진다).
    // 구체 속 무지개는 혜성마다 출현 때 정해진 각도·속도로 자전한다(연출뿐, 일시정지 중에는 멈춘다).
    // 사망 파편·골드 텍스트는 EnemyView가 모든 적에 같이 등록한다. 규칙 평면은 장면의 z = 0이고 x·y는 같다.
    internal sealed class CometView : IDisposable
    {
        // 혜성 하나의 화면 상태: 사각형과 그라데이션 자전의 각도(바퀴)·속도(바퀴/초).
        private sealed class CometVisual
        {
            public MeshRenderer Renderer;
            public float Angle;
            public float Spin;
        }

        // 혜성은 적 스프라이트·달 위, Breaker 링 계열(5 ~ 11) 아래에 그린다.
        private const int SortingOrder = 3;
        private const float MeshMargin = 1.1f;

        private static readonly int _headRadiusId = Shader.PropertyToID("_HeadRadius");
        private static readonly int _tailLengthId = Shader.PropertyToID("_TailLength");
        private static readonly int _toCenterId = Shader.PropertyToID("_ToCenter");
        private static readonly int _gradientAngleId = Shader.PropertyToID("_GradientAngle");
        private static readonly int _directionId = Shader.PropertyToID("_Direction");

        private readonly Transform _root;
        private readonly CometLook _look;
        private readonly Mesh _quad;
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private readonly Dictionary<EnemyId, CometVisual> _visuals = new Dictionary<EnemyId, CometVisual>();
        private readonly List<EnemyId> _gone = new List<EnemyId>();

        public CometView(Transform parent, CometLook look)
        {
            _root = new GameObject("Comet View").transform;
            _root.SetParent(parent, false);
            _look = look;
            _quad = QuadRenderers.CreateMesh();
        }

        // 이 혜성의 화면을 (처음이면 만들고) 이번 프레임에 맞춘다.
        // paused: 일시정지 중이면 자전을 멈춘다. delta: 이번 호출 사이 지난 시간(즉시 스냅만 하고 싶을 때는 0).
        public void Show(Enemy enemy, bool paused, float delta)
        {
            if (!_visuals.TryGetValue(enemy.Id, out CometVisual visual))
            {
                visual = Create(enemy);
                _visuals.Add(enemy.Id, visual);
            }
            else if (!paused && delta > 0)
            {
                visual.Angle = Mathf.Repeat(visual.Angle + visual.Spin * delta, 1);
            }

            Place(visual, enemy);
        }

        // seen에 없는 혜성(사망·정리)의 화면을 지운다.
        public void Retain(HashSet<EnemyId> seen)
        {
            _gone.Clear();

            foreach (EnemyId id in _visuals.Keys)
            {
                if (!seen.Contains(id))
                    _gone.Add(id);
            }

            foreach (EnemyId id in _gone)
            {
                Object.Destroy(_visuals[id].Renderer.gameObject);
                _visuals.Remove(id);
            }
        }

        // 판이 바뀌거나 판을 정리할 때 모든 혜성을 지운다.
        public void Reset()
        {
            foreach (CometVisual visual in _visuals.Values)
                Object.Destroy(visual.Renderer.gameObject);

            _visuals.Clear();
        }

        public void Dispose()
        {
            Object.Destroy(_root.gameObject);
            Object.Destroy(_quad);
        }

        // 출현 때 한 번 만든다. 사각형은 본체 중심에 놓이며 본체와 궤적 길이를 덮는다. 블랙홀 방향은 매 프레임 갱신한다(Place).
        private CometVisual Create(Enemy enemy)
        {
            MeshRenderer renderer = QuadRenderers.Create($"{enemy.Definition.Type} #{enemy.Id.Value}", _root, _quad, _look.Material, SortingOrder);
            float headRadius = enemy.Stats.Radius;
            float tailLength = _look.TailLength;
            float size = 2 * (tailLength + headRadius) * MeshMargin;
            renderer.transform.localScale = new Vector3(size, size, 1);

            renderer.GetPropertyBlock(_properties);
            _properties.SetFloat(_headRadiusId, headRadius);
            _properties.SetFloat(_tailLengthId, tailLength);
            _properties.SetFloat(_directionId, enemy.Stats.MoveSpeed < 0 ? -1 : 1);
            renderer.SetPropertyBlock(_properties);

            renderer.enabled = true;

            return new CometVisual
            {
                Renderer = renderer,
                Angle = UnityEngine.Random.value,
                Spin = UnityEngine.Random.Range(_look.SpinSpeedMin, _look.SpinSpeedMax),
            };
        }

        private void Place(CometVisual visual, Enemy enemy)
        {
            MeshRenderer renderer = visual.Renderer;
            renderer.transform.localPosition = new Vector3(enemy.Position.X, enemy.Position.Y, 0);
            renderer.GetPropertyBlock(_properties);
            _properties.SetVector(_toCenterId, new Vector4(-enemy.Position.X, -enemy.Position.Y, 0, 0));
            _properties.SetFloat(_gradientAngleId, visual.Angle * 2 * Mathf.PI);
            renderer.SetPropertyBlock(_properties);
        }
    }
}
