using System;
using System.Collections.Generic;
using BlackHole.Core;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 튜토리얼 판의 입력 안내. 판 규칙과 무관한 화면이다.
    // - IdleSeconds 동안 누르지 않고 적도 죽지 않으면, 소행성 하나에 조여드는 원과 "꾹 눌러서 부수세요"를 띄운다.
    // - 누르거나 적이 죽으면 곧바로 숨기고 시간을 처음부터 다시 센다. 정지 중에는 시간이 흐르지 않고 안내도 그대로 멈춘다.
    // - 안내하는 소행성은 죽을 때까지 같은 것을 따라간다(공전을 따라 움직인다).
    // 튜토리얼 판이 아니면 아무것도 보이지 않는다(BattleSystem이 active로 알린다).
    internal sealed class TutorialHintView : IDisposable
    {
        public const float IdleSeconds = 10f;

        private const float FadeSeconds = 0.4f;
        // 원이 대상 반지름의 RingFrom배에서 RingTo배까지 조여드는 한 주기(초). 조일수록 진해졌다가 끝에서 사라진다.
        private const float PulseSeconds = 1.2f;
        private const float RingFrom = 2.4f;
        private const float RingTo = 1.15f;
        private const float RingWidth = 0.05f;
        // 글자: TextMeshPro(월드) 글자 크기와, 대상 아래 가장자리에서 글자 중심까지의 거리(월드 단위).
        private const float TextSize = 3f;
        private const float TextGap = 0.45f;
        private const int TextSortingOrder = 12;
        private static readonly Color HintColor = new(0.96f, 0.95f, 1f, 1f);

        private readonly LineStrokes _strokes;
        private readonly LineRenderer _ring;
        private readonly TextMeshPro _text;
        private readonly Localizer _localizer;

        private float _idle;
        private float _shown;   // 0 ~ 1. 나타나는 동안 오른다.
        private float _time;    // 맥동 시계.
        private Enemy _target;

        public TutorialHintView(Transform parent, Localizer localizer)
        {
            _localizer = localizer ?? throw new ArgumentNullException(nameof(localizer));
            _strokes = new LineStrokes(parent, "Tutorial Hint");
            _ring = _strokes.Line("Hint Ring", RingWidth, HintColor);

            var text = new GameObject("Tutorial Hint Text");
            text.transform.SetParent(parent, false);
            _text = text.AddComponent<TextMeshPro>();
            _text.fontSize = TextSize;
            _text.alignment = TextAlignmentOptions.Center;
            _text.rectTransform.sizeDelta = new Vector2(12, 1.5f);
            _text.GetComponent<MeshRenderer>().sortingOrder = TextSortingOrder;

            Hide();
        }

        // 매 프레임 부른다. active: 튜토리얼 판이 진행 중인가. paused: 정지 중인가(안내를 그대로 멈춘다).
        public void Synchronize(World world, bool active, bool paused, float delta)
        {
            if (!active)
            {
                _idle = 0;
                Hide();
                return;
            }

            if (paused)
                return;

            // 눌렀거나 이번 Step에 적이 죽었으면 할 줄 아는 것이다 — 숨기고 다시 센다.
            if (world.AimPoint.HasValue || world.Deaths.Count > 0)
            {
                _idle = 0;
                Hide();
                return;
            }

            _idle += delta;

            if (_idle < IdleSeconds)
                return;

            Enemy target = TargetOf(world);

            if (target == null)
            {
                Hide();
                return;
            }

            Show(target, delta);
        }

        // 판이 바뀌거나 끝날 때.
        public void Reset()
        {
            _idle = 0;
            Hide();
        }

        public void Dispose()
        {
            _strokes.Dispose();

            if (_text != null)
                Object.Destroy(_text.gameObject);
        }

        private void Show(Enemy target, float delta)
        {
            if (_target != target || _shown <= 0)
            {
                // 새로 나타날 때 지금 언어로 글자를 쓴다(판 중에 언어를 바꿔도 다음 안내부터 맞는다).
                _text.text = _localizer.Get(UIText.Tutorial.HintHold);
                _time = 0;
            }

            _target = target;
            _shown = Mathf.Min(1, _shown + delta / FadeSeconds);
            _time += delta;

            float radius = target.Stats.Radius;
            float t = _time % PulseSeconds / PulseSeconds;
            float eased = 1 - (1 - t) * (1 - t);
            Color ring = HintColor;
            ring.a *= Mathf.Sin(t * Mathf.PI) * _shown;

            LineStrokes.SetCircle(_ring, target.Position, radius * Mathf.Lerp(RingFrom, RingTo, eased));
            LineStrokes.SetColor(_ring, ring);
            _ring.gameObject.SetActive(true);

            Color text = HintColor;
            text.a *= _shown;
            _text.color = text;
            _text.transform.localPosition = new Vector3(target.Position.X, target.Position.Y - radius - TextGap, 0);
            _text.gameObject.SetActive(true);
        }

        private void Hide()
        {
            _shown = 0;
            _target = null;
            _ring.gameObject.SetActive(false);
            _text.gameObject.SetActive(false);
        }

        // 따라가던 소행성이 살아 있으면 그대로, 아니면 살아 있는 첫 적(픽업 제외).
        private Enemy TargetOf(World world)
        {
            if (_target != null && _target.IsAlive)
                return _target;

            IReadOnlyList<Enemy> enemies = world.Enemies;

            for (int i = 0; i < enemies.Count; i++)
            {
                if (!enemies[i].Definition.IsPickup)
                    return enemies[i];
            }

            return null;
        }
    }
}
