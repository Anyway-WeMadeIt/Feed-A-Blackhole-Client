using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 적의 나선형 소멸 연출 [임시]: 죽은 적의 화면(Container 전체)을 넘겨받아, 점점 빨리 자전하면서 중심으로 말려 들어가 사라지게 한다(은하).
    // 적 화면(EnemyView)이 사망 기록으로 죽음을 알고 화면을 지우는 대신 여기로 넘긴다. 다 끝난 화면은 여기서 지운다.
    // 게임 상태를 바꾸지 않는다. 일시정지 중에는 멈춘다.
    internal sealed class EnemyVanishes
    {
        // 소멸 전체 시간(초).
        private const float Duration = 0.7f;
        // 끝날 때의 자전 속도 배율(시작 속도 대비).
        private const float SpinUp = 8f;

        private sealed class Vanish
        {
            public Transform Root;
            public SpriteRenderer[] Renderers;
            public Color[] Colors;
            public Vector3 StartScale;
            public float Spin;
            public float Elapsed;
        }

        private readonly List<Vanish> _vanishes = new();

        // root: 적 화면의 Container(위치·크기·자전). spin: 자전 속도(도/초, 부호가 방향).
        public void Play(Transform root, float spin)
        {
            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>();
            var colors = new Color[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
                colors[i] = renderers[i].color;

            root.gameObject.name += " (Collapse)";
            _vanishes.Add(new Vanish
            {
                Root = root,
                Renderers = renderers,
                Colors = colors,
                StartScale = root.localScale,
                Spin = spin,
            });
        }

        public void Advance(bool paused, float delta)
        {
            if (paused || delta <= 0)
                return;

            for (int i = _vanishes.Count - 1; i >= 0; i--)
            {
                Vanish vanish = _vanishes[i];
                vanish.Elapsed += delta;
                float t = Mathf.Clamp01(vanish.Elapsed / Duration);

                if (t >= 1 || vanish.Root == null)
                {
                    if (vanish.Root != null)
                        Object.Destroy(vanish.Root.gameObject);

                    _vanishes.RemoveAt(i);
                    continue;
                }

                // 처음엔 천천히, 끝으로 갈수록 빠르게 말려 들어간다.
                float shrink = 1 - t * t;
                vanish.Root.localScale = vanish.StartScale * shrink;
                vanish.Root.Rotate(0, 0, vanish.Spin * Mathf.Lerp(1, SpinUp, t) * delta);

                for (int j = 0; j < vanish.Renderers.Length; j++)
                {
                    Color color = vanish.Colors[j];
                    color.a *= 1 - t;
                    vanish.Renderers[j].color = color;
                }
            }
        }

        public void Reset()
        {
            foreach (Vanish vanish in _vanishes)
            {
                if (vanish.Root != null)
                    Object.Destroy(vanish.Root.gameObject);
            }

            _vanishes.Clear();
        }
    }
}
