using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlackHole.Unity
{
    // 키보드 단축키(Input System의 UI 액션)를 화면 흐름의 이벤트로 넘긴다. GameBootstrap이 ScreenFlow에 연결한다.
    // - Shift: 업그레이드, Space: 계속(엔딩에서는 누르고 있으면 빨리 감기), Esc: 일시정지 창(전투·결산·업그레이드)
    public class KeyInput : MonoBehaviour
    {
        private InputSystem_Actions _actions;

        public event Action ContinuePressed;
        public event Action UpgradePressed;
        public event Action PausePressed;

        // 계속 키(Space)를 지금 누르고 있는가. 엔딩의 빨리 감기에 쓴다.
        public bool ContinueHeld => _actions != null && _actions.UI.Continue.IsPressed();

        private void Awake()
        {
            _actions = new InputSystem_Actions();
        }

        private void OnEnable()
        {
            _actions.UI.Enable();
            _actions.UI.Upgrade.performed += OnUpgrade;
            _actions.UI.Continue.performed += OnContinue;
            _actions.UI.Pause.performed += OnPause;
        }

        private void OnDisable()
        {
            _actions.UI.Upgrade.performed -= OnUpgrade;
            _actions.UI.Continue.performed -= OnContinue;
            _actions.UI.Pause.performed -= OnPause;
            _actions.UI.Disable();
        }

        private void OnUpgrade(InputAction.CallbackContext context) => UpgradePressed?.Invoke();

        private void OnContinue(InputAction.CallbackContext context) => ContinuePressed?.Invoke();

        private void OnPause(InputAction.CallbackContext context) => PausePressed?.Invoke();
    }
}
