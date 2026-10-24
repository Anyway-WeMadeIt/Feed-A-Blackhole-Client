using BlackHole.CelestialBattle;
using UnityEngine;

namespace BlackHole.Unity
{
    // 화면(타이틀·업그레이드·결산·모드 선택) 배경 위에 얹는 은은한 별빛·궤도 문양. 전투 배경 키트의 Canvas 셰이더를 같은 드라이버로 돌린다.
    // 정적 배경 그림은 ImageBindings가 바꾸고(Background_Image), 이 문양의 머티리얼만 테마에 따라 White/Black으로 바꾼다.
    // 화면이 OnPresented에서 Apply(themeId)를 부른다. 드라이버가 없거나 머티리얼이 비면 프리팹에 놓인 그대로 둔다.
    [DisallowMultipleComponent]
    public sealed class UIScreenAmbient : MonoBehaviour
    {
        [SerializeField] private BattleBackgroundDriver _driver;
        [SerializeField] private Material _light;
        [SerializeField] private Material _dark;

        private string _shownThemeId;

        public void Apply(string themeId)
        {
            if (_driver == null || themeId == _shownThemeId)
                return;

            Material material = themeId == UITheme.Dark.ToId() ? _dark : _light;

            if (material == null)
                return;

            _driver.SetOverlayMaterial(material);
            _shownThemeId = themeId;
        }
    }
}
