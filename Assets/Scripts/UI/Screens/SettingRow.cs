using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 설정 창의 행 하나(위젯). UIManager에 등록하지 않음.
    // 사용자가 값을 바꾸면 Changed로 알린다. 설정의 규칙을 모른다 — 값은 설정 창이 넘긴 대로 보여 준다.
    public sealed class SettingRow : UIBase<SettingRow.Refs>
    {
        public enum Refs
        {
            LabelText,
            Toggle_Button,
            CheckImage,
            SliderTrack,
            SliderFill,
            Choice_Button,
            PrevBtn_Button,
            NextBtn_Button,
            ValueText,
        }

        private IReadOnlyList<string> _options = Array.Empty<string>();
        private RectTransform _track;
        private RectTransform _fill;
        private GameObject _check;
        private TMP_Text _valueText;
        private Button _prev;
        private Button _next;

        public string Id { get; private set; }

        public GameSettings.Kind Kind { get; private set; }

        // 켜기/끄기는 0·1, 막대는 0 ~ 1, 선택지는 번호.
        public float Value { get; private set; }

        public IReadOnlyList<string> Options => _options;

        public int Index => Mathf.RoundToInt(Value);

        // 펼침 목록이 붙을 자리.
        public RectTransform ChoiceBox => View.Rect(Refs.Choice_Button);

        // 글자 크기·언어별 글꼴을 받는 글자(이름, 선택지·단계 값)와 역할. 설정 창이 행을 만들 때 StyledTexts에 넣는다.
        public IEnumerable<(TMP_Text Text, UITextRole Role)> StyledTexts
        {
            get
            {
                yield return (View.Text(Refs.LabelText), UITextRole.Body);
                yield return (View.Text(Refs.ValueText), UITextRole.Body);
            }
        }

        public event Action<SettingRow> Changed;
        public event Action<SettingRow> ChoiceClicked;

        protected override void OnInitialize()
        {
            _track = View.Rect(Refs.SliderTrack);
            _fill = View.Rect(Refs.SliderFill);
            RectTransform check = View.Rect(Refs.CheckImage);
            _check = check != null ? check.gameObject : null;
            _valueText = View.Text(Refs.ValueText);
            _prev = View.Button(Refs.PrevBtn_Button);
            _next = View.Button(Refs.NextBtn_Button);

            BindEvent(View.Button(Refs.Toggle_Button), HandleToggleClicked);
            BindEvent(View.Button(Refs.Choice_Button), HandleChoiceClicked);
            BindEvent(_prev, HandlePrevClicked);
            BindEvent(_next, HandleNextClicked);

            // 막대는 버튼이 아니라 누름·끌기를 받는다. 끌기를 여기서 받으므로 막대 위에서는 목록이 스크롤되지 않는다.
            UI_EventHandler track = View.Component<UI_EventHandler>(Refs.SliderTrack);

            if (track != null)
            {
                track.OnPointerDownHandler += HandleSliderPointer;
                track.OnDragHandler += HandleSliderPointer;
            }
        }

        public void Show(string id, string label, GameSettings.Kind kind, float value, IReadOnlyList<string> options)
        {
            Id = id;
            Kind = kind;
            _options = options ?? Array.Empty<string>();
            WarnMissingParts();

            TMP_Text labelText = View.Text(Refs.LabelText);
            if (labelText != null)
                labelText.text = label;

            Value = value;
            Refresh();
        }

        // 펼침 목록에서 고른 번호. 설정 창이 부른다.
        public void Choose(int index) => Change(index);

        private void HandleToggleClicked(PointerEventData _) => Change(Value >= 0.5f ? 0 : 1);
        private void HandleChoiceClicked(PointerEventData _) => ChoiceClicked?.Invoke(this);
        // 끝에서는 버튼이 막혀 있지만 클릭은 온다(UI_EventHandler). Change가 범위 밖 번호를 거른다.
        private void HandlePrevClicked(PointerEventData _) => Change(Index - 1);
        private void HandleNextClicked(PointerEventData _) => Change(Index + 1);

        private void HandleSliderPointer(PointerEventData eventData)
        {
            if (_track == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _track, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            Rect rect = _track.rect;
            Change(Mathf.InverseLerp(rect.xMin, rect.xMax, local.x));
        }

        private void Change(float value)
        {
            if (Kind == GameSettings.Kind.Choice || Kind == GameSettings.Kind.Stepper)
            {
                if (value < 0 || value > _options.Count - 1)
                    return;
            }

            if (Mathf.Approximately(value, Value))
                return;

            Value = value;
            Refresh();
            Changed?.Invoke(this);
        }

        private void Refresh()
        {
            switch (Kind)
            {
                case GameSettings.Kind.Toggle:
                    if (_check != null)
                        _check.SetActive(Value >= 0.5f);
                    break;

                case GameSettings.Kind.Slider:
                    if (_fill != null)
                        _fill.anchorMax = new Vector2(Mathf.Clamp01(Value), _fill.anchorMax.y);
                    break;

                default:
                    int index = Index;
                    if (_valueText != null)
                        _valueText.text = index >= 0 && index < _options.Count ? _options[index] : string.Empty;
                    if (_prev != null)
                        _prev.interactable = index > 0;
                    if (_next != null)
                        _next.interactable = index < _options.Count - 1;
                    break;
            }
        }

        private void WarnMissingParts()
        {
            switch (Kind)
            {
                case GameSettings.Kind.Toggle:
                    UIRefCheck.WarnMissing(this, Refs.LabelText, Refs.Toggle_Button, Refs.CheckImage);
                    break;
                case GameSettings.Kind.Slider:
                    UIRefCheck.WarnMissing(this, Refs.LabelText, Refs.SliderTrack, Refs.SliderFill);
                    break;
                case GameSettings.Kind.Choice:
                    UIRefCheck.WarnMissing(this, Refs.LabelText, Refs.Choice_Button, Refs.ValueText);
                    break;
                case GameSettings.Kind.Stepper:
                    UIRefCheck.WarnMissing(this, Refs.LabelText, Refs.PrevBtn_Button, Refs.NextBtn_Button, Refs.ValueText);
                    break;
            }
        }
    }
}
