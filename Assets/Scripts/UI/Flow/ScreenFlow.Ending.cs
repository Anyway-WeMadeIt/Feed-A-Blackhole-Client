using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlackHole.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BlackHole.Unity
{
    // 엔딩: 마지막 이정표(보라 은하를 부순 판)에 닿으면 결산 화면 대신 이 흐름을 탄다.
    // 1. 붕괴(전투 화면): 판은 이미 끝났다. 남은 적이 블랙홀로 빨려 들어가고 블랙홀이 커진다(BattleSystem.TryBeginCollapse). 버튼·키는 받지 않는다.
    // 2. 엔딩 화면(EndingScreen)이 검게 덮는다. 다 덮이면 판을 정리·결산하고 결산 로그와 함께 저장한다 — 일반 판 끝과 같은 길이라 서버 진행도가 맞는다.
    //    결산 화면은 보이지 않는다(보상은 다음에 업그레이드 화면에서 쓴다).
    // 3. 엔딩 문구 → 크레딧 → 감사 문구. 문구·크레딧은 누르고 있으면(클릭·터치, Space) 빨리 흐른다. 감사 문구에서 누르면 화면 전환과 함께 타이틀로.
    // 엔딩 뒤 이어하기는 마지막 이정표 뒤의 판(성장도 = MaxStage, 은하는 보라로 고정, 더 이상 이정표 없음)이다.
    internal sealed partial class ScreenFlow
    {
        // 엔딩 글자(UIText.Ending). 줄·크레딧 묶음은 1부터 번호가 끊길 때까지 읽는다.
        // 크레딧 이름 칸 안에서 사람을 나누는 글자(한 줄에 한 사람).
        private const char EndingNameSeparator = '|';
        private const int EndingMaxRows = 64;

        private bool _endingPlaying;
        private EndingScreen _endingScreen;
        private Task _endingSettlement;

        // 엔딩 흐름 중인가(붕괴부터 타이틀로 갈 때까지). 이 동안 판 종료 버튼·시간 만료·키는 엔딩만 다룬다.
        public bool IsEndingPlaying => _endingPlaying;

        // GameHost: 마지막 이정표에 닿아 판이 끝났다. 붕괴 연출을 시작했으면 true(한 번만).
        internal bool TryBeginEnding()
        {
            if (_endingPlaying || !_battle.TryBeginCollapse())
                return false;

            _endingPlaying = true;
            return true;
        }

        // GameHost가 매 프레임 부른다. 붕괴가 끝나면 엔딩 화면을 열고, 연 뒤에는 빨리 감기 키(Space)를 누르고 있는지 넘긴다.
        internal void TickEnding()
        {
            if (!_endingPlaying)
                return;

            if (_endingScreen != null)
                _endingScreen.FastForwardKeyHeld = _keyInput.ContinueHeld;
            else if (_endingSettlement == null && _battle.HasCollapsed)
                OpenEndingScreen();
        }

        private void OpenEndingScreen()
        {
            UIAppliedTheme theme = _ui.Theme;

            _endingScreen = EndingScreen.Create(
                CodeScreenParent(),
                CodeScreenSiblingIndex(),
                EndingTextsOf(),
                theme.FontFor(UITextRole.Title),
                theme.FontFor(UITextRole.Body),
                theme.TextScale);

            _endingScreen.Covered += HandleEndingCovered;
            _endingScreen.Finished += HandleEndingFinished;
        }

        // 다 덮였다: 아래에서 판을 정리·결산한다.
        private void HandleEndingCovered() => _endingSettlement = SettleFinalBattleAsync();

        private async Task SettleFinalBattleAsync()
        {
            try
            {
                BattleRawData raw = await _battle.TryEndAsync();

                if (raw != null)
                    _recorder.Settle(raw);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
            }
        }

        private void HandleEndingFinished()
        {
            _sounds.PlaySwitchingScreens();
            _transition.Play(FinishEndingAsync);
        }

        // 덮인 뒤: 결산이 끝났는지 확인하고 엔딩 화면을 지운 뒤 타이틀을 보인다.
        private async Task FinishEndingAsync()
        {
            if (_endingSettlement != null)
                await _endingSettlement;

            CloseEnding();
            ShowTitle();
        }

        // 엔딩 화면 건너뛰기(Esc). 엔딩 화면이 없으면(붕괴 중) 아무것도 하지 않는다.
        private void SkipEnding()
        {
            if (_endingScreen != null)
                _endingScreen.Skip();
        }

        // 계속 키(Space)를 눌렀다: 감사 문구에서 끝낸다. 문구·크레딧 중에는 누르고 있는 동안 빨리 감는다(TickEnding).
        private void ContinueEnding()
        {
            if (_endingScreen != null)
                _endingScreen.Continue();
        }

        private void CloseEnding()
        {
            if (_endingScreen != null)
            {
                _endingScreen.Covered -= HandleEndingCovered;
                _endingScreen.Finished -= HandleEndingFinished;
                Object.Destroy(_endingScreen.gameObject);
            }

            _endingScreen = null;
            _endingSettlement = null;
            _endingPlaying = false;
        }

        // 번역표의 엔딩 글자. 빈 칸은 건너뛴다.
        private EndingTexts EndingTextsOf()
        {
            var lines = new List<string>();

            for (int i = 1; i <= EndingMaxRows && _localizer.TryGet(UIText.Ending.Line(i), out string line); i++)
            {
                if (!string.IsNullOrWhiteSpace(line))
                    lines.Add(line);
            }

            var credits = new List<EndingCredit>();

            for (int i = 1; i <= EndingMaxRows && _localizer.TryGet(UIText.Ending.CreditRole(i), out string role); i++)
            {
                var names = new List<string>();

                if (_localizer.TryGet(UIText.Ending.CreditNames(i), out string cell))
                {
                    foreach (string name in cell.Split(EndingNameSeparator))
                    {
                        if (!string.IsNullOrWhiteSpace(name))
                            names.Add(name.Trim());
                    }
                }

                credits.Add(new EndingCredit(role, names));
            }

            return new EndingTexts
            {
                Lines = lines,
                CreditsTitle = _localizer.TryGet(UIText.Ending.CreditsTitle, out string title) ? title : "",
                Credits = credits,
                Thanks = _localizer.Get(UIText.Ending.Thanks),
                Prompt = _localizer.Get(UIText.Ending.Prompt),
            };
        }
    }
}
