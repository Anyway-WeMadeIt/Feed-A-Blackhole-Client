using UnityEngine;

namespace BlackHole.Unity
{
    // 보스 체력을 여러 줄로 나누는 계산(은하 체력바). 줄 수는 색 등급이 정하고, 막대 하나가 지금 줄이다.
    // 줄 색은 기준 색에서 색상·밝기를 조금씩 바꿔 돈다 — 마지막 줄(1)이 기준 색 그대로. 순수 C#.
    internal sealed class HealthLines
    {
        // 색(색 등급)별 줄 수 [임시]: 빨강 → 보라.
        private static readonly int[] LinesByTier = { 40, 60, 80, 120, 160, 240 };

        // 줄 색: 기준 색에서 색상·밝기를 조금씩 바꿔 돈다. 마지막 줄(x1)이 기준 색 그대로다.
        private static readonly float[] LineHueShift = { 0f, 0.06f, -0.05f, 0.11f };
        private static readonly float[] LineValue = { 1f, 0.82f, 0.9f, 0.72f };

        private Color _color = Color.white;
        private int _lines;
        private float _lineHealth;

        // 줄 수.
        public int Count => _lines;

        // 새 보스: 색 등급으로 줄 수를, 최대 체력으로 줄 하나의 체력을 정한다. 색의 알파는 1로.
        public void Open(int tier, float maxHealth, Color color)
        {
            _color = color;
            _color.a = 1;
            _lines = LinesByTier[Mathf.Clamp(tier, 0, LinesByTier.Length - 1)];
            _lineHealth = maxHealth / _lines;
        }

        // 이 체력이 있는 줄(1 = 마지막 줄). 죽었으면 0.
        public int LineOf(float health)
        {
            if (health <= 0 || _lineHealth <= 0)
                return 0;

            return Mathf.Clamp(Mathf.CeilToInt(health / _lineHealth - 1e-4f), 1, _lines);
        }

        // 지금 줄 안의 남은 몫(0 ~ 1).
        public float FractionOf(float health, int line)
        {
            if (line <= 0)
                return 0;

            return Mathf.Clamp01((health - (line - 1) * _lineHealth) / _lineHealth);
        }

        // 줄 n의 색(n = 1이 기준 색 그대로). 0 이하는 기준 색.
        public Color ColorOf(int line)
        {
            if (line <= 0)
                return _color;

            int k = (line - 1) % LineHueShift.Length;
            Color.RGBToHSV(_color, out float h, out float s, out float v);
            Color color = Color.HSVToRGB(Mathf.Repeat(h + LineHueShift[k], 1), s, Mathf.Clamp01(v * LineValue[k]));
            color.a = 1;
            return color;
        }
    }
}
