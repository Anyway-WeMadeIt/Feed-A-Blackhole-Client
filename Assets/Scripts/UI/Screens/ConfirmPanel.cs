using System;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    // 확인 창. 부르는 쪽이 글자(Present)와 확인·취소 때 할 일을 정한다.
    // 뒤를 가리개(Blocker)가 덮어, 창이 열린 동안 아래 화면은 눌리지 않는다.
    public sealed class ConfirmPanel : UIPanel<ConfirmPanel.Refs>
    {
        public enum Refs
        {
            ConfirmBtn_Button,
            CancelBtn_Button,
            // 노치·둥근 모서리를 피하는 영역. 화면을 열 때와 해상도가 바뀔 때 UIManager가 Safe Area에 맞춘다(SafeAreaUtility).
            SafeAreaRoot,

            // 부르는 쪽이 정하는 글자. 크기·글꼴·색은 Presentation(TextBinding)이 맞춘다.
            Title_Text,
            Body_Text,
            ConfirmBtn_Text,
            CancelBtn_Text,

            // Presentation이 바꾸는 그림. 코드는 건드리지 않는다.
            // 버튼의 올림·누름·막힘 그림은 프리팹의 Button(Sprite Swap)에 있다.
            Panel_Image,
            ConfirmBtn_Image,
            CancelBtn_Image,
        }

        private Button _confirmButton;
        private Button _cancelButton;
        private ButtonAnimation _confirmAnimation;
        private ButtonAnimation _cancelAnimation;
        private TMP_Text _title;
        private TMP_Text _body;
        private TMP_Text _confirmLabel;
        private TMP_Text _cancelLabel;

        public event Action ConfirmClicked;
        public event Action CancelClicked;

        protected override void OnInitialize()
        {
            ScreenRefs.WarnMissing<Refs>(this);

            _confirmButton = View.Button(Refs.ConfirmBtn_Button);
            _cancelButton = View.Button(Refs.CancelBtn_Button);
            _title = View.Text(Refs.Title_Text);
            _body = View.Text(Refs.Body_Text);
            _confirmLabel = View.Text(Refs.ConfirmBtn_Text);
            _cancelLabel = View.Text(Refs.CancelBtn_Text);

            _confirmAnimation = ButtonAnimation.Of(_confirmButton);
            _cancelAnimation = ButtonAnimation.Of(_cancelButton);

            BindEvent(_confirmButton, ClickConfirmButton);
            BindEvent(_confirmButton, HoverConfirmButton, ETouchEvent.PointerEnter);
            BindEvent(_confirmButton, LeaveConfirmButton, ETouchEvent.PointerExit);
            BindEvent(_confirmButton, PressConfirmButton, ETouchEvent.PointerDown);
            BindEvent(_confirmButton, ReleaseConfirmButton, ETouchEvent.PointerUp);

            BindEvent(_cancelButton, ClickCancelButton);
            BindEvent(_cancelButton, HoverCancelButton, ETouchEvent.PointerEnter);
            BindEvent(_cancelButton, LeaveCancelButton, ETouchEvent.PointerExit);
            BindEvent(_cancelButton, PressCancelButton, ETouchEvent.PointerDown);
            BindEvent(_cancelButton, ReleaseCancelButton, ETouchEvent.PointerUp);
        }

        // 창의 글자. 열 때마다(afterPresented) 부른다.
        public void Present(string title, string body, string confirmLabel, string cancelLabel)
        {
            _title.text = title;
            _body.text = body;
            _confirmLabel.text = confirmLabel;
            _cancelLabel.text = cancelLabel;
        }

        private void ClickConfirmButton(PointerEventData _) => ConfirmClicked?.Invoke();
        private void HoverConfirmButton(PointerEventData _) => _confirmAnimation.Hover();
        private void LeaveConfirmButton(PointerEventData _) => _confirmAnimation.Leave();
        private void PressConfirmButton(PointerEventData _) => _confirmAnimation.Press();
        private void ReleaseConfirmButton(PointerEventData _) => _confirmAnimation.Release();

        private void ClickCancelButton(PointerEventData _) => CancelClicked?.Invoke();
        private void HoverCancelButton(PointerEventData _) => _cancelAnimation.Hover();
        private void LeaveCancelButton(PointerEventData _) => _cancelAnimation.Leave();
        private void PressCancelButton(PointerEventData _) => _cancelAnimation.Press();
        private void ReleaseCancelButton(PointerEventData _) => _cancelAnimation.Release();
    }
}
