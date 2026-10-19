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
    // 호버 지점(HoverPoint): 누르지 않은 마우스·펜이 가리키는 자리. 누르면 여기를 치게 된다는 표시(BreakerView의 대기 링)에만 쓰고 판에는 넣지 않는다.
    //   터치는 손가락이 떠 있는 위치를 알 수 없으니 호버가 없다. UI 위에 있으면 호버도 없다.
    // 포인터는 지금의 입력 출처일 뿐이다 — 판은 조준점만 안다. 규칙 평면은 장면의 z = 0이고 HQ가 장면의 원점이다.
    // 시작하거나 정리할 상태가 없다. 진행 중인 판이 없으면 아무것도 하지 않는다.
    internal sealed class AimInput
    {
        private readonly BattleSystem _battle;

        public AimInput(BattleSystem battle) => _battle = battle;

        // 마지막 Tick에서 읽은 호버 지점. 마우스·펜이 UI 밖을 가리키고 있을 때만 값이 있다(누르고 있어도 있다).
        public Point2? HoverPoint { get; private set; }

        // 전투 시스템보다 먼저 부른다. 이번 프레임의 Step이 이 조준점으로 공격한다.
        public void Tick()
        {
            // 판이 진행 중이 아니면(인트로·끝난 판·결산 대기) 호버도 없다 — 마지막 자리에 대기 링이 남지 않게 비운다.
            if (!_battle.IsRunning)
            {
                HoverPoint = null;
                _battle.HoverPoint = null;
                return;
            }

            _battle.Session.SetAimPoint(Read());
            HoverPoint = ReadHover();
            _battle.HoverPoint = HoverPoint;
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

        // 마지막으로 쓴 포인터가 마우스·펜이고 UI 위가 아니면 그 위치. 터치스크린이면 없다.
        private static Point2? ReadHover()
        {
            Camera camera = Camera.main;
            Pointer pointer = Pointer.current;

            if (camera == null || pointer is null || pointer is Touchscreen)
                return null;

            EventSystem events = EventSystem.current;
            if (events != null && events.IsPointerOverGameObject())
                return null;

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
