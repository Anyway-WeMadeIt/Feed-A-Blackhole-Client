using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 코드가 그리는 임시 도형(EnemyShape). 적 종류 에셋에 스프라이트가 없을 때 쓴다. 종류를 모른다 — 어느 종류가 어느 도형인지는 EnemyLook이 정한다.
    // 불규칙 다각형 5종(적 번호로 고름), 원(행성), 완만한 태양(별), 승인 시안의 은하 실루엣, 은하 중심 원(성질 표식).
    internal sealed class EnemyShapes : IDisposable
    {
        private const int ShapePixels = 64;
        // 은하 나선팔의 곡선을 위해 해상도를 두 배로 그린다. 단위당 픽셀도 같이 두 배라 크기(1 단위)는 다른 도형과 같다.
        private const int GalaxyPixels = 128;
        // 중심 원은 은하 몸체 위에 겹치지 않고, 특수 성질의 작은 표식에만 쓴다.
        private const float GalaxyCoreRadius = 0.28f;
        private static readonly Vector2[][] ShapeVertices =
        {
            new[] { new Vector2(-0.47f, 0.16f), new Vector2(-0.18f, 0.49f), new Vector2(0.43f, 0.36f), new Vector2(0.34f, -0.27f), new Vector2(-0.32f, -0.45f) },
            new[] { new Vector2(-0.49f, 0.01f), new Vector2(-0.27f, 0.43f), new Vector2(0.18f, 0.49f), new Vector2(0.47f, 0.12f), new Vector2(0.29f, -0.43f) },
            new[] { new Vector2(-0.43f, -0.33f), new Vector2(-0.39f, 0.28f), new Vector2(-0.05f, 0.49f), new Vector2(0.45f, 0.23f), new Vector2(0.24f, -0.39f) },
            new[] { new Vector2(-0.49f, 0.31f), new Vector2(-0.07f, 0.48f), new Vector2(0.27f, 0.27f), new Vector2(0.48f, 0.02f), new Vector2(0.35f, -0.45f), new Vector2(-0.29f, -0.34f) },
            new[] { new Vector2(-0.49f, -0.05f), new Vector2(-0.31f, 0.33f), new Vector2(-0.07f, 0.49f), new Vector2(0.37f, 0.34f), new Vector2(0.48f, -0.16f), new Vector2(0.06f, -0.43f) },
        };


        private readonly Texture2D[] _polygonTextures = new Texture2D[ShapeVertices.Length];
        private readonly Sprite[] _polygons = new Sprite[ShapeVertices.Length];
        private readonly Texture2D _circleTexture;
        private readonly Sprite _circle;
        private readonly Texture2D _starTexture;
        private readonly Sprite _star;
        private readonly Texture2D _galaxyTexture;
        private readonly Sprite _galaxy;
        private readonly Texture2D _galaxyCoreTexture;
        private readonly Sprite _galaxyCore;

        public EnemyShapes()
        {
            for (int i = 0; i < ShapeVertices.Length; i++)
            {
                _polygonTextures[i] = CreateShape(i);
                _polygons[i] = Sprite.Create(_polygonTextures[i], new Rect(0, 0, ShapePixels, ShapePixels), new Vector2(0.5f, 0.5f), ShapePixels);
            }

            _circleTexture = CreateCircle();
            _circle = Sprite.Create(_circleTexture, new Rect(0, 0, ShapePixels, ShapePixels), new Vector2(0.5f, 0.5f), ShapePixels);

            _starTexture = CreateStarBurst();
            _star = Sprite.Create(_starTexture, new Rect(0, 0, ShapePixels, ShapePixels), new Vector2(0.5f, 0.5f), ShapePixels);

            _galaxyTexture = CreateGalaxyArms();
            _galaxy = Sprite.Create(_galaxyTexture, new Rect(0, 0, GalaxyPixels, GalaxyPixels), new Vector2(0.5f, 0.5f), GalaxyPixels, 0, SpriteMeshType.FullRect);
            _galaxyCoreTexture = CreateGalaxyCore();
            _galaxyCore = Sprite.Create(_galaxyCoreTexture, new Rect(0, 0, GalaxyPixels, GalaxyPixels), new Vector2(0.5f, 0.5f), GalaxyPixels, 0, SpriteMeshType.FullRect);
        }

        // 도형의 스프라이트. None과 모르는 값은 다각형이다. enemyId는 다각형 변형을 고르는 데만 쓴다.
        public Sprite Of(EnemyShape shape, int enemyId)
        {
            switch (shape)
            {
                case EnemyShape.Circle:
                    return _circle;
                case EnemyShape.StarBurst:
                    return _star;
                case EnemyShape.GalaxySilhouette:
                    return _galaxy;
                case EnemyShape.GalaxyCore:
                    return _galaxyCore;
                default:
                    return _polygons[(enemyId - 1) % _polygons.Length];
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _polygons.Length; i++)
            {
                Object.Destroy(_polygons[i]);
                Object.Destroy(_polygonTextures[i]);
            }

            Object.Destroy(_circle);
            Object.Destroy(_circleTexture);
            Object.Destroy(_star);
            Object.Destroy(_starTexture);
            Object.Destroy(_galaxy);
            Object.Destroy(_galaxyTexture);
            Object.Destroy(_galaxyCore);
            Object.Destroy(_galaxyCoreTexture);
        }

        // 꼭짓점 안쪽을 흰색으로 채우고 가장자리 한 픽셀을 부드럽게 한다.
        private static Texture2D CreateShape(int shapeIndex) =>
            CreateFilledShape($"Enemy Polygon {shapeIndex + 1}", ShapePixels, ShapeVertices[shapeIndex]);

        private static Texture2D CreateFilledShape(string name, int size, Vector2[] outline)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float center = (size - 1) / 2f;
            Vector2[] vertices = new Vector2[outline.Length];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = new Vector2(center, center) + outline[i] * size;

            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    bool inside = false;
                    float minDistanceSquared = float.MaxValue;

                    for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
                    {
                        Vector2 start = vertices[j];
                        Vector2 end = vertices[i];
                        if ((start.y > y) != (end.y > y) && x < (end.x - start.x) * (y - start.y) / (end.y - start.y) + start.x)
                            inside = !inside;

                        Vector2 edge = end - start;
                        float projection = Mathf.Clamp01(Vector2.Dot(point - start, edge) / edge.sqrMagnitude);
                        minDistanceSquared = Mathf.Min(minDistanceSquared, (point - (start + projection * edge)).sqrMagnitude);
                    }

                    float distance = Mathf.Sqrt(minDistanceSquared) * (inside ? 1 : -1);
                    byte alpha = (byte)(Mathf.Clamp01(distance + 0.5f) * 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // 완전한 원(행성).
        private static Texture2D CreateCircle()
        {
            var texture = new Texture2D(ShapePixels, ShapePixels, TextureFormat.RGBA32, false)
            {
                name = "Enemy Circle",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float center = (ShapePixels - 1) / 2f;
            float radius = ShapePixels * 0.49f;
            var pixels = new Color32[ShapePixels * ShapePixels];

            for (int y = 0; y < ShapePixels; y++)
            {
                for (int x = 0; x < ShapePixels; x++)
                {
                    float distance = radius - new Vector2(x - center, y - center).magnitude;
                    pixels[y * ShapePixels + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(distance + 0.5f) * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // 산등성이처럼 완만한 태양 모양(별). 10개 돌기, 골 깊이는 바깥 반지름의 20%.
        private static Texture2D CreateStarBurst()
        {
            const int Spikes = 10;
            const float InnerRatio = 0.8f;

            var texture = new Texture2D(ShapePixels, ShapePixels, TextureFormat.RGBA32, false)
            {
                name = "Enemy Star Burst",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float center = (ShapePixels - 1) / 2f;
            float outerRadius = ShapePixels * 0.49f;
            float innerRadius = outerRadius * InnerRatio;
            var pixels = new Color32[ShapePixels * ShapePixels];

            for (int y = 0; y < ShapePixels; y++)
            {
                for (int x = 0; x < ShapePixels; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);

                    // 코사인 굴곡: 선형 삼각파와 달리 돌기 끝과 골 양쪽 다 꺾이는 모서리 없이 매끄럽게 이어진다.
                    float wave = (1f - Mathf.Cos(angle * Spikes)) * 0.5f; // 0(골) ~ 1(돌기 끝)
                    float edgeRadius = innerRadius + (outerRadius - innerRadius) * wave;

                    float distance = edgeRadius - r;
                    pixels[y * ShapePixels + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(distance + 0.5f) * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        // 승인된 은하 시안의 외곽선. 중심과 세 팔을 한 면으로 채워 파인 부분을 그대로 보존한다.
        // 좌표는 시안의 142×167 캔버스 기준이며, 글자를 제외한 도형만 1 단위 안에 맞춘다.
        private static Texture2D CreateGalaxyArms()
        {
            var start = new Vector2(99, 39);
            // 각 구간의 제어점 두 개와 끝점(3차 베지어).
            Vector2[] curves =
            {
                new Vector2(88, 30), new Vector2(79, 38), new Vector2(74, 48),
                new Vector2(108, 49), new Vector2(126, 69), new Vector2(126, 92),
                new Vector2(126, 104), new Vector2(120, 114), new Vector2(111, 118),
                new Vector2(108, 119), new Vector2(106, 117), new Vector2(109, 114),
                new Vector2(115, 105), new Vector2(115, 97), new Vector2(107, 86),
                new Vector2(108, 110), new Vector2(89, 137), new Vector2(64, 138),
                new Vector2(54, 138), new Vector2(46, 134), new Vector2(42, 129),
                new Vector2(39, 125), new Vector2(41, 123), new Vector2(44, 125),
                new Vector2(52, 131), new Vector2(61, 126), new Vector2(66, 119),
                new Vector2(43, 120), new Vector2(25, 111), new Vector2(20, 95),
                new Vector2(14, 82), new Vector2(18, 68), new Vector2(25, 61),
                new Vector2(29, 58), new Vector2(31, 60), new Vector2(29, 63),
                new Vector2(23, 73), new Vector2(29, 82), new Vector2(38, 88),
                new Vector2(33, 67), new Vector2(40, 48), new Vector2(53, 35),
                new Vector2(70, 19), new Vector2(89, 25), new Vector2(98, 32),
                new Vector2(102, 36), new Vector2(103, 42), new Vector2(99, 39),
            };
            const int SamplesPerCurve = 12;
            var outline = new Vector2[curves.Length / 3 * SamplesPerCurve];
            int index = 0;
            for (int segment = 0; segment < curves.Length; segment += 3)
            {
                for (int sample = 0; sample < SamplesPerCurve; sample++)
                {
                    float t = sample / (float)SamplesPerCurve;
                    float u = 1 - t;
                    Vector2 point = u * u * u * start
                        + 3 * u * u * t * curves[segment]
                        + 3 * u * t * t * curves[segment + 1]
                        + t * t * t * curves[segment + 2];
                    outline[index++] = new Vector2((point.x - 71.5f) / 116f, (82f - point.y) / 116f);
                }
                start = curves[segment + 2];
            }

            return CreateFilledShape("Enemy Galaxy Silhouette", GalaxyPixels, outline);
        }

        // 다른 천체와 같은 단색 원. 중심 크기만큼 채우고 윤곽 한 픽셀만 부드럽게 한다.
        private static Texture2D CreateGalaxyCore()
        {
            var texture = new Texture2D(GalaxyPixels, GalaxyPixels, TextureFormat.RGBA32, false)
            {
                name = "Enemy Galaxy Core",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            float center = (GalaxyPixels - 1) / 2f;
            float edge = GalaxyPixels * 0.49f;
            var pixels = new Color32[GalaxyPixels * GalaxyPixels];

            for (int y = 0; y < GalaxyPixels; y++)
            {
                for (int x = 0; x < GalaxyPixels; x++)
                {
                    float distance = edge * GalaxyCoreRadius - new Vector2(x - center, y - center).magnitude;
                    float alpha = Mathf.Clamp01(distance + 0.5f);
                    pixels[y * GalaxyPixels + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
