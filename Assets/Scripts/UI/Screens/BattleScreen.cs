using System;
using System.Globalization;
using BlackHole.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BlackHole.Unity
{
    // 전투 화면(전투 장면 위의 겹 화면). 전투 중에 필요한 정보만 보여 준다: 남은 시간, 다음 Level까지의 진행 막대, 일시정지·종료 버튼.
    // Level 숫자·목표·% 글자는 두지 않는다(막대와 겹친다). 번 Gold 칩은 프리팹에서 꺼 두었다(결산에서 본다) — 켜면 다시 보인다.
    // 막대 아래의 마름모 칸(MilestoneRow)은 이번 구간에서 오른 Level 수다. 칸 수는 구간의 Level 수(목표 − 시작, 최대 10). 이정표에 닿으면 달성 연출을 하고, 그동안 GameHost가 결산 전환을 미룬다.
    // 전투 Session을 모른다 — GameHost가 전투 Step 뒤 표시 값을 넘긴다. 진행 중인 판이 없으면 비어 있는 표시(ShowIdle)다.
    // 번 Gold는 이 판의 합계일 뿐이다. 진행 상태의 Gold는 판이 끝난 뒤 결산이 바꾼다.
    // 은하가 나오는 판(블랙홀 구역)에서는 화면 아래 가운데에 은하 체력바(GalaxyHealthBar)를 보인다. GameHost가 ShowGalaxy로 상태를 넘긴다.
    public sealed class BattleScreen : UIRoot<BattleScreen.Refs>, ILocalizedView, INumberFormatView
    {
        public enum Refs
        {
            Remaining_Text,
            // 남은 시간이 늘어난 순간 숫자 오른쪽에 잠깐 뜨는 "+0.3". 프리팹에서는 꺼져 있고 코드가 켠다.
            TimeGain_Text,
            // 번 Gold. 칩(GoldChip_Image)이 프리팹에서 꺼져 있어 지금은 보이지 않는다.
            EarnedGold_Text,
            // 다음 Level까지의 몫만큼 늘어나는 막대. 폭은 anchorMax.x로 정한다.
            LevelFill_Image,
            // 이정표 누적 칸의 행(프리팹에 10칸, 구간 Level 수만큼 보임)과, 달성 때 퍼지는 파편(UIMilestoneBurst). 둘 다 터치를 받지 않는다.
            MilestoneRow,
            MilestoneBurst,
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
            PauseBtn_Image,
            PauseIcon_Image,
            EndBtn_Image,
            EndIcon_Image,
        }

        public event Action PauseClicked;
        public event Action EndClicked;

        private TMP_Text _remaining;
        private TMP_Text _gain;
        private TMP_Text _earned;
        private RectTransform _levelFill;
        private TMP_Text _pauseLabel;
        private MilestoneRow _milestones;
        // 은하 체력바(코드로 만든다). 이름·줄 수 글자와 바탕은 Styled에 넣어 테마를 받고, 막대 안 수치는 OnPresented가 넘긴다.
        private GalaxyHealthBar _galaxyBar;
        private int _galaxyTier = -1;
        private int _shownTenths = -1;
        private long _shownEarned = -1;
        // Gold 표기(설정). UISettingsSync가 넣는다.
        private NumberFormat _numberFormat;
        private bool _idle;
        private bool? _shownPaused;
        private Localizer _localizer;

        private const float FillSharpness = 5f;
        // 남은 시간 숫자 한 칸의 폭(글자 크기 기준)과 소수 부분의 크기. 글꼴이 바뀌면 화면에서 간격을 보고 맞춘다.
        private const string DigitWidth = "0.6em";
        private const string FractionSize = "70%";
        private float _fillValue = float.NaN;

        // 시간 증가 알림. 짧게 연달아 늘면 하나로 합쳐 "+0.9"처럼 보이고, 마지막 증가 뒤 잠시 있다가 흐려진다.
        // 그 순간 남은 시간 숫자를 살짝 튕긴다. 화면에서 보고 맞출 값이다.
        private const float GainMergeWindow = 0.5f; // 이 안에 다시 늘면 합친다(초)
        private const float GainHold = 0.5f;        // 마지막 증가 뒤 그대로 보이는 시간(초)
        private const float GainFade = 0.3f;        // 그 뒤 흐려지는 시간(초)
        private const float GainGap = 12f;          // 숫자 오른쪽 끝과의 틈(px)
        private const float MinGain = 0.05f;        // 이보다 작은 증가는 모아 두었다가 알린다(초)
        private const float PopDuration = 0.15f;
        private const float PopScale = 0.1f;        // 1.0 → 1.1 → 1.0

        private float _shownAdded = float.NaN;      // 마지막으로 알린 "더해진 시간" 합. NaN이면 기준이 없다(새 판).
        private float _gainSum;
        private float _sinceGain = float.PositiveInfinity;
        private float _popTime = PopDuration;

        protected override void OnInitialize()
        {
            _remaining = View.Text(Refs.Remaining_Text);
            _gain = View.Text(Refs.TimeGain_Text);
            ResetGain();
            _earned = View.Text(Refs.EarnedGold_Text);
            _levelFill = View.Rect(Refs.LevelFill_Image);
            _pauseLabel = View.Text(Refs.PauseBtn_Text);

            RectTransform burstRect = View.Rect(Refs.MilestoneBurst);
            _milestones = new MilestoneRow(
                View.Rect(Refs.MilestoneRow),
                burstRect != null ? burstRect.GetComponent<UIMilestoneBurst>() : null);
            _milestones.Reset();

            // 은하 체력바의 기준 글꼴은 일시정지 글자의 것(한글이 있는 글꼴). 테마가 들어오면 언어·역할에 맞는 글꼴로 바뀐다.
            RectTransform safeArea = View.Rect(Refs.SafeAreaRoot);

            if (safeArea != null)
                _galaxyBar = new GalaxyHealthBar(safeArea, _pauseLabel != null ? _pauseLabel.font : null, Styled);

            BindEvent(View.Button(Refs.PauseBtn_Button), HandlePauseClicked);
            BindEvent(View.Button(Refs.EndBtn_Button), HandleEndClicked);
        }

        // 화면이 닫힐 때 알림·튕김·이정표 칸을 지운다. 다시 열 때 남아 있지 않게 한다.
        private void OnDisable()
        {
            ResetGain();
            _milestones?.Reset();
            _galaxyBar?.Hide();
        }

        // 이정표 칸·은하 체력바의 연출은 실제 시간으로 흐른다(정지 중·판이 끝난 뒤에도).
        private void Update()
        {
            _milestones?.Tick(Time.unscaledDeltaTime);
            _galaxyBar?.Tick(Time.unscaledDeltaTime);
        }

        // 테마가 들어왔다: 은하 체력바의 막대 안 수치 글꼴·크기를 맞춘다(나머지는 Styled가 맞췄다).
        protected override void OnPresented(in UIAppliedTheme theme) => _galaxyBar?.ApplyTheme(theme);

        // 이 판의 은하 상태(GameHost가 매 프레임). 은하가 없는 판이면 null — 막대를 숨긴다.
        internal void ShowGalaxy(GalaxyHealthState? galaxy)
        {
            if (_galaxyBar == null)
                return;

            if (galaxy is GalaxyHealthState state)
                _galaxyTier = state.Tier;

            _galaxyBar.Show(galaxy, galaxy.HasValue ? GalaxyName(_galaxyTier) : null);
        }

        // 은하 이름(색): battle.galaxy.<색 등급>. 번역표에 없으면 battle.galaxy.
        private string GalaxyName(int tier) =>
            _localizer != null && _localizer.TryGet(UIText.Battle.Galaxy(tier), out string name) ? name : Localized(UIText.Battle.GalaxyFallback);

        // 이정표 달성 연출 중인가. 끝날 때까지 GameHost가 결산 전환을 미루고, 이 화면은 버튼을 받지 않는다.
        public bool IsCelebratingMilestone => _milestones != null && _milestones.IsCelebrating;

        // 이정표에 닿아 판이 끝났다. 10칸을 채운 모습과 달성 연출을 시작한다. 시작했으면 true(한 판에 한 번).
        public bool TryPlayMilestoneReached() => _milestones != null && _milestones.TryCelebrate();

        private void HandlePauseClicked(PointerEventData _)
        {
            if (!IsCelebratingMilestone)
                PauseClicked?.Invoke();
        }

        private void HandleEndClicked(PointerEventData _)
        {
            if (!IsCelebratingMilestone)
                EndClicked?.Invoke();
        }

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

            if (!_idle)
            {
                _idle = true;
                _fillValue = float.NaN;
                SetLevelFill(0);
                ResetGain();
                _milestones?.Reset();
                _milestones?.SetVisible(false);
                _galaxyBar?.Hide();
            }

            if (_shownPaused != false && _pauseLabel != null)
            {
                _shownPaused = false;
                _pauseLabel.text = PauseLabel(false);
            }
        }

        // 매 프레임 불러도 된다. 보이는 값이 바뀔 때만 글자를 고친다.
        // addedSeconds: 이 판에서 제한 시간에 더해진 초의 합(GameSession.AddedSeconds). 늘어난 만큼 "+0.3"을 띄운다.
        // level·progress: 보여줄 Level과 다음 Level까지의 몫(0 ~ 1, 마지막 Level이면 1). 막대만 그린다 — Level이 오르면 막대가 한 바퀴 돈다.
        // wheeling: progress가 성장 공급 바퀴(GrowthPacer)의 진행도라 막대를 그 값에 바로 맞춘다(보간 없음 — 규칙의 시각과 어긋나지 않게).
        //   아니면 progress는 EXP 진행도라 부드럽게 따라간다.
        // startLevel·goalLevel: 이 판의 시작 Level(체크포인트)과 목표 Level(Hq.StartLevel·GoalLevel). 이정표 칸은 level - startLevel개를 채운다.
        //   목표가 없으면(마지막 이정표 뒤, goalLevel == 0) 칸 행을 숨긴다.
        public void Show(
            float remainingSeconds, float addedSeconds, long earnedGold, bool paused,
            int level, float progress, bool wheeling, int startLevel, int goalLevel)
        {
            _idle = false;

            if (_milestones != null)
            {
                bool hasGoal = goalLevel > 0;
                _milestones.SetVisible(hasGoal);

                if (hasGoal)
                {
                    _milestones.SetSlots(goalLevel - startLevel);
                    _milestones.Show(level - startLevel);
                }
            }

            // 시간 제한이 없는 판(튜토리얼, remainingSeconds가 무한대)은 남은 시간을 숨기고 숫자를 고치지 않는다.
            bool timed = !float.IsPositiveInfinity(remainingSeconds);

            if (_remaining != null && _remaining.gameObject.activeSelf != timed)
                _remaining.gameObject.SetActive(timed);

            int tenths = timed ? Mathf.CeilToInt(remainingSeconds * 10) : _shownTenths;
            bool remainingChanged = false;

            if (tenths != _shownTenths && _remaining != null)
            {
                _shownTenths = tenths;
                _remaining.text = RemainingText(tenths);
                remainingChanged = true;
            }

            if (TrackGain(addedSeconds) || (remainingChanged && GainVisible))
                PlaceGain();

            AnimateGain(Time.unscaledDeltaTime);

            if (earnedGold != _shownEarned && _earned != null)
            {
                _shownEarned = earnedGold;
                _earned.text = EarnedText(earnedGold);
            }

            UpdateLevelFill(level, progress, wheeling);

            if (paused != _shownPaused && _pauseLabel != null)
            {
                _shownPaused = paused;
                _pauseLabel.text = PauseLabel(paused);
            }
        }

        // 지금 보이는 값(번 Gold·일시정지 글자)을 지금 언어로 다시 쓴다. 비어 있는 표시는 그대로 둔다.
        public void Relocalize(Localizer localizer)
        {
            _localizer = localizer;

            if (_earned != null && _shownEarned >= 0)
                _earned.text = EarnedText(_shownEarned);

            if (_pauseLabel != null && _shownPaused is bool paused)
                _pauseLabel.text = PauseLabel(paused);

            if (_galaxyTier >= 0)
                _galaxyBar?.Rename(GalaxyName(_galaxyTier));
        }

        // Gold 표기 설정. 보이는 번 Gold를 새 표기로 다시 쓴다.
        public void SetNumberFormat(NumberFormat format)
        {
            _numberFormat = format;

            if (_earned != null && _shownEarned >= 0)
                _earned.text = EarnedText(_shownEarned);
        }

        private string EarnedText(long earnedGold) => Localized(UIText.Battle.Earned, GoldText.Of(earnedGold, _numberFormat));

        // 남은 시간 글자. 0.1초마다 바뀌므로 흔들림을 줄인다.
        // - 숫자는 칸 폭을 고정한다(<mspace>). 1과 8의 폭이 달라 가운데 정렬에서 숫자 묶음이 좌우로 움직이지 않게 한다.
        //   소수점은 칸에 넣지 않는다 — 숫자 한 칸만큼 벌어져 보인다.
        // - 소수 부분(".9")은 작게 그려 자주 바뀌는 자리가 덜 눈에 띄게 한다.
        // 자릿수가 바뀔 때(10 → 9)만 묶음 폭이 한 칸 줄어든다.
        private static string RemainingText(int tenths)
        {
            tenths = Mathf.Max(0, tenths);
            string whole = (tenths / 10).ToString(CultureInfo.InvariantCulture);
            string fraction = (tenths % 10).ToString(CultureInfo.InvariantCulture);

            return $"<mspace={DigitWidth}>{whole}</mspace><size={FractionSize}>.<mspace={DigitWidth}>{fraction}</mspace></size>";
        }

        private bool GainVisible => _gain != null && _gain.gameObject.activeSelf;

        // 더해진 시간 합이 늘었으면 알림 글자를 고치고 true. 새 판(합이 줄었거나 기준이 없음)이면 기준만 잡는다.
        private bool TrackGain(float addedSeconds)
        {
            if (float.IsNaN(_shownAdded) || addedSeconds < _shownAdded)
            {
                _shownAdded = addedSeconds;
                return false;
            }

            float gained = addedSeconds - _shownAdded;
            if (gained < MinGain)
                return false;

            _shownAdded = addedSeconds;
            _gainSum = _sinceGain <= GainMergeWindow ? _gainSum + gained : gained;
            _sinceGain = 0;
            _popTime = 0;

            if (_gain == null)
                return false;

            _gain.text = "+" + _gainSum.ToString("0.0", CultureInfo.InvariantCulture);
            _gain.alpha = 1;
            _gain.gameObject.SetActive(true);
            return true;
        }

        // 알림을 숫자 오른쪽 끝 바로 옆에 둔다. 숫자 폭은 자릿수·글자 크기 설정에 따라 바뀐다.
        // 숫자 글자는 Timer 전체에 펼쳐져 있고 기준점이 가운데라, 그 지역 좌표의 x가 곧 Timer 가운데 기준 x다.
        private void PlaceGain()
        {
            if (_gain == null || _remaining == null)
                return;

            _remaining.ForceMeshUpdate();
            RectTransform gain = _gain.rectTransform;
            gain.anchoredPosition = new Vector2(_remaining.textBounds.max.x + GainGap, gain.anchoredPosition.y);
        }

        private void AnimateGain(float deltaTime)
        {
            if (_popTime < PopDuration && _remaining != null)
            {
                _popTime += deltaTime;
                float t = Mathf.Clamp01(_popTime / PopDuration);
                float scale = t < 1 ? 1 + PopScale * Mathf.Sin(t * Mathf.PI) : 1;
                _remaining.rectTransform.localScale = new Vector3(scale, scale, 1);
            }

            if (float.IsPositiveInfinity(_sinceGain))
                return;

            _sinceGain += deltaTime;
            float alpha = 1 - Mathf.Clamp01((_sinceGain - GainHold) / GainFade);

            if (alpha > 0)
            {
                if (_gain != null)
                    _gain.alpha = alpha;

                return;
            }

            _sinceGain = float.PositiveInfinity;

            if (_gain != null)
                _gain.gameObject.SetActive(false);
        }

        private void ResetGain()
        {
            _shownAdded = float.NaN;
            _gainSum = 0;
            _sinceGain = float.PositiveInfinity;
            _popTime = PopDuration;

            if (_gain != null)
                _gain.gameObject.SetActive(false);

            if (_remaining != null)
                _remaining.rectTransform.localScale = Vector3.one;
        }

        private string PauseLabel(bool paused) => Localized(paused ? UIText.Battle.Resume : UIText.Battle.Pause);

        private string Localized(string key, params object[] args) =>
            _localizer != null ? _localizer.Format(key, args) : key;

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

        private void UpdateLevelFill(int level, float progress, bool snap)
        {
            float target = level + Mathf.Min(Mathf.Clamp01(progress), 0.9999f);

            if (snap || float.IsNaN(_fillValue) || target < _fillValue)
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
