using System.Globalization;

namespace BlackHole.Unity
{
    // 코드가 쓰는 UI 글자 표(Assets/Data/Localization/UIText.csv)의 키. 키 글자는 여기에만 둔다.
    // 고정 키는 상수, 번호·ID가 들어가는 키는 함수다. 상수는 에디터 검사(BlackHole/Localization/Check UI Text Table)가 표에 있는지 확인한다.
    // 프리팹 글자의 키({화면 타입}.{Ref 이름})는 ViewTexts가 만들고, 서버 알림 키는 Net(ConnectionNoticeKeys·RejectionKinds)이 가진다.
    internal static class UIText
    {
        public static class Mode
        {
            public const string NormalTitle = "mode.normal.title";
            public const string NormalDescription = "mode.normal.description";
        }

        public static class Confirm
        {
            public const string NewGameTitle = "confirm.newGame.title";
            public const string NewGameBody = "confirm.newGame.body";
            public const string NewGameConfirm = "confirm.newGame.confirm";
            public const string Cancel = "confirm.cancel";
        }

        public static class Battle
        {
            public const string Earned = "battle.earned";
            public const string Pause = "battle.pause";
            public const string Resume = "battle.resume";
            // 은하 이름의 대체 글자(색 등급 키가 표에 없을 때).
            public const string GalaxyFallback = "battle.galaxy";

            // 은하 이름(색 등급).
            public static string Galaxy(int tier) => "battle.galaxy." + Number(tier);
        }

        public static class Settlement
        {
            public const string ResultMilestone = "settlement.result.milestone";
            public const string ResultOver = "settlement.result.over";
            public const string Milestone = "settlement.milestone";
            public const string MilestoneReached = "settlement.milestone.reached";
            public const string MilestoneLast = "settlement.milestone.last";
            public const string Reward = "settlement.reward";
            public const string Total = "settlement.total";
            public const string Upgrade = "settlement.upgrade";
            public const string UpgradeCount = "settlement.upgrade.count";
            public const string RowAsteroid = "settlement.row.asteroid";
            public const string RowPlanet = "settlement.row.planet";
            public const string RowStar = "settlement.row.star";

            public static class Stat
            {
                public const string ElectricStarDamage = "settlement.stat.electricStarDamage";
                public const string LaserDamage = "settlement.stat.laserDamage";
                public const string SupernovaDamage = "settlement.stat.supernovaDamage";
                public const string GoldenAsteroidGold = "settlement.stat.goldenAsteroidGold";
                public const string ElectricAsteroidDamage = "settlement.stat.electricAsteroidDamage";
                public const string BreakerDamage = "settlement.stat.breakerDamage";
                public const string BreakerCritDamage = "settlement.stat.breakerCritDamage";
                public const string BreakerClicks = "settlement.stat.breakerClicks";
                public const string DestroyedStars = "settlement.stat.destroyedStars";
                public const string DestroyedPlanets = "settlement.stat.destroyedPlanets";
                public const string CollectedMoons = "settlement.stat.collectedMoons";
                public const string CollectedComets = "settlement.stat.collectedComets";
                public const string DestroyedAsteroids = "settlement.stat.destroyedAsteroids";
                public const string AddedTime = "settlement.stat.addedTime";
            }
        }

        public static class Milestone
        {
            public const string TutorialName = "milestone.tutorial.name";

            // 이정표(성장도) 이름.
            public static string Name(int stage) => "milestone." + Number(stage) + ".name";
        }

        public static class Upgrade
        {
            public const string Gold = "upgrade.gold";
            public const string HqGoal = "upgrade.hq.goal";
            public const string HqLast = "upgrade.hq.last";
        }

        public static class Node
        {
            public const string Rank = "node.rank";
            public const string Now = "node.now";
            public const string Maxed = "node.maxed";
            public const string Locked = "node.locked";
            public const string Buy = "node.buy";
            public const string Cost = "node.cost";
            public const string Short = "node.short";

            // 노드 트리 구역 이름.
            public static string Region(string key) => "node.region." + key;
        }

        public static class Stat
        {
            // 업그레이드 수치의 이름과 설명(시트의 StatId, 예: "timer", "breaker.radius").
            public static string Name(string statId) => "stat." + statId + ".name";
            public static string Description(string statId) => "stat." + statId + ".desc";
        }

        public static class Setting
        {
            // 설정 행의 이름(GameSettings 정의 ID).
            public static string Label(string settingId) => "setting." + settingId;

            // 선택형 설정의 선택지 글자.
            public static string Option(string settingId, int index) => "setting." + settingId + "." + Number(index);
        }

        public static class Ending
        {
            public const string CreditsTitle = "ending.credits.title";
            public const string Thanks = "ending.thanks";
            public const string Prompt = "ending.prompt";

            // 엔딩 문구 줄(1부터, 번호가 끊길 때까지).
            public static string Line(int index) => "ending.line." + Number(index);
            // 크레딧 묶음(1부터)의 역할과 이름 칸.
            public static string CreditRole(int index) => "ending.credits." + Number(index) + ".role";
            public static string CreditNames(int index) => "ending.credits." + Number(index) + ".names";
        }

        public static class Sync
        {
            // "{0}: {1}" 꼴의 거부 알림. 0은 거부 종류 글자(RejectionKinds.TextKeyOf), 1은 서버 코드.
            public const string Rejection = "sync.rejection";
        }

        public static class Tutorial
        {
            public const string HintHold = "tutorial.hint.hold";
        }

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
