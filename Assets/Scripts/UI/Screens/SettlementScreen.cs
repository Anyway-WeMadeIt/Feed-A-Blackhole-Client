using System;
using System.Globalization;
using BlackHole.Core;
using BlackHole.Systems.Numbers;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BlackHole.Unity
{
    public sealed class SettlementScreen : UIRoot<SettlementScreen.Refs>, ILocalizedView, INumberFormatView
    {
        public enum Refs
        {
            Result_Text,
            // 성장도 막대의 채움. 폭은 anchorMax.x로 정한다.
            StageFill_Image,
            Stage_Text,
            AsteroidRow_Button,
            AsteroidRow_Text,
            PlanetRow_Button,
            PlanetRow_Text,
            StarRow_Button,
            StarRow_Text,
            TotalLabel_Text,
            EarnedGold_Text,
            TotalGold_Text,
            UpgradeBtn_Button,
            UpgradeBtn_Text,
            ContinueBtn_Button,
            Tooltip_Image,
            Tooltip_Text,
            // 노치·둥근 모서리를 피하는 영역. 화면을 열 때와 해상도가 바뀔 때 UIManager가 Safe Area에 맞춘다(SafeAreaUtility).
            SafeAreaRoot,
            // 배경 위의 별빛·궤도 문양(UIScreenAmbient). 테마에 따라 머티리얼만 바뀐다.
            Ambient,
            // 오른쪽 통계 칸(스킬별 피해·수집). 칸에 손을 올리면 툴팁으로 이름을 보인다.
            // 칸은 두 그룹(격자)에 들어 있고 그룹 사이에 구분선이 있다. 값이 0인 칸·빈 그룹·구분선은 숨긴다.
            SkillStats,
            CountStats,
            StatsDivider,
            // 물질 행과 Gold 사이의 구분선(단색). 테마의 글자색을 옅게 쓴다.
            Divider,
            ElectricStarDamage_Button,
            ElectricStarDamage_Text,
            LaserDamage_Button,
            LaserDamage_Text,
            SupernovaDamage_Button,
            SupernovaDamage_Text,
            GoldenAsteroidGold_Button,
            GoldenAsteroidGold_Text,
            ElectricAsteroidDamage_Button,
            ElectricAsteroidDamage_Text,
            BreakerDamage_Button,
            BreakerDamage_Text,
            BreakerCritDamage_Button,
            BreakerCritDamage_Text,
            BreakerClicks_Button,
            BreakerClicks_Text,
            DestroyedStars_Button,
            DestroyedStars_Text,
            DestroyedPlanets_Button,
            DestroyedPlanets_Text,
            CollectedMoons_Button,
            CollectedMoons_Text,
            CollectedComets_Button,
            CollectedComets_Text,
            DestroyedAsteroids_Button,
            DestroyedAsteroids_Text,
            AddedTime_Button,
            AddedTime_Text,
            // 머리글·행 이름. 코드는 쓰지 않고 UI 글자 표가 언어에 맞게 바꾼다(ViewTexts).
            DestroyedCaption_Text,
            AsteroidName_Text,
            PlanetName_Text,
            StarName_Text,
            GoldCaption_Text,
            GrowthCaption_Text,

            // Presentation이 바꾸는 그림과 글자. 코드는 건드리지 않는다.
            Background_Image,
            AsteroidRow_Image,
            AsteroidIcon_Image,
            PlanetRow_Image,
            PlanetIcon_Image,
            StarRow_Image,
            StarIcon_Image,
            GoldIcon_Image,
            StageBar_Image,
            UpgradeBtn_Image,
            ContinueBtn_Image,
            ContinueBtn_Text,
            ElectricStarDamage_Image,
            ElectricStarDamageIcon_Image,
            LaserDamage_Image,
            LaserDamageIcon_Image,
            SupernovaDamage_Image,
            SupernovaDamageIcon_Image,
            GoldenAsteroidGold_Image,
            GoldenAsteroidGoldIcon_Image,
            ElectricAsteroidDamage_Image,
            ElectricAsteroidDamageIcon_Image,
            BreakerDamage_Image,
            BreakerDamageIcon_Image,
            BreakerCritDamage_Image,
            BreakerCritDamageIcon_Image,
            BreakerClicks_Image,
            BreakerClicksIcon_Image,
            DestroyedStars_Image,
            DestroyedStarsIcon_Image,
            DestroyedPlanets_Image,
            DestroyedPlanetsIcon_Image,
            CollectedMoons_Image,
            CollectedMoonsIcon_Image,
            CollectedComets_Image,
            CollectedCometsIcon_Image,
            DestroyedAsteroids_Image,
            DestroyedAsteroidsIcon_Image,
            AddedTime_Image,
            AddedTimeIcon_Image,
        }

        // 툴팁과 행 사이의 틈.
        private const float TooltipGap = 6;

        private readonly Vector3[] _corners = new Vector3[4];

        private TMP_Text _result;
        private RectTransform _stageFillRect;
        private TMP_Text _stageText;
        private TMP_Text _totalLabel;
        private TMP_Text _earned;
        private TMP_Text _totalGold;
        private TMP_Text _upgradeLabel;
        private TMP_Text _asteroidText;
        private TMP_Text _planetText;
        private TMP_Text _starText;

        private Button _asteroidRow;
        private Button _planetRow;
        private Button _starRow;
        private Button _upgradeButton;
        private Button _continueButton;

        private RectTransform _tooltip;
        private TMP_Text _tooltipText;
        private RectTransform _tooltipOwner;

        private GameObject _skillStats;
        private GameObject _countStats;
        private GameObject _statsDivider;
        private StatCell _electricStarDamage;
        private StatCell _laserDamage;
        private StatCell _supernovaDamage;
        private StatCell _goldenAsteroidGold;
        private StatCell _electricAsteroidDamage;
        private StatCell _breakerDamage;
        private StatCell _breakerCritDamage;
        private StatCell _breakerClicks;
        private StatCell _destroyedStars;
        private StatCell _destroyedPlanets;
        private StatCell _collectedMoons;
        private StatCell _collectedComets;
        private StatCell _destroyedAsteroids;
        private StatCell _addedTime;

        // 오른쪽 통계 칸 하나: 칸 버튼(툴팁·연출), 값 글자, 툴팁 이름의 글자 키.
        private sealed class StatCell
        {
            public Button Button { get; }
            public TMP_Text Value { get; }
            public string LabelKey { get; }

            public StatCell(Button button, TMP_Text value, string labelKey)
            {
                Button = button;
                Value = value;
                LabelKey = labelKey;
            }
        }

        // 언어가 바뀌면 다시 쓸 마지막 값. 아직 보이지 않았으면 null.
        private Localizer _localizer;
        // 슬라이더·결산 소리. 캔버스의 UISoundSource에서 처음 쓸 때 찾는다. 없으면 소리 없이 진행한다.
        private UISounds _sounds;
        private UISounds Sounds => _sounds ??= UISoundSource.For(this);
        // Gold 표기(설정). UISettingsSync가 넣는다.
        private NumberFormat _numberFormat;
        private bool? _shownMilestone;
        // 이정표 글자: 지금 자리 이름과 목표 이름("소행성대 → 행성계"). 목표가 없으면(마지막 뒤) 이름 하나. 닿았으면 "행성계 도달!".
        private (string Current, string Goal, bool Reached)? _shownStage;
        // 막대의 색: 프리팹 색(차오르는 동안), 못 닿았으면 회색, 닿았으면 금색.
        private Image _stageFillImage;
        private Color _stageFillColor = Color.white;
        private bool _stageReached;
        private static readonly Color StageShortColor = new(0.5f, 0.52f, 0.54f, 1);
        private static readonly Color StageReachedColor = new(0.89f, 0.79f, 0.52f, 1);
        private bool? _shownReward;
        private int? _shownPurchasable;

        public event Action UpgradeClicked;
        public event Action ContinueClicked;

        // 연출: 이정표 막대 채움과 물질·Gold 숫자 올리기. 화면 어디든 누르면 끝까지 간다(SkipAnimation).
        // 숫자는 소행성 → 행성 → 별 → 총 Gold 순서(합계 줄은 숨겨 두고 값만 총 Gold의 시작점에 쓴다). 막대는 앞의 셋이 오르는 동안 찬다.
        private readonly FillTween _stageFill = new();
        private readonly CountUpSequence _countUp = new();
        private const float DefaultDuration = 1.0f;
        private const float MatterStepCount = 4;
        private const float StageFillSteps = 3;
        // 연출을 위한 목표값. 건너뛰기와 숫자 표기 다시 쓰기에 쓴다.
        private int _asteroidTarget;
        private int _planetTarget;
        private int _starTarget;
        private long _earnedTarget;
        private long _totalBegin;
        private long _totalTarget;
        // 값이 바뀐 글자를 잠깐 키우는 강조 효과. 한 번에 하나.
        private const float ScaleDuration = 0.15f;
        private const float HighlightScale = 1.2f;
        private TMP_Text _scaleTarget;
        private float _scaleTimer;

        protected override void OnInitialize()
        {
            _result = View.Text(Refs.Result_Text);
            _stageFillRect = View.Rect(Refs.StageFill_Image);
            _stageFillImage = View.Image(Refs.StageFill_Image);
            if (_stageFillImage != null)
                _stageFillColor = _stageFillImage.color;

            _stageFill.Completed += HandleStageFillCompleted;
            _countUp.Completed += HandleCountUpCompleted;
            _countUp.ValueChanged += PlayScaleEffect;
            _stageText = View.Text(Refs.Stage_Text);
            _totalLabel = View.Text(Refs.TotalLabel_Text);
            _earned = View.Text(Refs.EarnedGold_Text);
            _totalGold = View.Text(Refs.TotalGold_Text);
            _upgradeLabel = View.Text(Refs.UpgradeBtn_Text);
            _asteroidText = View.Text(Refs.AsteroidRow_Text);
            _planetText = View.Text(Refs.PlanetRow_Text);
            _starText = View.Text(Refs.StarRow_Text);
            _tooltip = View.Rect(Refs.Tooltip_Image);
            _tooltipText = View.Text(Refs.Tooltip_Text);
            HideTooltip();

            // TextBindings에 없는 글자(합계·툴팁·캡션·통계 값)와 바탕색·구분선은 Styled가 테마에 맞춘다. 글자 크기는 프리팹 크기 × 배율.
            Styled.AddText(_totalGold, UITextRole.Body, UITextSizePolicy.Prefab);
            Styled.AddText(_tooltipText, UITextRole.Body, UITextSizePolicy.Prefab);
            Styled.AddText(View.Text(Refs.DestroyedCaption_Text), UITextRole.Caption, UITextSizePolicy.Prefab);
            Styled.AddText(View.Text(Refs.AsteroidName_Text), UITextRole.Caption, UITextSizePolicy.Prefab);
            Styled.AddText(View.Text(Refs.PlanetName_Text), UITextRole.Caption, UITextSizePolicy.Prefab);
            Styled.AddText(View.Text(Refs.StarName_Text), UITextRole.Caption, UITextSizePolicy.Prefab);
            Styled.AddText(View.Text(Refs.GoldCaption_Text), UITextRole.Caption, UITextSizePolicy.Prefab);
            // 골드 줄은 "$5,700" 숫자만 보인다. 캡션 "골드"와 그 아이콘은 프리팹에 두되 숨긴다.
            View.Text(Refs.GoldCaption_Text)?.gameObject.SetActive(false);
            View.Image(Refs.GoldIcon_Image)?.gameObject.SetActive(false);
            Styled.AddText(View.Text(Refs.GrowthCaption_Text), UITextRole.Caption, UITextSizePolicy.Prefab);

            Styled.AddImageColor(GetComponent<Image>(), UIColorRole.Backdrop);
            Styled.AddImageColor(View.Image(Refs.Divider), UIColorRole.TextMain);
            Styled.AddImageColor(View.Image(Refs.StatsDivider), UIColorRole.TextMain);

            _asteroidRow = View.Button(Refs.AsteroidRow_Button);
            _planetRow = View.Button(Refs.PlanetRow_Button);
            _starRow = View.Button(Refs.StarRow_Button);
            _upgradeButton = View.Button(Refs.UpgradeBtn_Button);
            _continueButton = View.Button(Refs.ContinueBtn_Button);

            // 물질 행 셋: 올리면 툴팁. 통계 칸(BindStat)과 같은 방식이다.
            ButtonFeedback.Bind(_asteroidRow,
                onHover: () => ShowTooltip(_asteroidRow, Localized(UIText.Settlement.RowAsteroid)),
                onLeave: () => HideTooltip(_asteroidRow));
            ButtonFeedback.Bind(_planetRow,
                onHover: () => ShowTooltip(_planetRow, Localized(UIText.Settlement.RowPlanet)),
                onLeave: () => HideTooltip(_planetRow));
            ButtonFeedback.Bind(_starRow,
                onHover: () => ShowTooltip(_starRow, Localized(UIText.Settlement.RowStar)),
                onLeave: () => HideTooltip(_starRow));

            ButtonFeedback.Bind(_upgradeButton, () => UpgradeClicked?.Invoke());
            ButtonFeedback.Bind(_continueButton, () => ContinueClicked?.Invoke());

            _skillStats = ObjectOf(View.Rect(Refs.SkillStats));
            _countStats = ObjectOf(View.Rect(Refs.CountStats));
            _statsDivider = ObjectOf(View.Rect(Refs.StatsDivider));
            _electricStarDamage = BindStat(Refs.ElectricStarDamage_Button, Refs.ElectricStarDamage_Text, UIText.Settlement.Stat.ElectricStarDamage);
            _laserDamage = BindStat(Refs.LaserDamage_Button, Refs.LaserDamage_Text, UIText.Settlement.Stat.LaserDamage);
            _supernovaDamage = BindStat(Refs.SupernovaDamage_Button, Refs.SupernovaDamage_Text, UIText.Settlement.Stat.SupernovaDamage);
            _goldenAsteroidGold = BindStat(Refs.GoldenAsteroidGold_Button, Refs.GoldenAsteroidGold_Text, UIText.Settlement.Stat.GoldenAsteroidGold);
            _electricAsteroidDamage = BindStat(Refs.ElectricAsteroidDamage_Button, Refs.ElectricAsteroidDamage_Text, UIText.Settlement.Stat.ElectricAsteroidDamage);
            _breakerDamage = BindStat(Refs.BreakerDamage_Button, Refs.BreakerDamage_Text, UIText.Settlement.Stat.BreakerDamage);
            _breakerCritDamage = BindStat(Refs.BreakerCritDamage_Button, Refs.BreakerCritDamage_Text, UIText.Settlement.Stat.BreakerCritDamage);
            _breakerClicks = BindStat(Refs.BreakerClicks_Button, Refs.BreakerClicks_Text, UIText.Settlement.Stat.BreakerClicks);
            _destroyedStars = BindStat(Refs.DestroyedStars_Button, Refs.DestroyedStars_Text, UIText.Settlement.Stat.DestroyedStars);
            _destroyedPlanets = BindStat(Refs.DestroyedPlanets_Button, Refs.DestroyedPlanets_Text, UIText.Settlement.Stat.DestroyedPlanets);
            _collectedMoons = BindStat(Refs.CollectedMoons_Button, Refs.CollectedMoons_Text, UIText.Settlement.Stat.CollectedMoons);
            _collectedComets = BindStat(Refs.CollectedComets_Button, Refs.CollectedComets_Text, UIText.Settlement.Stat.CollectedComets);
            _destroyedAsteroids = BindStat(Refs.DestroyedAsteroids_Button, Refs.DestroyedAsteroids_Text, UIText.Settlement.Stat.DestroyedAsteroids);
            _addedTime = BindStat(Refs.AddedTime_Button, Refs.AddedTime_Text, UIText.Settlement.Stat.AddedTime);
        }

        // 화면이 닫힐 때 툴팁도 닫는다. 다시 열 때 남아 있지 않게 한다.
        private void OnDisable() => HideTooltip();

        private StatCell BindStat(Refs button, Refs value, string labelKey)
        {
            StatCell cell = new(View.Button(button), View.Text(value), labelKey);
            Styled.AddText(cell.Value, UITextRole.Body, UITextSizePolicy.Prefab);

            ButtonFeedback.Bind(cell.Button,
                onHover: () => ShowTooltip(cell.Button, Localized(cell.LabelKey)),
                onLeave: () => HideTooltip(cell.Button));

            return cell;
        }


        // milestone: 이정표에 닿아 끝난 판인가(남은 시간과 관계없이 끝났다).
        public void ShowResult(bool milestone)
        {
            _shownMilestone = milestone;

            if (_result != null)
                _result.text = Localized(milestone ? UIText.Settlement.ResultMilestone : UIText.Settlement.ResultOver);
        }

        // 이정표 막대: 이번 판에 시작 체크포인트에서 목표 이정표까지 얼마나 갔는가(levelFraction 0 ~ 1). 물질 집계가 올라가는 동안 차오르고,
        // 다 찬 뒤 못 닿았으면 회색, 닿았으면 금색이 된다. current·goal: 이정표 이름(goal이 null이면 마지막 이정표 뒤).
        public void ShowStage(string current, string goal, float levelFraction, bool reached)
        {
            _stageReached = reached;

            if (_stageFillRect != null)
            {
                if (_stageFillImage != null)
                    _stageFillImage.color = _stageFillColor;

                // 슬라이더 SFX 길이에 맞춰 물질 단계 셋이 올라가는 동안 막대가 찬다.
                float length = Sounds?.SliderLength ?? 0f;
                float total = length > 0f ? length : DefaultDuration;
                _countUp.StepSeconds = total / MatterStepCount;
                _stageFill.Start(_stageFillRect, reached ? 1 : levelFraction, _countUp.StepSeconds * StageFillSteps);
                Sounds?.PlaySlider();
            }

            _shownStage = (current, goal, reached);
            WriteStageText(current, goal, reached);
        }

        private void WriteStageText(string current, string goal, bool reached)
        {
            if (_stageText == null)
                return;

            if (reached && goal != null)
                _stageText.text = Localized(UIText.Settlement.MilestoneReached, goal);
            else if (goal != null)
                _stageText.text = Localized(UIText.Settlement.Milestone, current, goal);
            else
                _stageText.text = Localized(UIText.Settlement.MilestoneLast, current);
        }

        // 물질 행: 물질별 처치 수. 판 기록에 종류별 Gold가 생기면 Gold로 바꾼다.
        public void ShowMatter(int asteroids, int planets, int stars)
        {
            _asteroidTarget = asteroids;
            _planetTarget = planets;
            _starTarget = stars;

            // 숫자 연출을 처음부터: 물질 셋을 넣고, 총 Gold는 ShowGold가 뒤에 붙인다.
            _countUp.Clear();
            _countUp.Add(_asteroidText, 0, asteroids, CountOf);
            _countUp.Add(_planetText, 0, planets, CountOf);
            _countUp.Add(_starText, 0, stars, CountOf);
        }

        // earned: 이 판이 번 Gold. settled: 결산이 더한 Gold(이정표로 끝났으면 목표 잔액까지의 차액). total: 결산 뒤 진행 상태의 Gold.
        public void ShowGold(long earned, long settled, bool milestone, long total)
        {
            _earnedTarget = milestone ? settled : earned;
            _totalTarget = total;
            // 결산 전 잔액. 실제로 더해진 것은 settled다(이정표 판은 번 Gold를 버린다).
            _totalBegin = total - settled;

            _shownReward = milestone;

            // 합계 줄은 데이터만 두고 보이지 않는다(값은 _earnedTarget·_totalBegin에 남아 총 골드 연출의 시작점이 된다).
            if (_totalLabel != null)
            {
                _totalLabel.text = Localized(milestone ? UIText.Settlement.Reward : UIText.Settlement.Total);
                _totalLabel.gameObject.SetActive(false);
            }

            if (_earned != null)
            {
                _earned.text = Money(_earnedTarget);
                _earned.gameObject.SetActive(false);
            }

            // 총 Gold는 결산 전 잔액에서 결산 뒤 잔액으로 오른다. 물질 셋 다음 차례.
            _countUp.Add(_totalGold, _totalBegin, total, Money);
        }

        // 오른쪽 통계 칸: 스킬별 피해·수집 통계(판 기록)와 종류별 처치 수(물질 행과 같은 값). 증가 연출 없이 바로 보인다.
        // 값이 0인 칸은 숨기고, 남은 칸은 그룹의 격자(GridLayoutGroup)가 앞에서부터 채운다. 빈 그룹과 구분선도 숨긴다.
        public void ShowStats(BattleStats stats, int asteroids, int planets, int stars)
        {
            // 추가된 시간은 보이는 자릿수(0.1초)로 0을 가린다.
            double addedSeconds = Math.Round(stats.AddedSeconds, 1);

            bool skills = false;
            skills |= ShowStat(_electricStarDamage, stats.ElectricStarDamage, NumberText.Compact(stats.ElectricStarDamage));
            skills |= ShowStat(_laserDamage, stats.LaserDamage, NumberText.Compact(stats.LaserDamage));
            skills |= ShowStat(_supernovaDamage, stats.SupernovaDamage, NumberText.Compact(stats.SupernovaDamage));
            skills |= ShowStat(_goldenAsteroidGold, stats.GoldenAsteroidGold, "$" + NumberText.Compact(stats.GoldenAsteroidGold));
            skills |= ShowStat(_electricAsteroidDamage, stats.ElectricAsteroidDamage, NumberText.Compact(stats.ElectricAsteroidDamage));
            skills |= ShowStat(_breakerDamage, stats.BreakerDamage, NumberText.Compact(stats.BreakerDamage));
            skills |= ShowStat(_breakerCritDamage, stats.BreakerCriticalDamage, NumberText.Compact(stats.BreakerCriticalDamage));
            skills |= ShowStat(_breakerClicks, stats.BreakerTicks, Whole(stats.BreakerTicks));

            bool counts = false;
            counts |= ShowStat(_destroyedStars, stars, Whole(stars));
            counts |= ShowStat(_destroyedPlanets, planets, Whole(planets));
            counts |= ShowStat(_collectedMoons, stats.CollectedMoons, Whole(stats.CollectedMoons));
            counts |= ShowStat(_collectedComets, stats.CollectedComets, Whole(stats.CollectedComets));
            counts |= ShowStat(_destroyedAsteroids, asteroids, Whole(asteroids));
            counts |= ShowStat(_addedTime, addedSeconds, addedSeconds.ToString("0.#", CultureInfo.InvariantCulture));

            SetActive(_skillStats, skills);
            SetActive(_countStats, counts);
            SetActive(_statsDivider, skills && counts);
        }

        // 값이 0이면 칸을 숨긴다. 보이면 true.
        private static bool ShowStat(StatCell cell, double value, string text)
        {
            bool visible = value > 0;

            if (cell.Button != null)
                cell.Button.gameObject.SetActive(visible);

            if (cell.Value != null)
                cell.Value.text = text;

            return visible;
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
                target.SetActive(active);
        }

        private static GameObject ObjectOf(RectTransform rect) => rect != null ? rect.gameObject : null;

        // 지금 Gold로 살 수 있는 노드 수. 없으면 수를 붙이지 않는다.
        public void ShowUpgradeCount(int purchasable)
        {
            _shownPurchasable = purchasable;

            if (_upgradeLabel != null)
            {
                _upgradeLabel.text = purchasable > 0
                    ? Localized(UIText.Settlement.UpgradeCount, purchasable.ToString(CultureInfo.InvariantCulture))
                    : Localized(UIText.Settlement.Upgrade);
            }
        }

        // 마지막으로 보인 결과·성장도·합계 이름·업그레이드 버튼 글자를 지금 언어로 다시 쓴다. 툴팁은 닫는다.
        // 숫자(처치 수·Gold)는 언어와 관계없어 그대로 둔다.
        public void Relocalize(Localizer localizer)
        {
            _localizer = localizer;
            HideTooltip();

            if (_shownMilestone is bool milestone)
                ShowResult(milestone);

            if (_shownStage is (string current, string goal, bool reached))
                WriteStageText(current, goal, reached);

            if (_shownReward is bool reward && _totalLabel != null)
                _totalLabel.text = Localized(reward ? UIText.Settlement.Reward : UIText.Settlement.Total);

            if (_shownPurchasable is int purchasable)
                ShowUpgradeCount(purchasable);
        }

        private UIScreenAmbient Ambient => View.Component<UIScreenAmbient>(Refs.Ambient);

        // 테마가 들어왔다: 배경 위 문양의 머티리얼을 테마에 맞춘다. 글자·구분선·바탕색은 Styled가 맞췄다.
        protected override void OnPresented(in UIAppliedTheme theme) => Ambient?.Apply(theme.ThemeId);

        // Gold 표기 설정. 증가 연출이 마지막으로 쓴 값을 새 표기로 다시 쓴다(연출 중이면 다음 프레임이 이어 쓴다).
        public void SetNumberFormat(NumberFormat format)
        {
            _numberFormat = format;
            _countUp.Rewrite();

            if (_earned != null && _shownReward.HasValue)
                _earned.text = Money(_earnedTarget);
        }

        private string Localized(string key, params object[] args) =>
            _localizer != null ? _localizer.Format(key, args) : key;

        #region 툴팁

        // 행 위 가운데에 붙인다. 툴팁은 레이캐스트를 받지 않아 행의 올림을 가로채지 않는다.
        private void ShowTooltip(Button row, string displayName)
        {
            if (_tooltip == null || row == null)
                return;

            _tooltipOwner = (RectTransform)row.transform;

            if (_tooltipText != null)
                _tooltipText.text = displayName;

            _tooltip.gameObject.SetActive(true);
            _tooltip.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltip);

            _tooltipOwner.GetWorldCorners(_corners);
            _tooltip.pivot = new Vector2(0.5f, 0);
            _tooltip.position = (_corners[1] + _corners[2]) * 0.5f;
            _tooltip.anchoredPosition += new Vector2(0, TooltipGap);
        }

        // 다른 행으로 막 옮겨 간 툴팁은 닫지 않는다.
        private void HideTooltip(Button row)
        {
            if (row != null && row.transform == _tooltipOwner)
                HideTooltip();
        }

        private void HideTooltip()
        {
            _tooltipOwner = null;

            if (_tooltip != null)
                _tooltip.gameObject.SetActive(false);
        }

        #endregion

        private static string Count(int count) =>
            "x" + count.ToString("N0", CultureInfo.InvariantCulture);

        private static string Whole(int count) => count.ToString(CultureInfo.InvariantCulture);

        private static string CountOf(long count) => Count((int)count);

        private string Money(long gold) =>
            "$" + NumberText.Of(gold, _numberFormat);

        // 성장도 막대나 숫자가 아직 올라가는 중인가.
        public bool IsAnimating => _stageFill.IsRunning || _countUp.IsRunning;

        private void Update()
        {
            // 연출 중에 화면 어디든 한 번 누르면(마우스·펜·터치) 바로 최종값으로 간다. 버튼·행 위를 눌러도 같다 — 자식이 사건을 먹어도 여기서는 입력 장치를 직접 본다.
            if (IsAnimating && Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                SkipAnimation();
                return;
            }

            _stageFill.Tick(Time.deltaTime);
            _countUp.Tick(Time.deltaTime);
            TickScaleEffect();
        }

        // 연출을 끝까지 돌린 것과 같은 상태로 만든다: 막대·숫자 다 최종값, 강조 효과 끔, 슬라이더 소리 멈추고 종료음. 연출 중이 아니면 아무것도 하지 않는다.
        public void SkipAnimation()
        {
            if (!IsAnimating)
                return;

            _stageFill.Finish();
            _countUp.Finish();

            if (_earned != null)
                _earned.text = Money(_earnedTarget);

            StopScaleEffect();
            Sounds?.StopSlider();
            Sounds?.PlayClosing();
        }

        // 막대가 다 찼다: 결과 색으로.
        private void HandleStageFillCompleted()
        {
            if (_stageFillImage != null)
                _stageFillImage.color = _stageReached ? StageReachedColor : StageShortColor;
        }

        // 숫자가 다 올랐다: 종료음.
        private void HandleCountUpCompleted() => Sounds?.PlayClosing();

        // 값이 바뀐 글자를 잠깐 키운다. 다른 글자가 커지는 중이었으면 그것은 원래 크기로.
        private void PlayScaleEffect(TMP_Text text)
        {
            StopScaleEffect();
            _scaleTarget = text;
            _scaleTimer = 0f;
        }

        private void StopScaleEffect()
        {
            if (_scaleTarget != null)
                _scaleTarget.transform.localScale = Vector3.one;

            _scaleTarget = null;
        }

        // 앞 절반은 HighlightScale까지 커지고 뒤 절반은 1로 돌아온다.
        private void TickScaleEffect()
        {
            if (_scaleTarget == null)
                return;

            _scaleTimer += Time.deltaTime;
            float t = _scaleTimer / ScaleDuration;
            float scale = t <= 0.5f
                ? Mathf.Lerp(1f, HighlightScale, t * 2f)
                : Mathf.Lerp(HighlightScale, 1f, (t - 0.5f) * 2f);
            _scaleTarget.transform.localScale = Vector3.one * scale;

            if (t >= 1f)
                StopScaleEffect();
        }
    }
}
