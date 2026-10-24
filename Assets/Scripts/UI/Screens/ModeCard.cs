using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 모드 선택 창의 카드 하나(위젯). UIManager에 등록하지 않는다 — 모드 선택 창이 틀을 복제해 모드마다 하나씩 만든다.
    // 머리(모드 색)·이름·설명·기록을 보여 주고, 눌렸다는 사실만 알린다. 크기와 자리는 모드 선택 창이 정한다.
    // 카드 자신(루트)에 Button이 있어야 눌림을 받는다. 기준점은 아래 가운데라 작아진 카드도 바닥이 맞는다.
    public sealed class ModeCard : UIBase<ModeCard.Refs>
    {
        public enum Refs
        {
            Header,
            TitleText,
            DescriptionText,
            RecordText,
        }

        // 글자 크기·언어별 글꼴·테마 색을 받는 글자와 역할. 모드 선택 창이 카드를 만들 때 StyledTexts에 넣는다.
        // 카드의 그림(바탕 panel)은 모드 선택 창이 ThemedImages.AddSprites(card)로 테마 짝으로 바꾼다. 머리(Header)는 그림이 없어 모드 색이 그대로다.
        public IEnumerable<(TMP_Text Text, UITextRole Role)> StyledTexts
        {
            get
            {
                yield return (View.Text(Refs.TitleText), UITextRole.Title);
                yield return (View.Text(Refs.DescriptionText), UITextRole.Body);
                yield return (View.Text(Refs.RecordText), UITextRole.Caption);
            }
        }

        public event Action Clicked;

        public RectTransform Rect => (RectTransform)transform;

        protected override void OnInitialize()
        {
            BindEvent(GetComponent<Button>(), HandleClicked);
        }

        private void HandleClicked(PointerEventData _) => Clicked?.Invoke();

        public void Show(ModeSelectPanel.ModeItem mode)
        {
            Image header = View.Image(Refs.Header);
            if (header != null)
                header.color = mode.Color;

            TMP_Text title = View.Text(Refs.TitleText);
            if (title != null)
                title.text = mode.Title;

            TMP_Text description = View.Text(Refs.DescriptionText);
            if (description != null)
                description.text = mode.Description;

            TMP_Text record = View.Text(Refs.RecordText);
            if (record != null)
            {
                bool hasRecord = !string.IsNullOrEmpty(mode.Record);
                record.text = hasRecord ? mode.Record : string.Empty;
                record.gameObject.SetActive(hasRecord);
            }
        }
    }
}
