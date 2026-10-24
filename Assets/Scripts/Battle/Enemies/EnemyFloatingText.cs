using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 적 위로 짧게 떠올랐다 사라지는 TMP 텍스트 연출의 공통 틀(골드 획득, 피해량 등).
    // 라벨 생성·재사용 풀링과 위로 올리며 옅어지는 진행은 이 클래스가 맡고,
    // 무엇을 구독해 언제 무슨 글자·색으로 띄울지는 자식 클래스가 정한다.
    // EnemyView.Synchronize가 매 프레임 Age를 불러 구동한다(개별 Update 없음, ExplosionRings와 같은 방식).
    // 글자 아래 검정 음영은 텍스트를 하나 더 그리는 대신, TMP 머티리얼의 Underlay 셰이더 기능을 쓴다.
    // 공유 머티리얼이 아니라 라벨별 "인스턴스" 머티리얼(text.fontMaterial)에만 키워드를 켜므로
    // 같은 폰트를 쓰는 다른 텍스트에는 영향이 없고, 폰트가 바뀌어도 TMP SDF 셰이더를 쓰는 한 그대로 동작한다.
    internal abstract class EnemyFloatingText
    {
        // 음영 색(고정 검정)과 셰이더 Underlay 파라미터. 폰트 크기에 맞춰 에디터에서 눈으로 보고 조절할 값이다.
        private static readonly Color UnderlayColor = Color.black;
        private const float UnderlayOffsetX = 0f;
        private const float UnderlayOffsetY = -1f;
        private const float UnderlayDilate = 0f;
        private const float UnderlaySoftness = 0.3f;

        // 골든 처치, 크리티컬 피해처럼 강조하고 싶은 경우 폰트 크기에 더해줄 값.
        private const float EmphasisFontSizeBonus = 2f;

        private sealed class Label
        {
            public TextMeshPro Text;
            public Material Material;
            public Vector3 Start;
            public float Elapsed;
            public bool Playing;
        }

        private readonly int _maxLabels;
        private readonly float _riseDistance;
        private readonly float _duration;
        private readonly int _sortingOrder;
        private readonly float _fontSize;

        private readonly Transform _root;
        private readonly List<Label> _labels = new List<Label>();

        protected EnemyFloatingText(Transform parent, string rootName, int maxLabels, float riseDistance,
            float duration, int sortingOrder, float fontSize)
        {
            _root = new GameObject(rootName).transform;
            _root.SetParent(parent, false);
            _maxLabels = maxLabels;
            _riseDistance = riseDistance;
            _duration = duration;
            _sortingOrder = sortingOrder;
            _fontSize = fontSize;
        }

        // 화면 크기를 고정하려고 곱하는 배율(판의 전장 배율, EnemyView가 정한다). 글자 크기와 떠오르는 거리에 곱한다.
        // 원작의 피해·골드 숫자는 카메라가 넓어져도 화면에서 같은 크기다.
        public float Scale { get; set; } = 1;

        // 쉬는 라벨을 가져와 position 자리에 text를 color로 띄운다. 자식의 구독 콜백이 호출한다.
        // emphasized면 폰트 크기를 EmphasisFontSizeBonus만큼 키운다(크기는 베이스가 들고 있는 폰트
        // 크기 상수에 관한 일이라 여기서 책임진다). 어떤 모양(볼드/기울임 등)으로 그릴지는 TMP의
        // FontStyles를 자식이 직접 넘기게 해, 조합이 늘어나도 매개변수를 더 늘릴 필요가 없게 한다.
        // 라벨은 재사용되므로 매번 다시 정해 줘야 이전 호출의 크기·스타일이 남지 않는다.
        protected void Show(Vector3 position, string text, Color color, bool emphasized = false,
            FontStyles style = FontStyles.Bold)
        {
            Label label = Take();
            label.Start = position;
            label.Elapsed = 0;
            label.Playing = true;
            label.Text.fontSize = emphasized ? _fontSize + EmphasisFontSizeBonus : _fontSize;
            label.Text.fontStyle = style;
            label.Text.text = text;
            label.Text.color = color;
            label.Text.transform.position = position;
            label.Text.transform.localScale = Vector3.one * Scale;
            label.Text.gameObject.SetActive(true);
        }

        // 떠 있는 라벨을 위로 올리며 옅어지게 하고, 다 끝나면 숨긴다(다음 Take까지 재사용 대기).
        // 음영은 Underlay 셰이더가 글자와 한 메시에 같이 그려주므로, 옅어지는 정도만 머티리얼에 맞춰 준다.
        public void Age(float delta)
        {
            for (int i = 0; i < _labels.Count; i++)
            {
                Label label = _labels[i];

                if (!label.Playing)
                    continue;

                label.Elapsed += delta;

                if (label.Elapsed >= _duration)
                {
                    label.Playing = false;
                    label.Text.gameObject.SetActive(false);
                    continue;
                }

                float progress = label.Elapsed / _duration;
                label.Text.transform.position = label.Start + Vector3.up * (_riseDistance * Scale * progress);

                float alpha = 1f - progress;

                Color color = label.Text.color;
                color.a = alpha;
                label.Text.color = color;

                Color underlay = UnderlayColor;
                underlay.a = alpha;
                label.Material.SetColor(ShaderUtilities.ID_UnderlayColor, underlay);
            }
        }

        // 판이 바뀌거나 판을 정리할 때 모든 라벨을 지운다.
        public void Reset()
        {
            foreach (Label label in _labels)
                Object.Destroy(label.Text.gameObject);

            _labels.Clear();
        }

        // 쉬는 라벨을 먼저 쓴다. 없으면 상한까지 새로 만들고, 상한이면 가장 오래 재생된 라벨을 다시 쓴다.
        private Label Take()
        {
            for (int i = 0; i < _labels.Count; i++)
            {
                if (!_labels[i].Playing)
                    return _labels[i];
            }

            if (_labels.Count < _maxLabels)
            {
                Label created = Create();
                _labels.Add(created);
                return created;
            }

            Label oldest = _labels[0];

            for (int i = 1; i < _labels.Count; i++)
            {
                if (_labels[i].Elapsed > oldest.Elapsed)
                    oldest = _labels[i];
            }

            return oldest;
        }

        private Label Create()
        {
            var view = new GameObject("Label");
            view.transform.SetParent(_root, false);

            // 활성 상태에서 컴포넌트를 붙여 TMP의 Awake/OnEnable(기본 폰트·머티리얼 지연 초기화)이
            // 먼저 끝나게 한다. 비활성 상태에서 머티리얼을 건드리면 그 초기화가 나중에 덮어써 버린다
            // (예전에 외곽선 머티리얼이 사라졌던 원인).
            TextMeshPro text = CreateText(view);

            // fontSharedMaterial(공유 에셋)을 바꿔치기하지 않고, 이 라벨 전용 인스턴스만 가져와 키워드를 켠다.
            Material material = text.fontMaterial;
            material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
            material.SetColor(ShaderUtilities.ID_UnderlayColor, UnderlayColor);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, UnderlayOffsetX);
            material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, UnderlayOffsetY);
            material.SetFloat(ShaderUtilities.ID_UnderlayDilate, UnderlayDilate);
            material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, UnderlaySoftness);

            view.SetActive(false);

            return new Label { Text = text, Material = material };
        }

        // 폰트 에셋은 지금은 TMP 기본값을 그대로 쓰고 있다 — 나중에 전용 폰트를 넣게 되면
        // 이 자리에서 text.font = ... 한 줄만 추가하면 된다(Underlay는 SDF 셰이더 기능이라 폰트와 무관하게 유지된다).
        private TextMeshPro CreateText(GameObject host)
        {
            host.SetActive(true);
            var text = host.AddComponent<TextMeshPro>();
            text.fontSize = _fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.extraPadding = true; // Underlay가 글자 바깥으로 번지는 만큼 메시 여백을 넉넉히 둔다.
            text.GetComponent<MeshRenderer>().sortingOrder = _sortingOrder;
            return text;
        }
    }
}
