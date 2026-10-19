using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 번개 줄기를 그리는 부품. 사망 효과 화면(DeathEffectView)이 번개 기록을 읽어 Play를 부른다.
    // 외형은 LightningLook이 가진다. 셰이더(BlackHole/Lightning Bolt)가 선 방향으로 돌린 사각형 하나에 꺾인 선을 수식으로 그린다.
    // - Play: From에서 To까지 번개가 hop × DeathEffects.ChainHopSeconds초 뒤에 나타나 깜빡이다가 옅어진다. hop은 연쇄에서 몇 번째 줄기인가(0부터).
    //   간격은 규칙(Core)의 값이다 — 그 홉이 죽인 특수 적의 효과가 같은 시각에 발동하므로 화면이 따로 정하지 않는다.
    // - Age: 시간을 흘린다. 부르지 않으면(일시정지) 깜빡임까지 멈춘 채 보이고, 다시 부르면 이어간다.
    // - 끝난 줄기는 숨겨 두었다가 다음 Play에 다시 쓴다. 동시에 보이는 수는 MaxBolts까지이고, 넘치면 가장 오래된 줄기를 다시 쓴다.
    // 만든 줄기는 모두 이 부품의 뿌리 아래에 있고 Reset이 모두 지운다. 규칙 평면은 장면의 z = 0이다.
    internal sealed class LightningBolts : IDisposable
    {
        private const int SortingOrder = 10;
        // 메시가 가장자리 부드럽게 하기까지 담도록 두는 여유 비율.
        private const float MeshMargin = 1.1f;
        // 번짐이 사실상 사라지는 거리(번짐 폭의 배수). exp(-4) ≈ 2%.
        private const float GlowReach = 4;
        // 셰이더의 무작위 값이 정밀도를 잃지 않도록 씨앗을 이 범위 안에서 돌린다.
        private const float SeedRange = 50;

        private static readonly int _lengthId = Shader.PropertyToID("_Length");
        private static readonly int _segmentsId = Shader.PropertyToID("_Segments");
        private static readonly int _amplitudeId = Shader.PropertyToID("_Amplitude");
        private static readonly int _seedId = Shader.PropertyToID("_Seed");
        private static readonly int _thicknessId = Shader.PropertyToID("_Thickness");
        private static readonly int _colorId = Shader.PropertyToID("_Color");
        private static readonly int _coreColorId = Shader.PropertyToID("_CoreColor");
        private static readonly int _glowWidthId = Shader.PropertyToID("_GlowWidth");
        private static readonly int _glowStrengthId = Shader.PropertyToID("_GlowStrength");
        private static readonly int _fadeId = Shader.PropertyToID("_Fade");

        private readonly Transform _root;
        private readonly LightningLook _look;
        private readonly Mesh _quad;
        private readonly MaterialPropertyBlock _properties = new();
        private readonly List<Bolt> _bolts = new();
        // 줄기마다 다른 꺾임 모양을 주는 씨앗.
        private int _nextSeed;

        private sealed class Bolt
        {
            public MeshRenderer Renderer;
            public float Length;
            // 음수면 아직 나타나기 전(연쇄 지연)이다.
            public float Elapsed;
            public float Seed;
            public bool Playing;
        }

        public LightningBolts(Transform parent, LightningLook look)
        {
            _root = new GameObject("Lightning Bolts").transform;
            _root.SetParent(parent, false);
            _look = look;
            _quad = CreateQuad();
        }

        public void Play(Point2 from, Point2 to, int hop)
        {
            Bolt bolt = Take();
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            bolt.Length = Mathf.Sqrt(dx * dx + dy * dy);
            bolt.Elapsed = -Mathf.Max(hop, 0) * DeathEffects.ChainHopSeconds;
            bolt.Seed = _nextSeed++ % (int)SeedRange * 0.731f;
            bolt.Playing = true;

            // 사각형을 두 점의 가운데에 놓고 선 방향으로 돌린다. 크기는 길이 + 꺾임 + 굵기 + 번짐이 들어갈 만큼.
            float reach = _look.Thickness * 0.5f + _look.GlowWidth * GlowReach;
            Transform view = bolt.Renderer.transform;
            view.localPosition = new Vector3((from.X + to.X) * 0.5f, (from.Y + to.Y) * 0.5f, 0);
            view.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            view.localScale = new Vector3(bolt.Length + 2 * reach, 2 * (_look.Amplitude + reach), 1) * MeshMargin;

            Show(bolt);
        }

        // 끝난 줄기는 숨긴다.
        public void Age(float delta)
        {
            // 매 프레임 경로: 인덱스로 돈다.
            for (int i = 0; i < _bolts.Count; i++)
            {
                Bolt bolt = _bolts[i];

                if (!bolt.Playing)
                    continue;

                bolt.Elapsed += delta;

                if (bolt.Elapsed >= _look.Duration)
                {
                    bolt.Playing = false;
                    bolt.Renderer.enabled = false;
                    continue;
                }

                Show(bolt);
            }
        }

        // 판이 바뀌거나 판을 정리할 때 모든 줄기를 지운다.
        public void Reset()
        {
            foreach (Bolt bolt in _bolts)
                Object.Destroy(bolt.Renderer.gameObject);

            _bolts.Clear();
            _nextSeed = 0;
        }

        public void Dispose()
        {
            Object.Destroy(_root.gameObject);
            Object.Destroy(_quad);
        }

        // 쉬는 줄기를 먼저 쓴다. 없으면 상한까지 새로 만들고, 상한이면 가장 오래된 줄기를 다시 쓴다.
        private Bolt Take()
        {
            Bolt oldest = null;

            for (int i = 0; i < _bolts.Count; i++)
            {
                Bolt bolt = _bolts[i];

                if (!bolt.Playing)
                    return bolt;

                if (oldest == null || bolt.Elapsed > oldest.Elapsed)
                    oldest = bolt;
            }

            if (_bolts.Count < _look.MaxBolts)
            {
                Bolt created = Create();
                _bolts.Add(created);
                return created;
            }

            return oldest;
        }

        // 나타나기 전(연쇄 지연)이면 숨긴다.
        private void Show(Bolt bolt)
        {
            bool visible = bolt.Elapsed >= 0;
            bolt.Renderer.enabled = visible;

            if (visible)
                Apply(bolt);
        }

        private void Apply(Bolt bolt)
        {
            float progress = Mathf.Clamp01(bolt.Elapsed / _look.Duration);
            // 깜빡임: 1/FlickerRate초마다 씨앗을 바꿔 꺾임 모양을 새로 뽑는다.
            int step = Mathf.FloorToInt(bolt.Elapsed * _look.FlickerRate);
            float seed = Mathf.Repeat(bolt.Seed + step * 1.37f, SeedRange);

            bolt.Renderer.GetPropertyBlock(_properties);
            _properties.SetFloat(_lengthId, bolt.Length);
            _properties.SetFloat(_segmentsId, Mathf.Max(1, bolt.Length * _look.KinksPerUnit));
            _properties.SetFloat(_amplitudeId, _look.Amplitude);
            _properties.SetFloat(_seedId, seed);
            _properties.SetFloat(_thicknessId, _look.Thickness);
            _properties.SetColor(_colorId, _look.Color);
            _properties.SetColor(_coreColorId, _look.CoreColor);
            _properties.SetFloat(_glowWidthId, _look.GlowWidth);
            _properties.SetFloat(_glowStrengthId, _look.GlowStrength);
            _properties.SetFloat(_fadeId, _look.Fade(progress));
            bolt.Renderer.SetPropertyBlock(_properties);
        }

        private Bolt Create()
        {
            var view = new GameObject("Lightning Bolt");
            view.transform.SetParent(_root, false);

            view.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = view.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _look.Material;
            renderer.sortingOrder = SortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return new Bolt { Renderer = renderer };
        }

        // 한 변이 1인 사각형. 줄기의 길이·폭은 transform의 배율로 맞춘다.
        private static Mesh CreateQuad()
        {
            var mesh = new Mesh
            {
                name = "Lightning Bolt Quad",
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
