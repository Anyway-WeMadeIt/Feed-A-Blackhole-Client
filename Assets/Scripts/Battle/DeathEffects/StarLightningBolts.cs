using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace BlackHole.Unity
{
    // 전기 별 번개를 그리는 부품. 사망 효과 화면(DeathEffectView)이 전기 별 번개 기록(StarLightningHit)을 읽어 Play를 부른다.
    // 전기 소행성의 LightningBolts와 완전히 따로다(외형은 StarLightningLook). 셰이더(BlackHole/Lightning Bolt)는 같은 것을 쓴다.
    // 소행성은 홉 하나가 사각형 하나(한 줄기)다. 별은 홉 하나가 나무(Tree) 하나다:
    // - 줄기: From에서 To까지 마디 TrunkKinks개를 거치며 크게 꺾이는 으뜸 가닥. 마디 사이 한 칸이 사각형 하나다.
    // - 갈래: 줄기의 앞쪽 마디에서 갈라져 좌우로 활처럼 벌어졌다가 To에서 다시 모이는 가닥. 여러 줄기가 타겟 하나를 꿰는 모양.
    // - 잔가지: 줄기·갈래의 마디에서 진행 방향으로 비스듬히 뻗어 허공에서 끝나는 짧은 가닥. 더 가늘고 먼저 사라진다.
    // 마디 좌표를 여기서 정하므로 모든 가닥이 마디에서 정확히 이어진다(셰이더의 꺾임은 양 끝에서 0이라 칸의 두 끝이 마디에 붙는다).
    // 나무의 얼개(갈래가 어느 마디에서 어느 쪽으로, 잔가지가 어디서)는 Play에서 한 번 정하고, 마디의 자리와 잔떨림은 깜빡임마다 다시 뽑는다.
    // - Play: 홉 하나(From에서 To까지)의 나무를 바로 보인다. 별 번개는 모든 적중이 같은 순간이라 홉 순서를 두지 않고, 한 발동의 나무가 모두 같은 프레임에
    //   나타난다. GrowSeconds가 0보다 크면 나무 안에서만 From 쪽 칸부터 그 간격으로 자란다. fieldScale은 이 판의 전장 배율.
    // - Age: 시간을 흘린다. 부르지 않으면(일시정지) 멈춘 채 보이고, 다시 부르면 이어간다.
    // - 나무는 사각형을 통째로 들고 있다. 끝난 나무는 숨겨 두었다가 다음 Play에 다시 쓴다. 동시에 보이는 나무는 MaxHops까지이고, 넘치면 가장 오래된 나무를 다시 쓴다.
    // 만든 사각형은 모두 이 부품의 뿌리 아래에 있고 Reset이 모두 지운다. 규칙 평면은 장면의 z = 0이다.
    internal sealed class StarLightningBolts : IDisposable
    {
        private const int SortingOrder = 10;
        // 메시가 가장자리 부드럽게 하기까지 담도록 두는 여유 비율.
        private const float MeshMargin = 1.1f;
        // 번짐이 사실상 사라지는 거리(번짐 폭의 배수). exp(-4) ≈ 2%.
        private const float GlowReach = 4;
        // 셰이더의 무작위 값이 정밀도를 잃지 않도록 씨앗을 이 범위 안에서 돌린다.
        private const float SeedRange = 50;
        // 마디가 직선에서 벗어나는 폭의 상한(홉 길이에 대한 비율). 짧은 홉에서 나무가 홉보다 넓어지지 않게.
        private const float MaxWanderRatio = 0.3f;
        // 갈래가 벌어지는 폭의 상한(갈래 길이에 대한 비율).
        private const float MaxForkSpreadRatio = 0.45f;
        // 갈래가 줄기보다 조금 먼저 사라지는 비율.
        private const float ForkLife = 0.9f;
        // 잔가지의 잔떨림은 줄기보다 조금 작게.
        private const float TwigAmplitude = 0.7f;

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
        private readonly StarLightningLook _look;
        private readonly Mesh _quad;
        private readonly MaterialPropertyBlock _properties = new();
        private readonly List<Tree> _trees = new();
        // 칸마다 다른 잔떨림 모양을 주는 씨앗.
        private int _nextSeed;

        // 사각형 하나 = 마디 사이 한 칸(또는 잔가지 하나). 굵기·시간은 어느 가닥의 칸인가에 따라 Play에서 정해지고, 자리는 깜빡임마다 바뀐다.
        private sealed class Bolt
        {
            public MeshRenderer Renderer;
            public float Length;
            public float Seed;
            // 나무가 나타난 뒤 이 칸이 나타나기까지(초).
            public float Delay;
            public float Duration;
            public float Thickness;
            public float Amplitude;
        }

        // 홉 하나의 나무. 얼개(배열의 크기와 갈래·잔가지의 자리)는 만들 때·Play에서 정하고, 마디 좌표는 Reshape이 채운다.
        private sealed class Tree
        {
            public bool Playing;
            public float Elapsed;
            // 마지막으로 마디를 뽑은 깜빡임 단계. -1이면 아직 뽑지 않았다.
            public int Step;
            public Vector2 From;
            public Vector2 To;
            public float Length;
            // 이 판의 전장 배율(ScaleWithField가 꺼져 있으면 1).
            public float Field;
            // 전장 배율을 곱한 값들.
            public float GlowWidth;
            public float KinksPerUnit;

            // 줄기의 마디: [0] = From, [^1] = To.
            public Vector2[] Trunk;
            // 갈래마다: 마디 [0] = 갈라진 줄기 마디, [^1] = To.
            public Vector2[][] ForkPaths;
            // 갈래가 갈라지는 줄기 마디(Trunk의 중간 마디 번호, 0부터).
            public int[] ForkStart;
            // 갈래가 벌어지는 쪽(+1 왼쪽 / -1 오른쪽).
            public float[] ForkSide;
            // 잔가지의 뿌리: 어느 가닥(-1 줄기, 아니면 갈래 번호)의 어느 마디(그 가닥 배열의 중간 번호)인가.
            public int[] TwigParent;
            public int[] TwigNode;
            // 잔가지가 뿌리 가닥의 진행 방향에서 꺾이는 각(라디안, 부호 포함)과 길이 배율.
            public float[] TwigTurn;
            public float[] TwigReach;
            // 칸: 줄기 칸, 갈래 칸(갈래 순), 잔가지 순.
            public Bolt[] Bolts;
        }

        public StarLightningBolts(Transform parent, StarLightningLook look)
        {
            _root = new GameObject("Star Lightning Bolts").transform;
            _root.SetParent(parent, false);
            _look = look;
            _quad = CreateQuad();
        }

        public void Play(Point2 from, Point2 to, float fieldScale)
        {
            float field = _look.ScaleWithField ? Mathf.Max(1f, fieldScale) : 1f;
            Tree tree = Take();

            tree.Playing = true;
            tree.Elapsed = 0;
            tree.Step = -1;
            tree.From = new Vector2(from.X, from.Y);
            tree.To = new Vector2(to.X, to.Y);
            tree.Length = Vector2.Distance(tree.From, tree.To);
            tree.Field = field;
            tree.GlowWidth = _look.GlowWidth * field;
            tree.KinksPerUnit = _look.KinksPerUnit / field;

            RollTopology(tree);
            AssignBolts(tree, _look.Thickness * field, _look.Amplitude * field);
            Update(tree);
        }

        // 끝난 나무는 숨긴다.
        public void Age(float delta)
        {
            // 매 프레임 경로: 인덱스로 돈다.
            for (int i = 0; i < _trees.Count; i++)
            {
                Tree tree = _trees[i];

                if (!tree.Playing)
                    continue;

                tree.Elapsed += delta;
                Update(tree);
            }
        }

        // 판이 바뀌거나 판을 정리할 때 모든 나무를 지운다.
        public void Reset()
        {
            foreach (Tree tree in _trees)
                foreach (Bolt bolt in tree.Bolts)
                    Object.Destroy(bolt.Renderer.gameObject);

            _trees.Clear();
            _nextSeed = 0;
        }

        public void Dispose()
        {
            Object.Destroy(_root.gameObject);
            Object.Destroy(_quad);
        }

        // 나무의 얼개: 갈래가 어느 마디에서 어느 쪽으로 갈라지고, 잔가지가 어느 가닥의 어느 마디에서 어느 각으로 뻗는가. 홉마다 한 번.
        private void RollTopology(Tree tree)
        {
            int trunkKinks = tree.Trunk.Length - 2;
            // 갈래는 줄기의 앞쪽 절반 마디에서 갈라진다 — 뒤쪽에서 갈라지면 벌어질 자리가 없다.
            int forkRange = Mathf.Max(1, (trunkKinks + 1) / 2);

            for (int f = 0; f < tree.ForkPaths.Length; f++)
            {
                tree.ForkStart[f] = Random.Range(0, forkRange);
                // 첫 둘은 좌우로 하나씩, 그 뒤는 무작위.
                tree.ForkSide[f] = f < 2 ? (f == 0 ? 1f : -1f) : (Random.value < 0.5f ? 1f : -1f);
            }

            Vector2 turn = _look.TwigTurnDegrees * Mathf.Deg2Rad;

            for (int t = 0; t < tree.TwigParent.Length; t++)
            {
                int parent = Random.Range(-1, tree.ForkPaths.Length);
                int nodes = parent < 0 ? tree.Trunk.Length : tree.ForkPaths[parent].Length;
                tree.TwigParent[t] = parent;
                // 중간 마디에서만(양 끝 From·To는 뺀다). 중간 마디는 적어도 하나 있다(Look이 보장).
                tree.TwigNode[t] = Random.Range(1, nodes - 1);
                tree.TwigTurn[t] = Random.Range(Mathf.Min(turn.x, turn.y), Mathf.Max(turn.x, turn.y)) * (Random.value < 0.5f ? -1f : 1f);
                tree.TwigReach[t] = Random.Range(0.5f, 1f);
            }
        }

        // 칸마다 굵기·잔떨림·나타나는 때·보이는 시간. 칸은 From 쪽부터 GrowSeconds 간격으로 나타난다:
        // 줄기 i번째 칸은 i칸 뒤, 갈래는 갈라지는 마디에 줄기가 닿은 뒤, 잔가지는 뿌리 마디에 가닥이 닿은 뒤 한 칸.
        private void AssignBolts(Tree tree, float thickness, float amplitude)
        {
            float grow = _look.GrowSeconds;
            float duration = _look.Duration;
            int b = 0;

            for (int i = 0; i < tree.Trunk.Length - 1; i++)
            {
                Bolt bolt = tree.Bolts[b++];
                bolt.Delay = i * grow;
                bolt.Duration = duration;
                bolt.Thickness = thickness;
                bolt.Amplitude = amplitude;
            }

            for (int f = 0; f < tree.ForkPaths.Length; f++)
            {
                for (int k = 0; k < tree.ForkPaths[f].Length - 1; k++)
                {
                    Bolt bolt = tree.Bolts[b++];
                    bolt.Delay = (tree.ForkStart[f] + 1 + k) * grow;
                    bolt.Duration = duration * ForkLife;
                    bolt.Thickness = thickness * _look.ForkThickness;
                    bolt.Amplitude = amplitude;
                }
            }

            for (int t = 0; t < tree.TwigParent.Length; t++)
            {
                int parent = tree.TwigParent[t];
                int node = tree.TwigNode[t];
                int reached = parent < 0 ? node : tree.ForkStart[parent] + 1 + node;

                Bolt bolt = tree.Bolts[b++];
                bolt.Delay = (reached + 1) * grow;
                bolt.Duration = duration * _look.TwigLife;
                bolt.Thickness = thickness * _look.TwigThickness;
                bolt.Amplitude = amplitude * TwigAmplitude;
            }
        }

        // 지금 시각의 나무를 보인다: 깜빡임 단계가 바뀌면 마디를 다시 뽑고, 칸마다 아직 전·보이는 중·끝남을 가른다. 모두 끝나면 나무를 쉬게 한다.
        private void Update(Tree tree)
        {
            int step = Mathf.FloorToInt(tree.Elapsed * _look.FlickerRate);

            if (step != tree.Step)
            {
                tree.Step = step;
                Reshape(tree);
            }

            bool alive = false;

            for (int i = 0; i < tree.Bolts.Length; i++)
            {
                Bolt bolt = tree.Bolts[i];
                float local = tree.Elapsed - bolt.Delay;

                if (local >= bolt.Duration)
                {
                    bolt.Renderer.enabled = false;
                    continue;
                }

                alive = true;

                if (local < 0)
                {
                    bolt.Renderer.enabled = false;
                    continue;
                }

                bolt.Renderer.enabled = true;
                Apply(tree, bolt, local / bolt.Duration);
            }

            if (!alive)
            {
                tree.Playing = false;
                Hide(tree);
            }
        }

        // 마디 좌표를 다시 뽑고 칸을 그 자리에 놓는다. 얼개(어느 마디·어느 쪽)는 그대로라 나무가 떨 뿐 다른 나무가 되지는 않는다.
        private void Reshape(Tree tree)
        {
            float field = tree.Field;
            Vector2 from = tree.From;
            Vector2 to = tree.To;
            Vector2 dir = (to - from) / Mathf.Max(tree.Length, 1e-4f);
            Vector2 normal = new(-dir.y, dir.x);

            // 줄기: 마디를 직선 위에 고르게(조금 흔들어) 두고 수직으로 벗어난다. 양 끝에 가까울수록 덜 벗어나 From·To에 모인다.
            int trunkKinks = tree.Trunk.Length - 2;
            float wander = Mathf.Min(_look.Wander * field, tree.Length * MaxWanderRatio);
            tree.Trunk[0] = from;
            tree.Trunk[trunkKinks + 1] = to;

            for (int i = 1; i <= trunkKinks; i++)
            {
                float u = (i + Random.Range(-0.3f, 0.3f)) / (trunkKinks + 1);
                float offset = Random.Range(-1f, 1f) * wander * Mathf.Sin(Mathf.PI * u);
                tree.Trunk[i] = from + dir * (u * tree.Length) + normal * offset;
            }

            // 갈래: 갈라진 마디에서 To까지의 직선을 활처럼 한쪽으로 휘어 간다. 가운데가 가장 벌어지고 To에서 다시 모인다.
            for (int f = 0; f < tree.ForkPaths.Length; f++)
            {
                Vector2[] path = tree.ForkPaths[f];
                Vector2 start = tree.Trunk[tree.ForkStart[f] + 1];
                Vector2 chord = to - start;
                float chordLength = chord.magnitude;
                Vector2 chordDir = chord / Mathf.Max(chordLength, 1e-4f);
                Vector2 chordNormal = new(-chordDir.y, chordDir.x);
                float spread = Mathf.Min(_look.ForkSpread * field, chordLength * MaxForkSpreadRatio);
                int forkKinks = path.Length - 2;
                path[0] = start;
                path[forkKinks + 1] = to;

                for (int k = 1; k <= forkKinks; k++)
                {
                    float u = (k + Random.Range(-0.25f, 0.25f)) / (forkKinks + 1);
                    float offset = tree.ForkSide[f] * spread * Mathf.Sin(Mathf.PI * u) * Random.Range(0.7f, 1.2f);
                    path[k] = start + chordDir * (u * chordLength) + chordNormal * offset;
                }
            }

            // 칸을 마디 사이에 놓는다. 줄기 → 갈래 → 잔가지 순(AssignBolts와 같은 순서).
            int b = 0;

            for (int i = 0; i < tree.Trunk.Length - 1; i++)
                Place(tree, tree.Bolts[b++], tree.Trunk[i], tree.Trunk[i + 1]);

            for (int f = 0; f < tree.ForkPaths.Length; f++)
            {
                Vector2[] path = tree.ForkPaths[f];

                for (int k = 0; k < path.Length - 1; k++)
                    Place(tree, tree.Bolts[b++], path[k], path[k + 1]);
            }

            // 잔가지: 뿌리 마디에서 그 가닥의 진행 방향(앞뒤 마디를 이은 방향)에서 꺾인 각으로 뻗는다. 끝은 허공.
            for (int t = 0; t < tree.TwigParent.Length; t++)
            {
                Vector2[] path = tree.TwigParent[t] < 0 ? tree.Trunk : tree.ForkPaths[tree.TwigParent[t]];
                int node = tree.TwigNode[t];
                Vector2 root = path[node];
                Vector2 heading = path[node + 1] - path[node - 1];
                float angle = Mathf.Atan2(heading.y, heading.x) + tree.TwigTurn[t];
                float reach = tree.Length * _look.TwigLength * tree.TwigReach[t];
                Vector2 tip = root + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach;
                Place(tree, tree.Bolts[b++], root, tip);
            }
        }

        // 칸 하나를 두 마디 사이에 놓고 새 씨앗을 준다. 사각형을 두 점의 가운데에 놓고 선 방향으로 돌린다. 크기는 길이 + 꺾임 + 굵기 + 번짐이 들어갈 만큼.
        private void Place(Tree tree, Bolt bolt, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            bolt.Length = delta.magnitude;
            bolt.Seed = _nextSeed++ % (int)SeedRange * 0.731f;

            float reach = bolt.Thickness * 0.5f + tree.GlowWidth * GlowReach;
            Transform view = bolt.Renderer.transform;
            view.localPosition = new Vector3((from.x + to.x) * 0.5f, (from.y + to.y) * 0.5f, 0);
            view.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            view.localScale = new Vector3(bolt.Length + 2 * reach, 2 * (bolt.Amplitude + reach), 1) * MeshMargin;
        }

        private void Apply(Tree tree, Bolt bolt, float progress)
        {
            bolt.Renderer.GetPropertyBlock(_properties);
            _properties.SetFloat(_lengthId, bolt.Length);
            _properties.SetFloat(_segmentsId, Mathf.Max(1, bolt.Length * tree.KinksPerUnit));
            _properties.SetFloat(_amplitudeId, bolt.Amplitude);
            _properties.SetFloat(_seedId, bolt.Seed);
            _properties.SetFloat(_thicknessId, bolt.Thickness);
            _properties.SetColor(_colorId, _look.Color);
            _properties.SetColor(_coreColorId, _look.CoreColor);
            _properties.SetFloat(_glowWidthId, tree.GlowWidth);
            _properties.SetFloat(_glowStrengthId, _look.GlowStrength);
            _properties.SetFloat(_fadeId, _look.Fade(Mathf.Clamp01(progress)));
            bolt.Renderer.SetPropertyBlock(_properties);
        }

        private static void Hide(Tree tree)
        {
            for (int i = 0; i < tree.Bolts.Length; i++)
                tree.Bolts[i].Renderer.enabled = false;
        }

        // 쉬는 나무를 먼저 쓴다. 없으면 상한까지 새로 만들고, 상한이면 가장 오래된 나무를 다시 쓴다.
        private Tree Take()
        {
            Tree oldest = null;

            for (int i = 0; i < _trees.Count; i++)
            {
                Tree tree = _trees[i];

                if (!tree.Playing)
                    return tree;

                if (oldest == null || tree.Elapsed > oldest.Elapsed)
                    oldest = tree;
            }

            if (_trees.Count < _look.MaxHops)
            {
                Tree created = Create();
                _trees.Add(created);
                return created;
            }

            return oldest;
        }

        // 나무 하나와 그 칸(사각형)들을 만든다. 배열 크기는 Look의 지금 값을 따른다.
        private Tree Create()
        {
            int forks = _look.Forks;
            int twigs = _look.Twigs;
            var tree = new Tree
            {
                Trunk = new Vector2[_look.TrunkKinks + 2],
                ForkPaths = new Vector2[forks][],
                ForkStart = new int[forks],
                ForkSide = new float[forks],
                TwigParent = new int[twigs],
                TwigNode = new int[twigs],
                TwigTurn = new float[twigs],
                TwigReach = new float[twigs],
                Bolts = new Bolt[_look.BoltsPerHop],
            };

            for (int f = 0; f < forks; f++)
                tree.ForkPaths[f] = new Vector2[_look.ForkKinks + 2];

            for (int b = 0; b < tree.Bolts.Length; b++)
                tree.Bolts[b] = CreateBolt();

            return tree;
        }

        private Bolt CreateBolt()
        {
            var view = new GameObject("Star Lightning Bolt");
            view.transform.SetParent(_root, false);

            view.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = view.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _look.Material;
            renderer.sortingOrder = SortingOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;

            return new Bolt { Renderer = renderer };
        }

        // 한 변이 1인 사각형. 칸의 길이·폭은 transform의 배율로 맞춘다.
        private static Mesh CreateQuad()
        {
            var mesh = new Mesh
            {
                name = "Star Lightning Bolt Quad",
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
