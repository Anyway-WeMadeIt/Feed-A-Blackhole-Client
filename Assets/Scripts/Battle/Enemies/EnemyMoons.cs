using System;
using BlackHole.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 달 성질이 붙은 적의 달. 외형은 Breaker 달과 같은 셰이더·머티리얼(BreakerLook.OrbMaterial)이다.
    // 모든 행성이 같은 크기의 달을 가지고, 궤도는 적의 반지름 바로 바깥이다(Breaker는 링 바깥 가장자리).
    // 달의 상태(렌더러·위상·속도·제 크기)는 EnemyVisual이 갖고, 이 처리기 하나가 모든 달을 같은 식으로 다룬다. 달 성질의 부속물로 EnemyView에 등록된다(IEnemyAttachment).
    internal sealed class EnemyMoons : IEnemyAttachment, IDisposable
    {
        // 달 구체의 반지름(전장 배율 1에서의 월드 단위). 모든 행성이 같은 크기의 달을 가진다. Breaker 달(BreakerLook.OrbRadius)보다 작다.
        // 원작 달은 카메라와 무관하게 화면에서 약 25px이라(C0 지름 25px ÷ 54px/unit ÷ 2) 판의 전장 배율을 곱한다.
        private const float MoonRadius = 0.236f;
        // 공전 속도 배율의 범위. 기본 속도(BreakerLook.OrbitSpeed)에 곱해 적마다 다르게 돈다.
        private const float MoonSpeedMin = 0.8f;
        private const float MoonSpeedMax = 1.2f;
        // 달은 적 스프라이트(0)와 성질 속 채움(1) 위, Breaker 링 계열(5 ~ 11) 아래에 그린다.
        private const int MoonSortingOrder = 2;
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
        private readonly BreakerLook _look;
        private readonly Mesh _quad;
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private readonly float[] _kinds = new float[MaxOrbs];

        // root: 달을 둘 부모(적 화면의 루트). look: 달의 외형(머티리얼·색·간격·기본 공전 속도)을 Breaker 달과 같게 맞추려고 받는다.
        public EnemyMoons(Transform root, BreakerLook look)
        {
            _root = root;
            _look = look;
            _quad = QuadRenderers.CreateMesh();
        }

        public EnemyTraitType Trait => EnemyTraitType.Moon;

        // 달은 출현 때 한 번 만든다. 적의 크기는 변하지 않으므로 궤도와 사각형 크기도 여기서 한 번만 정한다.
        // fieldScale: 이 판의 전장 배율. 적(월드)에 붙어 돌지만 달 자체와 간격은 화면 크기가 고정이라 곱한다.
        public void Create(EnemyVisual visual, Enemy enemy, float fieldScale)
        {
            var moon = QuadRenderers.Create($"Moon #{enemy.Id.Value}", _root, _quad, _look.OrbMaterial, MoonSortingOrder);
            float moonRadius = MoonRadius * fieldScale;
            float orbitRadius = enemy.Stats.Radius + _look.OrbitOffset * fieldScale + moonRadius;
            float size = 2 * (orbitRadius + moonRadius) * MeshMargin;
            moon.transform.localScale = new Vector3(size, size, 1);

            moon.GetPropertyBlock(_properties);
            _properties.SetFloat(_orbitRadiusId, orbitRadius);
            _properties.SetFloat(_orbRadiusId, moonRadius);
            _properties.SetFloat(_orbCountId, 1);
            _properties.SetColor(_moonFillId, _look.MoonFill);
            _properties.SetColor(_moonOutlineId, _look.MoonOutline);
            _properties.SetFloat(_outlineWidthId, _look.MoonOutlineWidth * fieldScale);
            _properties.SetFloatArray(_orbKindsId, _kinds);
            moon.SetPropertyBlock(_properties);

            moon.enabled = true;
            visual.Moon = moon;
            visual.MoonPhase = UnityEngine.Random.value;
            visual.MoonSpeed = _look.OrbitSpeed * UnityEngine.Random.Range(MoonSpeedMin, MoonSpeedMax);
            moon.transform.localPosition = new Vector3(enemy.Position.X, enemy.Position.Y, 0);
            SetPhase(visual);
        }

        // 출현 연출은 점에서 시작한다: 제 크기를 기억해 두고 크기 0으로.
        public void BeginSpawn(EnemyVisual visual)
        {
            visual.MoonBaseScale = visual.Moon.transform.localScale;
            visual.Moon.transform.localScale = new Vector3(0, 0, visual.MoonBaseScale.z);
        }

        // 제 크기에 배율을 곱한다(출현·붕괴 연출).
        public void SetScale(EnemyVisual visual, float scale)
        {
            Vector3 moon = visual.MoonBaseScale;
            visual.Moon.transform.localScale = new Vector3(moon.x * scale, moon.y * scale, moon.z);
        }

        // 적의 자리로 옮기고, 일시정지가 아니면 공전을 delta만큼 진행한다.
        public void Follow(EnemyVisual visual, Point2 position, bool paused, float delta)
        {
            visual.Moon.transform.localPosition = new Vector3(position.X, position.Y, 0);

            if (paused || delta <= 0)
                return;

            // 시계방향으로 돌므로 위상이 줄어든다.
            visual.MoonPhase = Mathf.Repeat(visual.MoonPhase - visual.MoonSpeed * delta, 1);
            SetPhase(visual);
        }

        // 붕괴 중: 적과 같은 자리·비율을 따르고, 블랙홀에 닿으면 숨긴다.
        public void Place(EnemyVisual visual, Vector3 position, float scale, bool visible)
        {
            visual.Moon.transform.localPosition = position;
            SetScale(visual, scale);
            visual.Moon.enabled = visible;
        }

        public void Destroy(EnemyVisual visual) => Object.Destroy(visual.Moon.gameObject);

        public void Dispose() => Object.Destroy(_quad);

        private void SetPhase(EnemyVisual visual)
        {
            visual.Moon.GetPropertyBlock(_properties);
            _properties.SetFloat(_phaseId, visual.MoonPhase);
            visual.Moon.SetPropertyBlock(_properties);
        }
    }
}
