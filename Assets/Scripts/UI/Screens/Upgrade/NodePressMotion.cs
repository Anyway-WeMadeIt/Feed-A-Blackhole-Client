using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 업그레이드 트리에서 터치로 누른 노드의 크기 연출과, 꾹 눌러 산 직후 초록 링이 사라지는 연출.
    // 누름 상태(꾹 누르는 노드·살 준비)는 NodeTreeView가 갖고, 이 클래스는 받은 값으로 노드 크기와 링을 움직인다. Tick은 NodeTreeView.Update가 부른다.
    // - 닿아 있으면 목표 크기로 곧장(PressSeconds): 살 수 있는 노드는 부풀고(PuffScale), 그 밖은 살짝 눌린다(PressScale).
    // - 떼면 스프링으로 1로 돌아온다(살짝 지나쳤다 바로 멈춤). 사며 떼면 잠깐 더 부풀었다가(예비 동작) 찰싹 돌아온다.
    internal sealed class NodePressMotion
    {
        private const float FillFadeSeconds = 0.35f;
        // 크기: 살 수 있는 노드를 누르면 살짝 부풀고(PuffScale), 떼서 사면 스프링으로 쫄깃하게 돌아온다. 다른 노드는 살짝 눌린다(PressScale).
        private const float PuffScale = 1.08f;
        private const float PressScale = 0.94f;
        private const float PressSeconds = 0.07f;
        // 스프링: 한 번 살짝 지나쳤다 바로 멈춘다(길게 떨지 않게 감쇠를 높게).
        private const float SpringStiffness = 1100;
        private const float SpringDamping = 24;
        private const float SpringRest = 0.002f;
        // 사며 뗄 때: 아주 잠깐 더 부풀었다가(예비 동작) 고무줄처럼 찰싹 돌아온다. 찰싹은 스프링에 안쪽 속도를 준다.
        private const float AnticipateScale = 1.14f;
        private const float AnticipateSeconds = 0.06f;
        private const float SnapKick = 2.2f;
        // 게이지가 꽉 찬 순간: 아주 잠깐 한 번 더 부풀고(ArmedBump), 그림이 초록 쪽으로 살짝 물든다(NodeVisual).
        private const float ArmedBumpScale = 1.13f;
        private const float ArmedBumpSeconds = 0.09f;

        // 크기 연출 중인 노드. 한 번에 하나다. 닿아 있으면 목표(PuffScale/PressScale)로 곧장 가고, 떼면 스프링으로 1로 돌아온다.
        private NodeVisual _node;
        private int _pointer;
        private bool _held;
        private float _scale = 1;
        private float _velocity;
        // 사며 뗀 뒤의 예비 동작이 끝나는 시각. 그 전에는 AnticipateScale로 부풀고, 끝나면 찰싹 돌아온다.
        private float _anticipateUntil;
        private bool _snapPending;
        // 게이지가 꽉 찬 순간의 작은 부풂이 끝나는 시각.
        private float _armedBumpUntil;
        // 초록 링이 사라지는 중인 노드(산 직후). 비우면 fillAmount 0.
        private NodeVisual _fading;
        private float _fadeElapsed;

        // 손가락이 노드에 닿았다. 다른 손가락이 이미 닿아 있으면 그 연출을 그대로 둔다.
        public void Touch(NodeVisual node, int pointer)
        {
            if (_held)
                return;

            if (_node != null && _node != node)
                _node.SetScale(1);

            _node = node;
            _pointer = pointer;
            _held = true;
            _velocity = 0;
            _anticipateUntil = 0;
            _snapPending = false;
        }

        // 손가락을 뗐다. 닿아 있던 그 손가락일 때만 스프링으로 돌아온다.
        public void Lift(int pointer)
        {
            if (_held && pointer == _pointer)
                _held = false;
        }

        // 보는 곳이 움직이기 시작했다: 닿아 있어도 뗀 것으로 보고 스프링으로 돌아온다.
        public void LetGo() => _held = false;

        // 꾹 눌러 산 채로 뗐다: 초록 링은 꽉 찬 채로 두고 사라지게 한다. 크기는 예비 동작 → 찰싹(눌림 연출이 이 노드를 잡고 있을 때만, 보통 그렇다).
        public void PlayBought(NodeVisual node)
        {
            _fading = node;
            _fadeElapsed = 0;

            if (_node == node)
            {
                _anticipateUntil = Time.unscaledTime + AnticipateSeconds;
                _snapPending = true;
            }
        }

        // 초록 링이 사라지는 중인가(산 직후). 그 링은 비우지 않는다.
        public bool IsFading(NodeVisual node) => _fading == node;

        // 게이지가 꽉 찼다: 잠깐 더 부풀었다가 누름 크기로 돌아온다.
        public void Bump() => _armedBumpUntil = Time.unscaledTime + ArmedBumpSeconds;

        public void CancelBump() => _armedBumpUntil = 0;

        // 크기: 닿아 있으면 목표로 곧장(PressSeconds), 떼면 스프링(살짝 지나쳤다 돌아온다). 산 직후의 초록 링은 서서히 사라진다.
        // pressed: 꾹 누름 판정 중인 노드(NodeTreeView). 없으면 null.
        public void Tick(NodeVisual pressed)
        {
            float delta = Time.unscaledDeltaTime;

            if (_node != null)
            {
                if (_held)
                {
                    // 살 수 있는 노드를 누르면 부풀고, 그 밖(정보만·Gold 부족)은 살짝 눌린다.
                    float target = _node == pressed && pressed.State == NodeState.Purchasable ? PuffScale : PressScale;

                    // 게이지가 꽉 찬 순간: 잠깐 더 부풀었다가 누름 크기로 돌아온다.
                    if (_node == pressed && Time.unscaledTime < _armedBumpUntil)
                        target = ArmedBumpScale;

                    float speed = Mathf.Max(Mathf.Abs(target - 1), ArmedBumpScale - PuffScale) / PressSeconds;
                    _scale = Mathf.MoveTowards(_scale, target, delta * speed);
                    _velocity = 0;
                }
                else if (Time.unscaledTime < _anticipateUntil)
                {
                    // 예비 동작: 아주 잠깐 더 부푼다.
                    _scale = Mathf.MoveTowards(_scale, AnticipateScale, delta * (AnticipateScale - PuffScale) / AnticipateSeconds);
                    _velocity = 0;
                }
                else
                {
                    if (_snapPending)
                    {
                        // 찰싹: 안쪽으로 속도를 줘 고무줄처럼 돌아온다.
                        _velocity = -SnapKick;
                        _snapPending = false;
                    }

                    float step = Mathf.Min(delta, 1f / 30);
                    _velocity += (1 - _scale) * SpringStiffness * step;
                    _velocity *= Mathf.Exp(-SpringDamping * step);
                    _scale += _velocity * step;
                }

                _node.SetScale(_scale);

                if (!_held && Time.unscaledTime >= _anticipateUntil && Mathf.Abs(_scale - 1) < SpringRest && Mathf.Abs(_velocity) < SpringRest)
                {
                    _node.SetScale(1);
                    _node = null;
                    _scale = 1;
                    _velocity = 0;
                    _snapPending = false;
                }
            }

            if (_fading != null)
            {
                _fadeElapsed += delta;
                float t = Mathf.Clamp01(_fadeElapsed / FillFadeSeconds);
                _fading.SetFill(1, alpha: 1 - t);

                if (t >= 1)
                    StopFade(_fading);
            }
        }

        public void StopFade(NodeVisual node)
        {
            if (_fading != node)
                return;

            _fading = null;
            node.SetFill(0);
        }

        // 페이지가 닫힌다: 크기를 1로 되돌리고 사라지던 링을 비운다.
        public void Stop()
        {
            if (_node != null)
            {
                _node.SetScale(1);
                _node = null;
            }

            _held = false;
            _scale = 1;
            _velocity = 0;
            _anticipateUntil = 0;
            _snapPending = false;

            if (_fading != null)
                StopFade(_fading);
        }

        // 트리를 새로 만든다: 노드가 곧 지워지므로 되돌리지 않고 잊는다.
        public void Forget()
        {
            _node = null;
            _held = false;
            _scale = 1;
            _velocity = 0;
            _anticipateUntil = 0;
            _snapPending = false;
            _fading = null;
        }
    }
}
