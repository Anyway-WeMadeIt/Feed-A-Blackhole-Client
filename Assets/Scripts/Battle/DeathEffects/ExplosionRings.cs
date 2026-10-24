using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 폭발의 충격파 링을 그리는 부품. 사망 효과 화면(DeathEffectView)이 폭발 기록을 읽어 Play를 부른다.
    // 외형은 ExplosionLook이 가진다. 셰이더(BlackHole/Explosion Ring)가 사각형 하나에 링·번짐·섬광을 수식으로 그린다.
    // - Play: 폭발 자리에서 링이 0부터 판정 반지름까지 DeathEffects.ExplosionRingSeconds 동안 ExplosionRingReach의 식으로 퍼지며 옅어진다. 처음에 안쪽이 번쩍인다.
    //   시간과 퍼짐 식은 규칙(Core)의 것이다 — 링이 적에 닿는 시각에 그 적의 효과가 발동하므로 화면이 따로 정하지 않는다.
    // - Age: 시간을 흘린다. 부르지 않으면(일시정지) 멈춘 채 보이고, 다시 부르면 이어간다.
    // - 끝난 링은 숨겨 두었다가 다음 Play에 다시 쓴다. 동시에 보이는 수는 MaxBlasts까지이고, 넘치면 가장 오래된 링을 다시 쓴다.
    // 만든 링은 모두 이 부품의 뿌리 아래에 있고 Reset이 모두 지운다. 규칙 평면은 장면의 z = 0이다.
    internal sealed class ExplosionRings : IDisposable
    {
        private const int SortingOrder = 10;
        // 메시가 가장자리 부드럽게 하기까지 담도록 두는 여유 비율.
        private const float MeshMargin = 1.1f;
        // 번짐이 사실상 사라지는 거리(번짐 폭의 배수). exp(-4) ≈ 2%.
        private const float GlowReach = 4;

        private static readonly int _radiusId = Shader.PropertyToID("_Radius");
        private static readonly int _thicknessId = Shader.PropertyToID("_Thickness");
        private static readonly int _colorId = Shader.PropertyToID("_Color");
        private static readonly int _glowWidthId = Shader.PropertyToID("_GlowWidth");
        private static readonly int _glowStrengthId = Shader.PropertyToID("_GlowStrength");
        private static readonly int _flashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int _flashId = Shader.PropertyToID("_Flash");
        private static readonly int _fadeId = Shader.PropertyToID("_Fade");

        private readonly Transform _root;
        private readonly ExplosionLook _look;
        private readonly Mesh _quad;
        private readonly MaterialPropertyBlock _properties = new();
        private readonly List<Blast> _blasts = new();

        private sealed class Blast
        {
            public MeshRenderer Renderer;
            // 가장 크게 퍼졌을 때의 반지름(= 판정 반지름).
            public float Radius;
            public float Elapsed;
            public bool Playing;
        }

        public ExplosionRings(Transform parent, ExplosionLook look)
        {
            _root = new GameObject("Explosion Rings").transform;
            _root.SetParent(parent, false);
            _look = look;
            _quad = CreateQuad();
        }

        // center: 폭발 자리. radius: 판정 반지름.
        public void Play(Point2 center, float radius)
        {
            Blast blast = Take();
            blast.Radius = radius;
            blast.Elapsed = 0;
            blast.Playing = true;

            // 메시는 가장 큰 반지름 + 굵기 + 번짐이 들어갈 크기. 폭발마다 반지름이 다를 수 있어 Play마다 정한다.
            float extent = blast.Radius + _look.Thickness * 0.5f + _look.GlowWidth * GlowReach;
            Transform view = blast.Renderer.transform;
            view.localPosition = new Vector3(center.X, center.Y, 0);
            view.localScale = Vector3.one * (2 * extent * MeshMargin);

            blast.Renderer.enabled = true;
            Apply(blast);
        }

        // 끝난 링은 숨긴다.
        public void Age(float delta)
        {
            // 매 프레임 경로: 인덱스로 돈다.
            for (int i = 0; i < _blasts.Count; i++)
            {
                Blast blast = _blasts[i];

                if (!blast.Playing)
                    continue;

                blast.Elapsed += delta;

                if (blast.Elapsed >= DeathEffects.ExplosionRingSeconds)
                {
                    blast.Playing = false;
                    blast.Renderer.enabled = false;
                    continue;
                }

                Apply(blast);
            }
        }

        // 판이 바뀌거나 판을 정리할 때 모든 링을 지운다.
        public void Reset()
        {
            foreach (Blast blast in _blasts)
                Object.Destroy(blast.Renderer.gameObject);

            _blasts.Clear();
        }

        public void Dispose()
        {
            Object.Destroy(_root.gameObject);
            Object.Destroy(_quad);
        }

        // 쉬는 링을 먼저 쓴다. 없으면 상한까지 새로 만들고, 상한이면 가장 오래 재생된 링을 다시 쓴다.
        private Blast Take()
        {
            Blast oldest = null;

            for (int i = 0; i < _blasts.Count; i++)
            {
                Blast blast = _blasts[i];

                if (!blast.Playing)
                    return blast;

                if (oldest == null || blast.Elapsed > oldest.Elapsed)
                    oldest = blast;
            }

            if (_blasts.Count < _look.MaxBlasts)
            {
                Blast created = Create();
                _blasts.Add(created);
                return created;
            }

            return oldest;
        }

        private void Apply(Blast blast)
        {
            float progress = Mathf.Clamp01(blast.Elapsed / DeathEffects.ExplosionRingSeconds);

            blast.Renderer.GetPropertyBlock(_properties);
            _properties.SetFloat(_radiusId, blast.Radius * DeathEffects.ExplosionRingReach(progress));
            _properties.SetFloat(_thicknessId, _look.Thickness);
            _properties.SetColor(_colorId, _look.Color);
            _properties.SetFloat(_glowWidthId, _look.GlowWidth);
            _properties.SetFloat(_glowStrengthId, _look.GlowStrength);
            _properties.SetColor(_flashColorId, _look.FlashColor);
            _properties.SetFloat(_flashId, _look.Flash(progress));
            _properties.SetFloat(_fadeId, _look.Fade(progress));
            blast.Renderer.SetPropertyBlock(_properties);
        }

        private Blast Create()
        {
            var view = new GameObject("Explosion Ring");
            view.transform.SetParent(_root, false);

            view.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = view.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _look.Material;
            renderer.sortingOrder = SortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return new Blast { Renderer = renderer };
        }

        // 한 변이 1인 사각형. 링의 크기는 transform의 배율로 맞춘다.
        private static Mesh CreateQuad()
        {
            var mesh = new Mesh
            {
                name = "Explosion Ring Quad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                    new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0),
                },
                triangles = new[] { 0, 2, 1, 2, 3, 1 }
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
