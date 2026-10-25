using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    public sealed class SettingsPanel : UIPanel<SettingsPanel.Refs>
    {
        public enum Refs
        {
            RowList,
            ToggleRowTemplate,
            SliderRowTemplate,
            ChoiceRowTemplate,
            StepperRowTemplate,
            ChoiceBlocker_Button,
            ChoicePopup,
            ChoiceOptionTemplate,
            BackBtn_Button,
            // 행 목록의 스크롤 막대와 손잡이(단색). 테마의 글자색을 옅게 쓴다.
            Scrollbar,
            Handle,
            // 노치·둥근 모서리를 피하는 영역. 화면을 열 때와 해상도가 바뀔 때 UIManager가 Safe Area에 맞춘다(SafeAreaUtility).
            SafeAreaRoot,

            // Presentation이 바꾸는 그림과 글자. 코드는 건드리지 않는다.
            // 행(SettingRow)은 위젯이라 Presentation이 닿지 않는다 — 행의 모양은 프리팹의 행 틀에서 정한다.
            Sheet_Image,
            Title_Text,
            BackBtn_Image,
        }

        // 행 하나에 보일 설정.
        public readonly struct SettingItem
        {
            public string Id { get; }
            public string Label { get; }
            public GameSettings.Kind Kind { get; }
            // 켜기/끄기는 0·1, 막대는 0 ~ 1, 선택지는 번호.
            public float Value { get; }
            // 선택지(펼침 목록·단계). 다른 종류는 비어 있어도 된다.
            public IReadOnlyList<string> Options { get; }

            public SettingItem(string id, string label, GameSettings.Kind kind, float value, IReadOnlyList<string> options)
            {
                Id = id;
                Label = label;
                Kind = kind;
                Value = value;
                Options = options;
            }
        }

        private static readonly Color SelectedOptionColor = new Color(0.36f, 0.62f, 0.42f);

        private readonly List<SettingRow> _rows = new List<SettingRow>();
        private readonly List<Button> _options = new List<Button>();
        // 글자 크기·언어별 글꼴과 테마 그림·색은 Styled에 넣어 Presentation과 같은 테마를 받는다. 행과 펼침 목록은 복제로 만들므로
        // 만들 때 넣고 지울 때 뺀다(틀은 넣지 않는다). 글자 크기는 프리팹 크기 × 배율(Prefab).
        private readonly Dictionary<GameSettings.Kind, SettingRow> _templates = new Dictionary<GameSettings.Kind, SettingRow>();
        private readonly Vector3[] _corners = new Vector3[4];

        private RectTransform _rowList;
        private RectTransform _blocker;
        private RectTransform _popup;
        private Button _optionTemplate;
        private SettingRow _choiceRow;

        public event Action<string, bool> Toggled;
        public event Action<string, float> SliderChanged;
        public event Action<string, int> OptionChanged;
        public event Action BackClicked;

        protected override void OnInitialize()
        {
            _rowList = View.Rect(Refs.RowList);
            _blocker = View.Rect(Refs.ChoiceBlocker_Button);
            _popup = View.Rect(Refs.ChoicePopup);

            // 고정 그림: 펼침 목록 바탕(panel 그림), 스크롤 막대·손잡이(단색, 알파는 프리팹 값).
            Styled.AddSprite(View.Image(Refs.ChoicePopup));
            Styled.AddImageColor(View.Image(Refs.Scrollbar), UIColorRole.TextMain);
            Styled.AddImageColor(View.Image(Refs.Handle), UIColorRole.TextMain);
            _optionTemplate = View.Button(Refs.ChoiceOptionTemplate);

            AddTemplate(GameSettings.Kind.Toggle, Refs.ToggleRowTemplate);
            AddTemplate(GameSettings.Kind.Slider, Refs.SliderRowTemplate);
            AddTemplate(GameSettings.Kind.Choice, Refs.ChoiceRowTemplate);
            AddTemplate(GameSettings.Kind.Stepper, Refs.StepperRowTemplate);

            // 틀과 펼침 목록은 처음에 숨긴다.
            SetVisible(_optionTemplate, false);
            SetVisible(_blocker, false);
            SetVisible(_popup, false);

            // 선택지 뒤의 가림막은 화면 전체의 투명 버튼이라 연출·소리 없이 닫기만 한다.
            BindEvent(View.Button(Refs.ChoiceBlocker_Button), HandleBlockerClicked);
            ButtonFeedback.Bind(View.Button(Refs.BackBtn_Button), HandleBackClicked);
        }

        // 설정 행을 새로 만든다. 창을 열 때마다 불러 지금 값을 보여 준다. 목록은 맨 위부터 보인다.
        public void Build(IReadOnlyList<SettingItem> items)
        {
            CloseChoice();
            ClearRows();

            if (_rowList == null)
                return;

            foreach (SettingItem item in items)
            {
                if (!_templates.TryGetValue(item.Kind, out SettingRow template) || template == null)
                {
                    Debug.LogWarning($"[{nameof(SettingsPanel)}] '{item.Kind}' 행의 틀이 없어 '{item.Id}'를 보여 주지 못한다.", this);
                    continue;
                }

                _rows.Add(CreateRow(template, item));
            }

            _rowList.anchoredPosition = Vector2.zero;
        }

        // 행을 다시 만들지 않고 이름·선택지·값만 다시 보여 준다(언어가 바뀌었을 때). 목록의 스크롤 위치는 그대로 둔다.
        // 행은 ID로 찾는다. 맞는 항목이 없는 행은 그대로 둔다.
        public void Relabel(IReadOnlyList<SettingItem> items)
        {
            foreach (SettingRow row in _rows)
            {
                if (row == null)
                    continue;

                foreach (SettingItem item in items)
                {
                    if (item.Id != row.Id)
                        continue;

                    row.Show(item.Id, item.Label, item.Kind, item.Value, item.Options);
                    break;
                }
            }
        }

        // 패널이 닫힐 때(비활성) 펼침 목록도 닫는다.
        private void OnDisable() => CloseChoice();

        private void HandleBackClicked() => BackClicked?.Invoke();
        private void HandleBlockerClicked(PointerEventData _) => CloseChoice();

        // 행이 알린 새 값을 종류에 맞는 사건으로 바꿔 알린다.
        private void HandleRowChanged(SettingRow row)
        {
            switch (row.Kind)
            {
                case GameSettings.Kind.Toggle:
                    Toggled?.Invoke(row.Id, row.Value >= 0.5f);
                    break;
                case GameSettings.Kind.Slider:
                    SliderChanged?.Invoke(row.Id, row.Value);
                    break;
                default:
                    OptionChanged?.Invoke(row.Id, row.Index);
                    break;
            }
        }

        #region 펼침 목록

        // 행의 선택지를 목록으로 펼친다. 고른 선택지는 색으로 표시한다.
        private void OpenChoice(SettingRow row)
        {
            CloseChoice();

            RectTransform box = row.ChoiceBox;
            if (_popup == null || _optionTemplate == null || box == null)
                return;

            _choiceRow = row;

            for (int i = 0; i < row.Options.Count; i++)
                _options.Add(CreateOption(row.Options[i], i, i == row.Index));

            if (_blocker != null)
            {
                _blocker.gameObject.SetActive(true);
                _blocker.SetAsLastSibling();
            }

            _popup.gameObject.SetActive(true);
            _popup.SetAsLastSibling();
            PlaceChoice(box);
        }

        // 선택지 칸의 아래에 같은 폭으로 붙인다. 창 아래로 넘치면 칸 위로 펼친다.
        private void PlaceChoice(RectTransform box)
        {
            _popup.sizeDelta = new Vector2(box.rect.width, _popup.sizeDelta.y);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_popup);

            box.GetWorldCorners(_corners);
            Vector3 bottomRight = _corners[3];
            Vector3 topRight = _corners[2];

            _popup.pivot = new Vector2(1, 1);
            _popup.position = bottomRight;

            _popup.GetWorldCorners(_corners);
            float popupBottom = _corners[0].y;
            ((RectTransform)transform).GetWorldCorners(_corners);

            if (popupBottom < _corners[0].y)
            {
                _popup.pivot = new Vector2(1, 0);
                _popup.position = topRight;
            }
        }

        private void ChooseOption(int index)
        {
            SettingRow row = _choiceRow;
            CloseChoice();

            if (row != null)
                row.Choose(index);
        }

        private void CloseChoice()
        {
            foreach (Button option in _options)
            {
                if (option == null)
                    continue;

                Styled.RemoveUnder(option);
                Destroy(option.gameObject);
            }

            _options.Clear();
            _choiceRow = null;
            SetVisible(_blocker, false);
            SetVisible(_popup, false);
        }

        private Button CreateOption(string text, int index, bool selected)
        {
            Button option = Instantiate(_optionTemplate, _popup);
            option.name = "Option " + text;

            TMP_Text label = option.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = text;
                Styled.AddText(label, UITextRole.Body, UITextSizePolicy.Prefab);
            }

            // 선택지 바탕은 단색: 고른 것은 강조색, 나머지는 테마 표면색.
            if (option.targetGraphic is Image back)
            {
                if (selected)
                    back.color = SelectedOptionColor;
                else
                    Styled.AddImageColor(back, UIColorRole.Surface);
            }

            ButtonFeedback.Bind(option, () => ChooseOption(index));
            option.gameObject.SetActive(true);
            return option;
        }

        #endregion

        private SettingRow CreateRow(SettingRow template, SettingItem item)
        {
            SettingRow row = Instantiate(template, _rowList);
            row.name = "Row " + item.Id;
            row.EnsureInitialized();
            row.Show(item.Id, item.Label, item.Kind, item.Value, item.Options);
            row.Changed += HandleRowChanged;
            row.ChoiceClicked += OpenChoice;

            foreach ((TMP_Text text, UITextRole role) in row.StyledTexts)
                Styled.AddText(text, role, UITextSizePolicy.Prefab);

            Styled.AddSprites(row);
            row.gameObject.SetActive(true);
            return row;
        }

        private void ClearRows()
        {
            foreach (SettingRow row in _rows)
            {
                if (row == null)
                    continue;

                Styled.RemoveUnder(row);
                Destroy(row.gameObject);
            }

            _rows.Clear();
        }

        private void AddTemplate(GameSettings.Kind kind, Refs key)
        {
            SettingRow template = View.Widget<SettingRow>(key);
            _templates[kind] = template;
            SetVisible(template, false);
        }

        private static void SetVisible(Component component, bool visible)
        {
            if (component != null && component.gameObject.activeSelf != visible)
                component.gameObject.SetActive(visible);
        }
    }
}
