using BlackHole.Core;
using TMPro;
using UnityEngine;

namespace BlackHole.Unity
{
    // 적이 맞은 피해량을 그 자리 위로 띄우는 연출. 일반 피해는 흰색, 치명타는 연한 노랑색에 기울임(골든과 모양으로 구분).
    internal sealed class EnemyDamageText : EnemyFloatingText
    {
        private const int MaxLabels = 16;
        private const float RiseDistance = -0.4f;
        private const float Duration = 1.6f;
        private const int SortingOrder = 21;
        private const float FontSize = 7f;

        // 아래쪽 검정 음영이 명도 대비를 책임지므로, 다크모드와 무관하게 흰색으로 고정한다.
        private static readonly Color NormalColor = Color.white;
        private static readonly Color CriticalColor = new Color32(0xFF, 0xD9, 0x33, 0xFF);

        public EnemyDamageText(Transform parent)
            : base(parent, "Damage Text", MaxLabels, RiseDistance, Duration, SortingOrder, FontSize)
        {
        }

        // 피격 기록 하나의 피해량을 맞은 자리에 띄운다.
        public void Show(HitRecord hit)
        {
            Color color = hit.IsCritical ? CriticalColor : NormalColor;
            FontStyles style = hit.IsCritical ? (FontStyles.Bold | FontStyles.Italic) : FontStyles.Bold;
            Show(new Vector3(hit.Position.X, hit.Position.Y, 0),
                Mathf.RoundToInt(hit.Amount).ToString(), color, hit.IsCritical, style);
        }
    }
}
