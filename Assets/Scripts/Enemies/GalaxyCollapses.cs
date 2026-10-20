using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 은하의 나선형 붕괴 연출 [임시]: 죽은 은하의 화면(중심 + 나선팔)을 넘겨받아, 나선팔이 점점 빨리 감기면서 전체가 중심으로 말려 들어가 사라지게 한다.
    // 적 화면(EnemyView)이 사망 기록으로 은하의 죽음을 알고 화면을 지우는 대신 여기로 넘긴다. 다 끝난 화면은 여기서 지운다.
    // 게임 상태를 바꾸지 않는다. 일시정지 중에는 멈춘다.
    internal sealed class GalaxyCollapses
    {
        // 붕괴 전체 시간(초).
        private const float Duration = 0.7f;
        // 끝날 때의 자전 속도 배율(시작 속도 대비).
        private const float SpinUp = 8f;

        private sealed class Collapse
        {
            public Transform Root;
            public Transform Arms;
            public SpriteRenderer[] Renderers;
            public Color[] Colors;
            public Vector3 StartScale;
            public float Spin;
            public float Elapsed;
        }

        private readonly List<Collapse> _collapses = new();

        // root: 은하 화면의 뿌리(중심 스프라이트). arms: 그 아래에서 자전하던 나선팔. spin: 자전 속도(도/초, 부호가 방향).
        public void Play(Transform root, Transform arms, float spin)
        {
            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>();
            var colors = new Color[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
                colors[i] = renderers[i].color;

            root.gameObject.name += " (Collapse)";
            _collapses.Add(new Collapse
            {
                Root = root,
                Arms = arms,
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

            for (int i = _collapses.Count - 1; i >= 0; i--)
            {
                Collapse collapse = _collapses[i];
                collapse.Elapsed += delta;
                float t = Mathf.Clamp01(collapse.Elapsed / Duration);

                if (t >= 1 || collapse.Root == null)
                {
                    if (collapse.Root != null)
                        Object.Destroy(collapse.Root.gameObject);

                    _collapses.RemoveAt(i);
                    continue;
                }

                // 처음엔 천천히, 끝으로 갈수록 빠르게 말려 들어간다.
                float shrink = 1 - t * t;
                collapse.Root.localScale = collapse.StartScale * shrink;

                if (collapse.Arms != null)
                    collapse.Arms.Rotate(0, 0, collapse.Spin * Mathf.Lerp(1, SpinUp, t) * delta);

                for (int j = 0; j < collapse.Renderers.Length; j++)
                {
                    Color color = collapse.Colors[j];
                    color.a *= 1 - t;
                    collapse.Renderers[j].color = color;
                }
            }
        }

        public void Reset()
        {
            foreach (Collapse collapse in _collapses)
            {
                if (collapse.Root != null)
                    Object.Destroy(collapse.Root.gameObject);
            }

            _collapses.Clear();
        }
    }
}
