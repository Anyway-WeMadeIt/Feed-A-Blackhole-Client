using BlackHole.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace BlackHole.Unity
{
    // 조준 입력: 포인터(마우스·펜·터치 중 마지막으로 쓴 것)의 화면 위치를 규칙 평면의 점으로 바꿔, 진행 중인 판의 조준점(Breaker가 치는 자리)에 넣는다(SYSTEM_CATALOG S02).
    // 누르고 있는 동안만 조준한다 — Breaker는 누른 순간 곧바로 치고 누르는 동안 범위 안의 적을 붙잡는다(BreakerSkill). 떼면 조준점이 없다.
    // - 마우스·펜: 버튼(펜 끝)을 누르고 있는 동안. PC에서도 터치와 같은 규칙이라 같은 밸런스로 시험한다.
    // - 터치: 손가락이 닿아 있는 동안. UI(버튼 등) 위를 누른 포인터는 조준하지 않는다.
    //   Device Simulator도 마우스 조작을 터치로 넘기므로 터치 경로를 탄다.
    // 포인터는 지금의 입력 출처일 뿐이다 — 판은 조준점만 안다. 규칙 평면은 장면의 z = 0이고 HQ가 장면의 원점이다.
    // 시작하거나 정리할 상태가 없다. 진행 중인 판이 없으면 아무것도 하지 않는다.
    // 테스트 도구가 조준 봇(Bot)을 꽂으면 포인터 대신 봇이 조준점을 정한다(에디터·개발 빌드) — 같은 seed면 같은 플레이가 되어 성능 전후 비교에 쓴다.
    internal sealed class AimInput
    {
        private readonly BattleSystem _battle;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private GameSession _botSession;

        // 조준 봇. null이면 포인터가 조준한다. 판이 바뀌면 그 판의 seed로 봇을 다시 시작한다.
        public IAimBot Bot { get; set; }
#endif

        public AimInput(BattleSystem battle) => _battle = battle;

        // 전투 시스템보다 먼저 부른다. 이번 프레임의 Step이 이 조준점으로 공격한다. delta: 이번 프레임에 흐른 시간(초, 봇만 쓴다).
        public void Tick(float delta)
        {
            if (!_battle.IsRunning)
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Bot != null)
            {
                if (_botSession != _battle.Session)
                {
                    _botSession = _battle.Session;
                    Bot.Begin(_battle.LastSeed);
                }

                _battle.Session.SetAimPoint(Bot.Aim(_battle.Session.World, delta));
                return;
            }

            _botSession = null;
#endif

            _battle.Session.SetAimPoint(Read());
        }

        // 포인터나 카메라가 없으면 조준점도 없다.
        private static Point2? Read()
        {
            Camera camera = Camera.main;
            Pointer pointer = Pointer.current;

            if (camera == null || pointer == null)
                return null;

            if (pointer is Touchscreen touchscreen)
            {
                if (!IsAimingTouch(touchscreen.primaryTouch))
                    return null;
            }
            else if (!IsAimingPointer(pointer))
            {
                return null;
            }

            Vector2 screen = pointer.position.ReadValue();
            Vector3 point = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -camera.transform.position.z));
            return new Point2(point.x, point.y);
        }

        // 버튼을 누르고 있고, UI 위를 누른 것이 아닌 마우스·펜.
        private static bool IsAimingPointer(Pointer pointer)
        {
            if (!pointer.press.isPressed)
                return false;

            EventSystem events = EventSystem.current;
            return events == null || !events.IsPointerOverGameObject();
        }

        // 손가락이 닿아 있고, UI 위를 누른 것이 아닌 터치.
        private static bool IsAimingTouch(TouchControl touch)
        {
            if (!touch.press.isPressed)
                return false;

            EventSystem events = EventSystem.current;
            return events == null || !events.IsPointerOverGameObject(touch.touchId.ReadValue());
        }
    }
}
