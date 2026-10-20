using System;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        // 확인을 누르면 할 일. 창을 열 때 정하고, 창이 닫히면 비운다.
        private Action _confirmed;

        // 지금 창들 위에 확인 창을 쌓는다. 확인하면 창을 닫고 confirmed를, 취소하면 닫기만 한다.
        private void OpenConfirm(string title, string body, string confirmLabel, string cancelLabel, Action confirmed)
        {
            _confirmed = confirmed;

            _ui.PushPanel<ConfirmPanel>(
                _presentations.Confirm,
                afterPresented: panel =>
                {
                    BindView(panel, ApplyBindings);
                    panel.Present(title, body, confirmLabel, cancelLabel);
                },
                afterClosed: Unbind);
        }

        private void ApplyBindings(ConfirmPanel panel)
        {
            AddBinding(panel,
                p => p.ConfirmClicked += HandleConfirmAcceptClicked,
                p => p.ConfirmClicked -= HandleConfirmAcceptClicked);

            AddBinding(panel,
                p => p.CancelClicked += HandleConfirmCancelClicked,
                p => p.CancelClicked -= HandleConfirmCancelClicked);
        }

        // 확인: 창을 닫은 뒤 정해 둔 일을 한다.
        private void HandleConfirmAcceptClicked()
        {
            Action confirmed = _confirmed;
            _confirmed = null;
            _ui.PopPanel(Unbind);
            confirmed();
        }

        private void HandleConfirmCancelClicked()
        {
            _confirmed = null;
            _ui.PopPanel(Unbind);
        }
    }
}
