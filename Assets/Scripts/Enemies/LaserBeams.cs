using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 레이저 별의 레이저를 그리는 부품. 사망 효과 화면(DeathEffectView)이 예고 목록(LaserTelegraph)과 발사 기록(LaserBurst)을 읽어 부른다.
    // 외형은 LaserLook이 가진다. 셰이더(BlackHole/Laser Beam)가 선 방향으로 돌린 사각형 하나에 곧은 빛줄기를 수식으로 그린다.
    // 아트 톤은 플랫(색종이): 번짐 대신 색 띠, 사라질 때는 옅어지지 않고 크기가 줄어든다(Look의 세기 곡선은 크기 배율). 조준 실선만 알파로 옅게 그린다.
    // 연출(세 동작, LaserLook 머리말): ① 표식 — 별이 터지며 섬광과 함께 X 표식(꺾인 가로 플레어 + 빔 방향의 짧은 세로 줄기)이 튕기듯 튀어나온다.
    // ② 조준 — 표식이 자라는 동안 빔의 씨앗은 별 자리에 둥근 빛으로 뭉쳐 있다가, 플레어가 점으로 뭉치듯 줄어들며 실선 쪽으로 살짝 돌기 시작할 때
    //   가는 실선으로 늘어나 화면 밖으로 뻗는다. 발사 직전 실선이 진해진다.
    // ③ 관통 — 실선이 판정 너비를 넘겼다가 자리 잡으며 화면을 관통하고, 표식은 가라앉는다(발사 섬광은 없다).
    // - ShowTelegraphs: 예고 중인 레이저마다 섬광·플레어·표식·실선을 그린다. 예고에는 번호가 없어 상태를 두지 않고, 규칙이 주는 남은 시간으로 진행(0 ~ 1)을 구해
    //   매 프레임 모양을 다시 정한다. 플레어의 각과 좌우는 경로 끝점을 씨앗으로 뽑아 예고와 발사가 같은 값을 쓴다.
    // - Play: 발사 하나를 BurstSeconds 동안 그린다 — 빔이 굵어졌다 납작해져 사라지고, 플레어·표식은 FlareCollapseSeconds 동안 가라앉는다.
    // - Age: 시간을 흘린다. 부르지 않으면(일시정지) 멈춘 채 보이고, 다시 부르면 이어간다.
    // 실선·빔의 길이는 규칙의 양 끝(화면 밖 ±300)이 아니라 카메라의 가장 먼 모서리까지다 — 중심에서 자라는 느낌을 내기 위해서다. 규칙 평면은 장면의 z = 0이다.
    internal sealed class LaserBeams : IDisposable
    {
        // 그리는 순서: 플레어 < 빔 < 섬광.
        private const int FlareOrder = 9;
        private const int BeamOrder = 10;
        private const int FlashOrder = 11;
        // 메시가 가장자리 부드럽게 하기까지 담도록 두는 여유 비율.
        private const float MeshMargin = 1.1f;
        // 색 띠가 끝나는 거리(띠 폭의 배수). 띠는 폭 끝에서 딱 끝나므로 사각형을 그 이상 키우면 투명 픽셀만 더 그린다.
        private const float GlowReach = 1;
        // 카메라를 못 찾을 때 실선·빔이 중심에서 한쪽으로 뻗는 길이(월드 단위).
        private const float FallbackReach = 60;

        private static readonly int _lengthId = Shader.PropertyToID("_Length");
        private static readonly int _thicknessId = Shader.PropertyToID("_Thickness");
        private static readonly int _colorId = Shader.PropertyToID("_Color");
        private static readonly int _coreColorId = Shader.PropertyToID("_CoreColor");
        private static readonly int _glowWidthId = Shader.PropertyToID("_GlowWidth");
        private static readonly int _glowStrengthId = Shader.PropertyToID("_GlowStrength");
        private static readonly int _bandsId = Shader.PropertyToID("_Bands");
        private static readonly int _bandFadeId = Shader.PropertyToID("_BandFade");
        private static readonly int _taperId = Shader.PropertyToID("_Taper");
        private static readonly int _fadeId = Shader.PropertyToID("_Fade");

        private readonly Transform _root;
        private readonly LaserLook _look;
        private readonly Mesh _quad;
        private readonly MaterialPropertyBlock _properties = new();
        // 예고 하나의 세 요소. 이번에 쓰지 않은 것은 숨겨 두었다가 다음 예고에 다시 쓴다.
        private readonly List<Trio> _telegraphs = new();
        private readonly List<Burst> _bursts = new();

        // 섬광·플레어·표식·선(빔)의 사각형 넷.
        private sealed class Trio
        {
            public MeshRenderer Flash;
            public MeshRenderer Flare;
            public MeshRenderer Mark;
            public MeshRenderer Line;
        }

        // 발사 하나. 경로는 Play에서 정해지고 시간만 흐른다.
        private sealed class Burst
        {
            public Trio View;
            public bool Playing;
            public float Elapsed;
            public Vector2 Center;
            public float BeamAngle;
            // 발사 순간 플레어가 빔 방향에서 꺾여 있는 각(라디안). 가라앉는 동안 그대로다.
            public float FlareTurn;
            // 판정 너비(빔의 다 굵어진 굵기).
            public float Width;
            public bool Critical;
            public float Field;
            public float Reach;
        }

        public LaserBeams(Transform parent, LaserLook look)
        {
            _root = new GameObject("Laser Beams").transform;
            _root.SetParent(parent, false);
            _look = look;
            _quad = CreateQuad();
        }

        // 예고 중인 레이저마다 섬광·플레어·실선을 그린다. 매 프레임 부른다(일시정지 중에도 — 남은 시간이 멈춰 있어 모양도 멈춘다).
        public void ShowTelegraphs(IReadOnlyList<LaserTelegraph> telegraphs, float fieldScale)
        {
            float field = Field(fieldScale);

            for (int i = 0; i < telegraphs.Count; i++)
            {
                if (i == _telegraphs.Count)
                    _telegraphs.Add(CreateTrio("Laser Telegraph"));

                LaserTelegraph telegraph = telegraphs[i];
                Trio view = _telegraphs[i];
                float progress = telegraph.Duration > 0 ? 1 - Mathf.Clamp01(telegraph.Remaining / telegraph.Duration) : 1;
                Vector2 center = Center(telegraph.Start, telegraph.End);
                float beamAngle = Angle(telegraph.Start, telegraph.End);
                // 플레어는 꺾인 각에서 출발해 발사 직전 빔 쪽으로 회전한다.
                float flareAngle = beamAngle + FlareTurn(telegraph.Start) * _look.FlareTurnFraction(progress);
                float reach = ScreenReach(center);

                // 섬광: 예고 시작에 터졌다 곧 줄어들고, 발사 직전 다시 커진다(크기 배율).
                float flash = _look.Flash(progress);
                Place(view.Flash, center, 0, 0, _look.FlashRadius * 2 * field * flash, _look.FlashGlowWidth * field * flash, 0, 1);

                // 표식: 가로 플레어와 빔 방향의 짧은 세로 줄기가 같은 곡선으로 튕기듯 자라 X를 만들고, 발사 직전 회전하며 얇아진다.
                float growth = _look.FlareGrowth(progress);
                float thin = _look.FlareThinness(progress) * _look.FlareStrength(progress);
                float thickness = _look.FlareThickness * field * thin;
                float glow = _look.FlareGlowWidth * field * thin;
                Place(view.Flare, center, flareAngle, _look.FlareReach * field * growth * 2, thickness, glow, 1, 1);
                Place(view.Mark, center, beamAngle, _look.MarkReach * field * growth * 2, thickness, glow, 1, 1);

                // 조준: 실선이 뻗기 전엔 별 자리에 둥근 빛으로 뭉쳐 있다가(길이 0, 굵기 = 둥근 빛 지름), 자라는 만큼 실선 굵기로 가늘어지며 화면 밖으로 뻗는다.
                float lineGrowth = _look.LineGrowth(progress);
                float lineThickness = Mathf.Lerp(_look.LineDotRadius * 2, _look.LineThickness, lineGrowth) * field;
                float lineGlow = Mathf.Lerp(_look.LineDotRadius, _look.LineGlowWidth, lineGrowth) * field;
                Place(view.Line, center, beamAngle, reach * lineGrowth * 2, lineThickness, lineGlow, 0, _look.LineStrength(progress));
            }

            for (int i = telegraphs.Count; i < _telegraphs.Count; i++)
                Hide(_telegraphs[i]);
        }

        // width: 판정 너비(LaserBurstDefinition.Width). 다 굵어진 빔의 굵기와 맞는 범위가 같다.
        public void Play(Point2 start, Point2 end, float width, bool critical, float fieldScale)
        {
            Burst burst = Take();
            burst.Playing = true;
            burst.Elapsed = 0;
            burst.Center = Center(start, end);
            burst.BeamAngle = Angle(start, end);
            burst.FlareTurn = FlareTurn(start) * _look.FlareTurnFraction(1);
            burst.Width = width;
            burst.Critical = critical;
            burst.Field = Field(fieldScale);
            burst.Reach = ScreenReach(burst.Center);
            Show(burst);
        }

        public void Age(float delta)
        {
            float total = Mathf.Max(_look.BurstSeconds, _look.FlareCollapseSeconds);

            // 매 프레임 경로: 인덱스로 돈다.
            for (int i = 0; i < _bursts.Count; i++)
            {
                Burst burst = _bursts[i];

                if (!burst.Playing)
                    continue;

                burst.Elapsed += delta;

                if (burst.Elapsed >= total)
                {
                    burst.Playing = false;
                    Hide(burst.View);
                    continue;
                }

                Show(burst);
            }
        }

        // 판이 바뀌거나 판을 정리할 때 모두 지운다.
        public void Reset()
        {
            foreach (Trio trio in _telegraphs)
                Destroy(trio);

            foreach (Burst burst in _bursts)
                Destroy(burst.View);

            _telegraphs.Clear();
            _bursts.Clear();
        }

        public void Dispose()
        {
            Object.Destroy(_root.gameObject);
            Object.Destroy(_quad);
        }

        private void Show(Burst burst)
        {
            float progress = Mathf.Clamp01(burst.Elapsed / _look.BurstSeconds);
            float boost = burst.Critical ? _look.CriticalBoost : 1;

            // 빔: 판정 너비 × 곡선으로 순식간에 굵어져 관통한다. 색 띠는 전장 배율(치명타면 더)로 넓힌다.
            // 사라질 때: 띠가 바깥부터 꺼져 심만 남고 → 심이 가는 줄로 납작해져 잠깐 머문 뒤 → 길이는 그대로 둔 채 고르게 가늘어져 안 보이게 된다.
            float beamThickness = burst.Width * _look.BeamWidthScale(progress) * _look.BeamStrength(progress);
            Place(burst.View.Line, burst.Center, burst.BeamAngle, burst.Reach * 2, beamThickness, _look.BeamGlowWidth * burst.Field * boost, 0, 1,
                _look.BeamBandFade(progress));

            // 표식: 예고 끝의 길이(FlareGrowth(1))와 각에서 파도처럼 가라앉는다 — 처음엔 천천히, 끝에 빠르게 줄며 옅어지고, 남은 각은 빔과 일직선까지 마저 돈다.
            float collapse = Mathf.Clamp01(burst.Elapsed / _look.FlareCollapseSeconds);
            float settle = 1 - collapse;
            float growth = _look.FlareGrowth(1) * (1 - collapse * collapse);
            // 각은 예고 끝의 각 그대로, 굵기는 예고 끝의 굵기에서 0까지 가늘어진다.
            float flareAngle = burst.BeamAngle + burst.FlareTurn;
            float thin = _look.FlareThinness(1) * _look.FlareStrength(1) * settle;
            float thickness = _look.FlareThickness * burst.Field * thin;
            float glow = _look.FlareGlowWidth * burst.Field * thin;
            Place(burst.View.Flare, burst.Center, flareAngle, _look.FlareReach * burst.Field * growth * 2, thickness, glow, 1, 1);
            Place(burst.View.Mark, burst.Center, burst.BeamAngle, _look.MarkReach * burst.Field * growth * 2, thickness, glow, 1, 1);

            // 발사에는 섬광이 없다(빔 위에 원이 남지 않게). 발사용 Trio의 Flash는 만들어만 두고 쓰지 않는다.
        }

        // 쉬는 발사를 먼저 쓴다. 없으면 상한까지 새로 만들고, 상한이면 가장 오래된 발사를 다시 쓴다.
        private Burst Take()
        {
            Burst oldest = null;

            for (int i = 0; i < _bursts.Count; i++)
            {
                Burst burst = _bursts[i];

                if (!burst.Playing)
                    return burst;

                if (oldest == null || burst.Elapsed > oldest.Elapsed)
                    oldest = burst;
            }

            if (_bursts.Count < _look.MaxBursts)
            {
                var created = new Burst { View = CreateTrio("Laser Burst") };
                _bursts.Add(created);
                return created;
            }

            return oldest;
        }

        // 사각형 하나를 center에 angle(라디안)로 돌려 놓고 셰이더 값을 준다. fade(알파)가 0이거나 굵기가 0이면 숨긴다.
        // length는 양 끝 사이의 전체 길이(0이면 둥근 섬광), thickness는 심의 굵기, taper는 끝이 뾰족해지는 정도(플레어 1, 나머지 0),
        // bandFade는 색 띠가 바깥부터 꺼진 진행(빔만 쓴다).
        private void Place(MeshRenderer renderer, Vector2 center, float angle, float length, float thickness, float glowWidth, float taper, float fade,
            float bandFade = 0)
        {
            if (fade <= 0 || thickness <= 0)
            {
                renderer.enabled = false;
                return;
            }

            // 크기는 길이 + 굵기 + 번짐이 들어갈 만큼.
            float reach = thickness * 0.5f + glowWidth * GlowReach;
            Transform view = renderer.transform;
            view.localPosition = new Vector3(center.x, center.y, 0);
            view.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
            view.localScale = new Vector3(length + 2 * reach, 2 * reach, 1) * MeshMargin;

            renderer.GetPropertyBlock(_properties);
            _properties.SetFloat(_lengthId, length);
            _properties.SetFloat(_thicknessId, thickness);
            _properties.SetColor(_colorId, _look.Color);
            _properties.SetColor(_coreColorId, _look.CoreColor);
            _properties.SetFloat(_glowWidthId, glowWidth);
            _properties.SetFloat(_glowStrengthId, _look.GlowStrength);
            _properties.SetFloat(_bandsId, _look.Bands);
            _properties.SetFloat(_bandFadeId, bandFade);
            _properties.SetFloat(_taperId, taper);
            _properties.SetFloat(_fadeId, Mathf.Clamp01(fade));
            renderer.SetPropertyBlock(_properties);
            renderer.enabled = true;
        }

        private float Field(float fieldScale) => _look.ScaleWithField ? Mathf.Max(1f, fieldScale) : 1f;

        private static Vector2 Center(Point2 start, Point2 end) => new((start.X + end.X) * 0.5f, (start.Y + end.Y) * 0.5f);

        private static float Angle(Point2 start, Point2 end) => Mathf.Atan2(end.Y - start.Y, end.X - start.X);

        // 플레어가 빔 방향에서 꺾이는 각(라디안, 부호 포함). 경로의 시작점을 씨앗으로 뽑아 같은 레이저는 늘 같은 값이다.
        private float FlareTurn(Point2 seed)
        {
            Vector2 range = _look.FlareAngleDegrees * Mathf.Deg2Rad;
            float side = Hash(seed.X, seed.Y) < 0.5f ? -1f : 1f;
            return side * Mathf.Lerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), Hash(seed.Y, seed.X));
        }

        private static float Hash(float x, float y) => Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f, 1f);

        // center에서 화면의 가장 먼 모서리까지의 거리(월드 단위). 실선·빔을 이만큼 양쪽으로 뻗으면 화면을 늘 관통한다. 원근 카메라나 카메라가 없으면 고정값.
        private static float ScreenReach(Vector2 center)
        {
            Camera camera = Camera.main;

            if (camera == null || !camera.orthographic)
                return FallbackReach;

            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            Vector3 eye = camera.transform.position;
            float dx = Mathf.Abs(center.x - eye.x) + halfWidth;
            float dy = Mathf.Abs(center.y - eye.y) + halfHeight;
            return Mathf.Sqrt(dx * dx + dy * dy) * MeshMargin;
        }

        private static void Hide(Trio trio)
        {
            trio.Flash.enabled = false;
            trio.Flare.enabled = false;
            trio.Mark.enabled = false;
            trio.Line.enabled = false;
        }

        private static void Destroy(Trio trio)
        {
            Object.Destroy(trio.Flash.gameObject);
            Object.Destroy(trio.Flare.gameObject);
            Object.Destroy(trio.Mark.gameObject);
            Object.Destroy(trio.Line.gameObject);
        }

        private Trio CreateTrio(string name) => new()
        {
            Flash = CreateQuadRenderer(name + " Flash", FlashOrder),
            Flare = CreateQuadRenderer(name + " Flare", FlareOrder),
            Mark = CreateQuadRenderer(name + " Mark", FlareOrder),
            Line = CreateQuadRenderer(name + " Beam", BeamOrder),
        };

        private MeshRenderer CreateQuadRenderer(string name, int order)
        {
            var view = new GameObject(name);
            view.transform.SetParent(_root, false);

            view.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = view.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _look.Material;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        // 한 변이 1인 사각형. 길이·폭은 transform의 배율로 맞춘다.
        private static Mesh CreateQuad()
        {
            var mesh = new Mesh
            {
                name = "Laser Beam Quad",
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
