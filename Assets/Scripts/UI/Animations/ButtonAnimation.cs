using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 버튼 하나의 연출: 손을 올리면(PC) 살짝 커지고, 누르면 줄었다가, 떼면 튀어오르며 돌아온다. 모바일은 누름·뗌만 온다.
    // 크기는 이 컴포넌트가, 색·그림은 같은 버튼의 Button 전환(Color Tint·Sprite Swap)이 상태에 맞춰 바꾼다.
    // 입력을 스스로 받지 않는다 — 화면이 BindEvent로 받은 사건에서 Hover·Leave·Press·Release를 부른다.
    //
    // 움직이는 동안만 코루틴이 돈다. Update가 없어서 가만히 있는 버튼에는 매 프레임 비용이 없다.
    // 꺼지면(화면이 닫히면) ResetAll로 크기·색을 되돌리고 상태를 비운다 — 다시 켜질 때 이전 상태가 남지 않는다.
    // localScale을 쓰므로 다른 코드가 크기를 움직이는 버튼(모드 선택 카드 루트 등)에는 붙이지 않는다.
    // 레이아웃 그룹과 Presentation은 크기를 건드리지 않는다.
    [DisallowMultipleComponent]
    public sealed class ButtonAnimation : MonoBehaviour
    {
        // 한 프레임에 나아가는 최대 시간. 프레임이 끊겨도 스프링이 튀어 나가지 않게 한다.
        private const float MaxStep = 1f / 30;
        private const float RestEpsilon = 0.0005f;

        [SerializeField, Min(0)] private float _hoverScale = 1.06f;
        [SerializeField, Min(0)] private float _pressScale = 0.93f;
        [Tooltip("클수록 빨리 따라간다.")]
        [SerializeField, Min(1)] private float _stiffness = 500;
        [Tooltip("작을수록 목표를 지나쳤다가 돌아오며 더 튄다.")]
        [SerializeField, Min(0)] private float _damping = 18;

        private bool _initialized;
        private Selectable _selectable;
        private Vector3 _rest;
        private Coroutine _routine;
        private float _scale = 1;
        private float _velocity;
        private bool _hovered;
        private bool _pressed;

        // 올림·누름 소리. 화면이 IUISoundView.SetSounds에서 넣는다(SetSounds). 없으면 소리 없이 움직인다.
        public UISounds Sounds { get; set; }

        // root 아래 모든 버튼 연출에 소리를 넣는다. 화면의 IUISoundView.SetSounds가 부른다.
        public static void SetSounds(Component root, UISounds sounds)
        {
            foreach (ButtonAnimation animation in root.GetComponentsInChildren<ButtonAnimation>(true))
                animation.Sounds = sounds;
        }

        // 버튼의 연출. 프리팹에 없으면 기본값으로 붙인다(BindEvent가 UI_EventHandler를 붙이는 것과 같다).
        public static ButtonAnimation Of(Selectable button)
        {
            if (button == null)
                return null;

            if (!button.TryGetComponent(out ButtonAnimation animation))
                animation = button.gameObject.AddComponent<ButtonAnimation>();

            return animation;
        }

        // 막힌 버튼은 반응하지 않는다.
        public void Hover()
        {
            if (!IsInteractable())
                return;

            Sounds?.PlayHover();
            _hovered = true;
            Play();
        }

        // 버튼을 벗어나면 누름 모양도 푼다(Selectable의 눌림 색과 같다).
        public void Leave()
        {
            _hovered = false;
            _pressed = false;
            Play();
        }

        public void Press()
        {
            if (!IsInteractable())
                return;

            Sounds?.PlayClick();
            _pressed = true;
            Play();
        }

        public void Release()
        {
            _pressed = false;
            Play();
        }

        // 한 번 튀어오른다(확정·완료 같은 순간). 스프링에 속도를 더해 커졌다가 제자리로 돌아온다. 막힌 버튼도 튄다.
        public void Punch(float kick = 2.5f)
        {
            EnsureInitialized();
            _velocity += kick;
            Play();
        }

        // 진행 중인 움직임을 멈추고, 원래 크기와 원래 색으로 되돌린 뒤 상태를 비운다.
        public void ResetAll()
        {
            EnsureInitialized();

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            _hovered = false;
            _pressed = false;
            _scale = 1;
            _velocity = 0;
            transform.localScale = _rest;
            RestoreColor();
        }

        private void Awake() => EnsureInitialized();

        private void OnDisable() => ResetAll();

        // 기준 크기는 처음 쓸 때(제자리일 때) 잡는다. 화면이 꺼진 채 초기화되어 Awake보다 먼저 불릴 수 있다.
        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            _selectable = GetComponent<Selectable>();
            _rest = transform.localScale;
        }

        // 목표로 움직이기 시작한다. 이미 움직이는 중이면 코루틴이 새 목표를 따라가므로 할 일이 없다.
        // 꺼져 있으면 코루틴을 시작할 수 없으므로 제자리로 돌린다.
        private void Play()
        {
            EnsureInitialized();

            if (!isActiveAndEnabled)
            {
                ResetAll();
                return;
            }

            if (_routine != null || (_velocity == 0 && Mathf.Approximately(_scale, Goal())))
                return;

            _routine = StartCoroutine(Animate());
        }

        // 첫 걸음은 다음 프레임에 딛는다. 그래서 StartCoroutine 안에서 끝나는 일이 없고, _routine은 늘 살아 있는 코루틴이다.
        // 일시정지(timeScale 0)에도 움직인다.
        private IEnumerator Animate()
        {
            while (true)
            {
                yield return null;

                float goal = Goal();
                if (Mathf.Abs(goal - _scale) < RestEpsilon && Mathf.Abs(_velocity) < RestEpsilon)
                {
                    _scale = goal;
                    _velocity = 0;
                    Apply();
                    break;
                }

                float dt = Mathf.Min(Time.unscaledDeltaTime, MaxStep);
                _velocity += (goal - _scale) * _stiffness * dt;
                _velocity *= Mathf.Exp(-_damping * dt);
                _scale += _velocity * dt;
                Apply();
            }

            _routine = null;
        }

        // 막힌 버튼은 제자리. 누름이 올림보다 먼저다(터치는 누르는 동안 올림도 함께 온다).
        private float Goal()
        {
            if (!IsInteractable())
                return 1;

            if (_pressed)
                return _pressScale;

            return _hovered ? _hoverScale : 1;
        }

        private void Apply() => transform.localScale = _rest * _scale;

        // 색은 Color Tint가 맡는다. 되돌릴 때는 지금 상태(켜짐·막힘)의 기본 색으로 곧바로 맞춘다.
        // Sprite Swap은 Selectable이 꺼질 때 스스로 기본 그림으로 돌려 놓으므로 할 일이 없다.
        private void RestoreColor()
        {
            if (_selectable == null
                || _selectable.transition != Selectable.Transition.ColorTint
                || _selectable.targetGraphic == null)
            {
                return;
            }

            ColorBlock colors = _selectable.colors;
            Color tint = _selectable.IsInteractable() ? colors.normalColor : colors.disabledColor;
            _selectable.targetGraphic.CrossFadeColor(tint * colors.colorMultiplier, 0, true, true);
        }

        private bool IsInteractable()
        {
            EnsureInitialized();
            return _selectable == null || _selectable.IsInteractable();
        }
    }
}
