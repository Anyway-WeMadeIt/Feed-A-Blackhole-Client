using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 화면 전환 연출(블랙홀 아이리스): 화면이 가운데 원으로 빨려 들어가 덮였다가, 새 화면에서 원이 커지며 열린다.
    // 다 덮인 순간에 부르는 쪽의 일(루트 바꾸기, 판 시작·정리)을 한다. 그래서 화면이 바뀌는 순간과 그때의 끊김이 보이지 않는다.
    // - Play: 덮기 → 일 → 잠깐 머묾 → 열기. 일이 비동기(Task)면 끝날 때까지 덮인 채 기다린다.
    //   아직 일을 하기 전에 다시 불리면 마지막 요청의 일만 한다(두 번 누름). 열리는 중에 불리면 그 자리에서 다시 덮는다.
    // - 전환하는 동안에는 화면 전체의 입력을 막는다. 다 열리면 덮개를 꺼서, 가만히 있는 동안 그리는 비용도 입력 막기도 없다.
    // - 처음에는 덮여 있다. 첫 Play(앱 시작의 타이틀)가 덮인 채 일을 하고 열리므로, 앱이 원이 열리며 시작한다.
    // 외형은 ScreenTransitionLook, 셰이더는 BlackHole/UI Screen Iris. 머티리얼은 복제해 쓴다(공유 에셋을 바꾸지 않도록).
    // timeScale과 관계없이 움직인다.
    [DisallowMultipleComponent]
    public sealed class ScreenTransition : MonoBehaviour
    {
        // 한 프레임에 나아가는 최대 시간. 화면을 바꾼 프레임이 길어도 여는 연출을 건너뛰지 않는다.
        private const float MaxStep = 1f / 30;
        // 원이 화면 밖으로 나가는 여유(둘레 빛 폭의 배수). 다 열렸을 때도, 다 덮였을 때도 둘레 빛이 남지 않는다.
        private const float RimReach = 4;

        private static readonly int _colorId = Shader.PropertyToID("_Color");
        private static readonly int _radiusId = Shader.PropertyToID("_Radius");
        private static readonly int _rimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int _rimWidthId = Shader.PropertyToID("_RimWidth");
        private static readonly int _rimStrengthId = Shader.PropertyToID("_RimStrength");

        private ScreenTransitionLook _look;
        private RectTransform _rect;
        private Image _cover;
        private Material _material;
        private Coroutine _routine;
        private Func<Task> _pending;
        // 0이면 다 열림, 1이면 다 덮임. 시간에 따라 곧게 움직이고, 보이는 반지름은 곡선(Close)으로 바꾼다.
        private float _progress = 1;

        // 덮기·일·머묾·열기 중 어느 단계든 전환 중이면 참. 다 열리면 거짓.
        public bool IsPlaying => _routine != null;

        // canvas: 덮개를 둘 캔버스(맨 위 캔버스). 덮개는 그 캔버스의 마지막 자식이라 모든 화면·패널 위에 그려진다.
        public static ScreenTransition Create(Transform canvas, ScreenTransitionLook look)
        {
            if (canvas == null)
                throw new ArgumentNullException(nameof(canvas));

            if (look == null || look.Material == null)
                throw new ArgumentException("화면 전환 외형과 그 머티리얼이 필요하다.", nameof(look));

            var view = new GameObject("Screen Transition", typeof(RectTransform));
            view.layer = canvas.gameObject.layer;

            var rect = (RectTransform)view.transform;
            rect.SetParent(canvas, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            var cover = view.AddComponent<Image>();
            cover.raycastTarget = true;

            var transition = view.AddComponent<ScreenTransition>();
            transition.Initialize(look, rect, cover);
            return transition;
        }

        public void Play(Action atCovered)
        {
            if (atCovered == null)
                throw new ArgumentNullException(nameof(atCovered));

            Play(() =>
            {
                atCovered();
                return Task.CompletedTask;
            });
        }

        public void Play(Func<Task> atCovered)
        {
            _pending = atCovered ?? throw new ArgumentNullException(nameof(atCovered));

            if (_routine == null)
                _routine = StartCoroutine(Run());
        }

        private void Initialize(ScreenTransitionLook look, RectTransform rect, Image cover)
        {
            _look = look;
            _rect = rect;
            _cover = cover;
            _material = new Material(look.Material) { name = look.Material.name + " (Instance)" };
            _cover.material = _material;
            Apply();
        }

        private void OnDestroy()
        {
            if (_material != null)
                Destroy(_material);
        }

        private IEnumerator Run()
        {
            _cover.enabled = true;

            while (_pending != null)
            {
                // 덮기: 지금 자리에서 다 덮일 때까지. 이미 덮여 있으면 곧바로 지나간다.
                while (_progress < 1)
                {
                    yield return null;
                    _progress = Mathf.Min(1, _progress + Step() / _look.CoverSeconds);
                    Apply();
                }

                // 덮인 채 일을 한다. 일하는 동안 새 요청이 오면 이어서 한다.
                while (_pending != null)
                {
                    Task work = Begin(_pending);
                    _pending = null;

                    while (!work.IsCompleted)
                        yield return null;

                    // 일에서 난 예외는 여기서 남기고 전환을 이어 간다. 부르는 쪽은 잡지 않는다.
                    if (work.IsFaulted)
                    {
                        foreach (Exception error in work.Exception.InnerExceptions)
                            Debug.LogException(error);
                    }
                }

                // 머묾: 바뀐 화면이 덮인 채 한 번은 그려지게 잠깐 둔다. 그 사이 새 요청이 오면 덮인 채 이어서 한다.
                for (float held = 0; held < _look.HoldSeconds && _pending == null; held += Step())
                    yield return null;

                // 열기. 열리는 중에 새 요청이 오면 그 자리에서 다시 덮는다(바깥 반복).
                while (_progress > 0 && _pending == null)
                {
                    yield return null;

                    if (_pending != null)
                        break;

                    _progress = Mathf.Max(0, _progress - Step() / _look.RevealSeconds);
                    Apply();
                }
            }

            _cover.enabled = false;
            _routine = null;
        }

        // 일을 시작한다. 곧바로 던진 예외도 실패한 Task로 바꿔 전환이 멈추지 않게 한다.
        private static Task Begin(Func<Task> work)
        {
            try
            {
                return work() ?? Task.CompletedTask;
            }
            catch (Exception error)
            {
                return Task.FromException(error);
            }
        }

        private static float Step() => Mathf.Min(Time.unscaledDeltaTime, MaxStep);

        // 진행을 반지름으로 바꿔 셰이더에 넘긴다. 다 열림: 원이 화면 대각선 밖, 다 덮임: 원이 0보다 작다.
        private void Apply()
        {
            float rim = _look.RimWidth;
            float open = 0.5f * _rect.rect.size.magnitude + rim * RimReach;
            float closed = -rim * RimReach;
            float radius = Mathf.LerpUnclamped(open, closed, _look.Close(_progress));

            _material.SetColor(_colorId, _look.Color);
            _material.SetFloat(_radiusId, radius);
            _material.SetColor(_rimColorId, _look.RimColor);
            _material.SetFloat(_rimWidthId, rim);
            _material.SetFloat(_rimStrengthId, _look.RimStrength);
        }
    }
}
