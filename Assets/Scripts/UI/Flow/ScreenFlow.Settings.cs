using System.Collections.Generic;
using System.Globalization;

namespace BlackHole.Unity
{
    internal sealed partial class ScreenFlow
    {
        private void OpenSettings()
        {
            _ui.PushPanel<SettingsPanel>(
                _presentations.Settings,
                afterPresented: panel =>
                {
                    BindView(panel, ApplyBindings);
                    panel.Build(SettingItems());
                },
                afterClosed: Unbind);
        }

        private void ApplyBindings(SettingsPanel panel)
        {
            AddBinding(panel,
                p => p.Toggled += HandleSettingToggled,
                p => p.Toggled -= HandleSettingToggled);

            AddBinding(panel,
                p => p.SliderChanged += HandleSettingSliderChanged,
                p => p.SliderChanged -= HandleSettingSliderChanged);

            AddBinding(panel,
                p => p.OptionChanged += HandleSettingOptionChanged,
                p => p.OptionChanged -= HandleSettingOptionChanged);

            AddBinding(panel,
                p => p.BackClicked += HandleSettingsBackClicked,
                p => p.BackClicked -= HandleSettingsBackClicked);

            // 언어가 바뀌면(이 창에서 고른 경우 포함) 행의 이름·선택지를 그 언어로 바꾼다. 창 제목은 ViewTexts가 바꾼다.
            AddBinding(panel,
                _ => _localizer.Changed += HandleSettingsLanguageChanged,
                _ => _localizer.Changed -= HandleSettingsLanguageChanged);
        }

        // 바뀐 값은 곧바로 설정에 넣는다(저장·반영은 GameSettings가 한다).
        private void HandleSettingToggled(string id, bool on) => _settings.SetOn(id, on);
        private void HandleSettingSliderChanged(string id, float level) => _settings.SetLevel(id, level);
        private void HandleSettingOptionChanged(string id, int index) => _settings.SetIndex(id, index);

        // 행을 다시 만들지 않고 글자만 바꾼다 — 목록의 스크롤 위치가 그대로 남는다.
        private void HandleSettingsLanguageChanged() => _ui.GetUI<SettingsPanel>().Relabel(SettingItems());

        // 닫을 때 디스크에 쓴다.
        private void HandleSettingsBackClicked()
        {
            _settings.Save();
            _ui.PopPanel(Unbind);
        }

        // 설정 정의와 지금 값을 설정 창의 행으로 바꾼다. 이름·선택지는 지금 언어로.
        private List<SettingsPanel.SettingItem> SettingItems()
        {
            var items = new List<SettingsPanel.SettingItem>(GameSettings.Definitions.Count);

            foreach (GameSettings.Definition definition in GameSettings.Definitions)
            {
                items.Add(new SettingsPanel.SettingItem(
                    definition.Id, SettingLabel(definition), definition.Kind, _settings.ValueOf(definition.Id), SettingOptions(definition)));
            }

            return items;
        }

        // 표의 setting.{ID}. 없으면 영어 원문.
        private string SettingLabel(GameSettings.Definition definition) =>
            _localizer.TryGet("setting." + definition.Id, out string label) ? label : definition.Label;

        // 표의 setting.{ID}.{번호}. 없으면 원문 — 숫자·퍼센트·언어 이름은 표에 넣지 않는다.
        private IReadOnlyList<string> SettingOptions(GameSettings.Definition definition)
        {
            IReadOnlyList<string> options = definition.Options;
            if (options.Count == 0)
                return options;

            var shown = new string[options.Count];
            string prefix = "setting." + definition.Id + ".";

            for (int i = 0; i < options.Count; i++)
            {
                string key = prefix + i.ToString(CultureInfo.InvariantCulture);
                shown[i] = _localizer.TryGet(key, out string option) ? option : options[i];
            }

            return shown;
        }
    }
}
