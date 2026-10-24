using UnityEngine;

namespace BlackHole.Unity
{
    // UI 소리의 출처. 캔버스 루트에 하나 붙여 두면(GameBootstrap) 그 아래의 버튼 배선(ButtonFeedback)과 화면이 부모를 거슬러 찾아 쓴다.
    // 화면마다 소리를 넣어 주지 않아도 되고, 소리 없이 실행하면(SoundPlayer 없음) UISounds가 조용히 넘어간다.
    [DisallowMultipleComponent]
    public sealed class UISoundSource : MonoBehaviour
    {
        public UISounds Sounds { get; set; }

        // component가 든 캔버스의 UI 소리. 출처가 없으면 null.
        public static UISounds For(Component component)
        {
            if (component == null)
                return null;

            UISoundSource source = component.GetComponentInParent<UISoundSource>(true);
            return source != null ? source.Sounds : null;
        }
    }
}
