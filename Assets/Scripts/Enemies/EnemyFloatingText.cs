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
    internal abstract class EnemyFloatingText
    {
        private sealed class Label
        {
            public TextMeshPro Text;
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

        // 떠 있는 라벨이 없고, 지운 객체도 장면에서 모두 사라졌는가.
        // 지운 객체는 프레임 끝에 사라지므로, Reset 뒤 한 프레임이 지나야 true가 된다.
        public bool IsClear => _labels.Count == 0 && _root.childCount == 0;

        // 쉬는 라벨을 가져와 position 자리에 text를 color로 띄운다. 자식의 구독 콜백이 호출한다.
        protected void Show(Vector3 position, string text, Color color)
        {
            Label label = Take();
            label.Start = position;
            label.Elapsed = 0;
            label.Playing = true;
            label.Text.text = text;
            label.Text.color = color;
            label.Text.transform.position = position;
            label.Text.transform.localScale = Vector3.one * Scale;
            label.Text.gameObject.SetActive(true);
        }

        // 떠 있는 라벨을 위로 올리며 옅어지게 하고, 다 끝나면 숨긴다(다음 Take까지 재사용 대기).
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

                Color color = label.Text.color;
                color.a = 1f - progress;
                label.Text.color = color;
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
            view.SetActive(false);

            var text = view.AddComponent<TextMeshPro>();
            text.fontSize = _fontSize;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            text.GetComponent<MeshRenderer>().sortingOrder = _sortingOrder;

            return new Label { Text = text };
        }
    }
}
