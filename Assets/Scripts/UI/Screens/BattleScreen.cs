using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BlackHole.Unity
{
    // 전투 화면(전투 장면 위의 겹 화면). 남은 시간, 이 판이 번 Gold, 블랙홀의 판 Level·목표 Level과 다음 Level까지의 %(글자와 막대), 일시정지 여부를 받아 보여 주고, 일시정지·종료 버튼을 알린다.
    // 전투 Session을 모른다 — GameHost가 전투 Step 뒤 표시 값을 넘긴다. 진행 중인 판이 없으면 비어 있는 표시(ShowIdle)다.
    // 번 Gold는 이 판의 합계일 뿐이다. 진행 상태의 Gold는 판이 끝난 뒤 결산이 바꾼다.
    public sealed class BattleScreen : UIRoot<BattleScreen.Refs>
    {
        public enum Refs
        {
            Remaining_Text,
            EarnedGold_Text,
            Level_Text,
            // 다음 Level까지의 몫만큼 늘어나는 막대. 폭은 anchorMax.x로 정한다.
            LevelFill_Image,
            PauseBtn_Button,
            PauseBtn_Text,
            EndBtn_Button,
            EndBtn_Text,
            // 노치·둥근 모서리를 피하는 영역. 화면을 열 때와 해상도가 바뀔 때 UIManager가 Safe Area에 맞춘다(SafeAreaUtility).
            SafeAreaRoot,

            // Presentation이 바꾸는 그림. 코드는 건드리지 않는다.
            // 배경이 어두워지면 Image Theme로 칩·아이콘을 바꾼다(예: 아이콘 ink → ivory).
            GoldChip_Image,
            GoldIcon_Image,
            LevelChip_Image,
            LevelBar_Image,
            TimerChip_Image,
            TimerIcon_Image,
            PauseBtn_Image,
            PauseIcon_Image,
            EndBtn_Image,
            EndIcon_Image,
        }

        public event Action PauseClicked;
        public event Action EndClicked;

        private TMP_Text _remaining;
        private TMP_Text _earned;
        private TMP_Text _level;
        private RectTransform _levelFill;
        private TMP_Text _pauseLabel;
        private int _shownTenths = -1;
        private long _shownEarned = -1;
        private int _shownLevel = -1;
        private int _shownPercent = -1;
        private int _shownGoal = -1;
        private bool? _shownPaused;

        private const float FillSharpness = 5f;
        private float _fillValue = float.NaN;

        protected override void OnInitialize()
        {
            ScreenRefs.WarnMissing<Refs>(this);

            _remaining = View.Text(Refs.Remaining_Text);
            _earned = View.Text(Refs.EarnedGold_Text);
            _level = View.Text(Refs.Level_Text);
            _levelFill = View.Rect(Refs.LevelFill_Image);
            _pauseLabel = View.Text(Refs.PauseBtn_Text);

            BindEvent(View.Button(Refs.PauseBtn_Button), HandlePauseClicked);
            BindEvent(View.Button(Refs.EndBtn_Button), HandleEndClicked);
        }

        private void HandlePauseClicked(PointerEventData _) => PauseClicked?.Invoke();
        private void HandleEndClicked(PointerEventData _) => EndClicked?.Invoke();

        // 진행 중인 판이 없을 때의 표시. 매 프레임 불러도 된다.
        public void ShowIdle()
        {
            if (_shownTenths != int.MinValue && _remaining != null)
            {
                _shownTenths = int.MinValue;
                _remaining.text = "-";
            }

            if (_shownEarned != long.MinValue && _earned != null)
            {
                _shownEarned = long.MinValue;
                _earned.text = string.Empty;
            }

            if (_shownLevel != int.MinValue && _level != null)
            {
                _shownLevel = int.MinValue;
                _level.text = string.Empty;
                _fillValue = float.NaN;
                SetLevelFill(0);
            }

            if (_shownPaused != false && _pauseLabel != null)
            {
                _shownPaused = false;
                _pauseLabel.text = "Pause";
            }
        }

        // 매 프레임 불러도 된다. 보이는 값이 바뀔 때만 글자를 고친다.
        // progress: 지금 Level에서 다음 Level까지의 몫(0 ~ 1). 마지막 Level이면 1이다. goalLevel: 이번 성장도의 목표 Level(0이면 목표 없음).
        public void Show(float remainingSeconds, long earnedGold, bool paused, int level, float progress, int goalLevel)
        {
            int tenths = Mathf.CeilToInt(remainingSeconds * 10);

            if (tenths != _shownTenths && _remaining != null)
            {
                _shownTenths = tenths;
                _remaining.text = (tenths / 10f).ToString("F1", CultureInfo.InvariantCulture);
            }

            if (earnedGold != _shownEarned && _earned != null)
            {
                _shownEarned = earnedGold;
                _earned.text = "+" + earnedGold.ToString("N0", CultureInfo.InvariantCulture) + " Gold";
            }

            int percent = Mathf.FloorToInt(progress * 100);

            if ((level != _shownLevel || percent != _shownPercent || goalLevel != _shownGoal) && _level != null)
            {
                _shownLevel = level;
                _shownPercent = percent;
                _shownGoal = goalLevel;
                _level.text = "Lv " + level.ToString(CultureInfo.InvariantCulture)
                    + (goalLevel > 0 ? " / " + goalLevel.ToString(CultureInfo.InvariantCulture) : string.Empty)
                    + "  " + percent.ToString(CultureInfo.InvariantCulture) + "%";
                // 막대도 글자와 같은 %로 맞춘다. 1% 단위로만 바뀌므로 매 프레임 레이아웃을 다시 잡지 않는다.
                //SetLevelFill(percent / 100f);
            }

            UpdateLevelFill(level, progress);

            if (paused != _shownPaused && _pauseLabel != null)
            {
                _shownPaused = paused;
                _pauseLabel.text = paused ? "Resume" : "Pause";
            }
        }

        private void SetLevelFill(float amount)
        {
            // if (_levelFill != null)
            //     _levelFill.anchorMax = new Vector2(Mathf.Clamp01(amount), _levelFill.anchorMax.y);
            if (_levelFill == null)
                return;
            
            float x = Mathf.Clamp01(amount);

            if (Mathf.Approximately(_levelFill.anchorMax.x, x))
                return;
            
            _levelFill.anchorMax = new Vector2(x, _levelFill.anchorMax.y);
        }

        private void UpdateLevelFill(int level, float progress)
        {
            float target = level + Mathf.Min(Mathf.Clamp01(progress), 0.9999f);

            if (float.IsNaN(_fillValue) || target < _fillValue)
            {
                _fillValue = target;
            }
            else
            {
                float t = 1f - Mathf.Exp(-FillSharpness * Time.unscaledDeltaTime);
                _fillValue = Mathf.Lerp(_fillValue, target, t);

                if (target - _fillValue < 0.001f)
                    _fillValue = target;
            }

            SetLevelFill(_fillValue - Mathf.Floor(_fillValue));
        }   
    }
}
