using UnityEngine;

namespace BlackHole.Unity
{
    // 혜성(픽업 적)의 외형 설정 에셋. 화면(EnemyView)만 읽는다. 규칙 수치(크기·공전)는 적 종류 에셋(EnemyKind)과 공급 설정에 있다.
    // 혜성은 스프라이트가 아니라 셰이더로 그리므로 외형 연결(EnemyLooks)이 아니라 이 에셋이 가진다.
    [CreateAssetMenu(fileName = "CometLook", menuName = "BlackHole/Comet Look")]
    public sealed class CometLook : ScriptableObject
    {
        [Header("외형")]
        [Tooltip("BlackHole/Comet 셰이더의 머티리얼. 무지개(흐름 속도·채도)와 궤적의 끝 폭·투명도는 이 머티리얼의 속성이다. 구체 크기는 혜성의 규칙 반지름이다.")]
        [SerializeField] private Material _material;
        [Tooltip("궤적의 길이(월드 단위, 공전 경로를 따라 잰 길이).")]
        [Min(0)]
        [SerializeField] private float _tailLength = 1.5f;

        [Header("구체 속 무지개의 자전: 혜성마다 출현 때 처음 각도와 속도를 범위 안에서 무작위로 정한다(연출뿐)")]
        [Tooltip("자전 속도의 최솟값(바퀴/초). 음수면 반대로 돈다.")]
        [SerializeField] private float _spinSpeedMin = -1f;
        [Tooltip("자전 속도의 최댓값(바퀴/초). 최솟값 이상이어야 한다.")]
        [SerializeField] private float _spinSpeedMax = 1f;

        public Material Material => _material;
        public float TailLength => _tailLength;
        public float SpinSpeedMin => _spinSpeedMin;
        public float SpinSpeedMax => Mathf.Max(_spinSpeedMin, _spinSpeedMax);
    }
}
