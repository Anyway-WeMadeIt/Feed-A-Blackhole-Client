namespace BlackHole.Core
{
    // UpgradeStats 시트 한 행. 이름 칸(ValueType·Unit·Aggregation)은 글자 그대로 두고 로더가 해석한다.
    public sealed class UpgradeStatRowData
    {
        // 시트 행 번호(머리칸이 1행). 진단 위치에 쓴다. 시트에서 오지 않았으면 0이다.
        public int Row;
        public string StatId;
        // Float, Int
        public string ValueType;
        // Flat, Percent
        public string Unit;
        public float DefaultValue;
        // Add만 쓸 수 있다.
        public string Aggregation;
        // 비어 있으면 null(제한 없음).
        public float? Min;
        public float? Max;
        public bool Enabled;
    }
}
