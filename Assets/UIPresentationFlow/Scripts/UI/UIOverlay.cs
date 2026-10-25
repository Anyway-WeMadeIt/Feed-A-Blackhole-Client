using System;

// Overlay: Root·Panel 위, 화면 전환 덮개 아래에 얹히는 뷰. 토스트·알림처럼 입력을 가로채지 않고 제 수명으로 사라지는 것과,
// 엔딩처럼 모든 화면을 덮는 겹 화면이 여기 속한다. Panel 스택에 들어가지 않는다 — TopPanel이 되지 않고, 아래 Panel을 흐리거나 막지 않으며,
// 여럿이 동시에 보일 수 있다(나중에 보인 것이 위). UIManager.ShowOverlay·HideOverlay로 켜고 끈다.
public interface IUIOverlay { }

public abstract class UIOverlay<TRefs> : UIBase<TRefs>, IUIOverlay
    where TRefs : struct, Enum
{
}
