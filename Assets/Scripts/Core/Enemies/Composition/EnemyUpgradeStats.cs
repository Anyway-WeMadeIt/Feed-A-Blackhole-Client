namespace BlackHole.Core
{
    // 적 종류·성질마다 판 구성을 바꾸는 업그레이드 수치(UpgradeStat). 그 종류·성질에 수치가 없으면 null이다(노드가 닿지 않는다).
    // 판 구성(EnemyComposition)은 적 종류 에셋의 기본값에 그 수치의 늘어난 양(UpgradeStatValues.GainOf)을 더한다.
    public static class EnemyUpgradeStats
    {
        // 질량(%). 시트 값이 곧 전투 질량이다(MassRule) — 늘어난 양이 아니라 값 자체를 쓴다. 픽업은 질량이 없다.
        public static UpgradeStat? Mass(EnemyType kind) => kind switch
        {
            EnemyType.Asteroid => UpgradeStat.AsteroidMassScale,
            EnemyType.Planet => UpgradeStat.PlanetMassScale,
            EnemyType.Star => UpgradeStat.StarMassScale,
            EnemyType.Galaxy => UpgradeStat.GalaxyMassScale,
            _ => null,
        };

        // 크기(기본 1, 상한 SizeRule.Max). 크기 1부터 이 값까지가 같은 몫으로 섞여 나온다(SizeRule).
        public static UpgradeStat? Size(EnemyType kind) => kind switch
        {
            EnemyType.Asteroid => UpgradeStat.AsteroidSize,
            EnemyType.Planet => UpgradeStat.PlanetSize,
            EnemyType.Star => UpgradeStat.StarSize,
            EnemyType.Galaxy => UpgradeStat.GalaxySize,
            _ => null,
        };

        // 전투 시작 공급에 더하는 수(마리). 원작 도전 과제 "50개 이상으로 시작"이 이 강화를 가리킨다.
        public static UpgradeStat? StartSupply(EnemyType kind) => kind switch
        {
            EnemyType.Asteroid => UpgradeStat.AsteroidCount,
            _ => null,
        };

        // 블랙홀이 Level업할 때마다 이 종류를 판 시작 수의 몇 % 요청하는가(원작 "블랙홀 성장 시 생성되는 소행성 수"). %.
        // 시작 수는 변환까지 반영한 이 판의 전투 시작 공급 수다. 시작 수가 0인 종류는 나오지 않는다.
        public static UpgradeStat? GrowthSupply(EnemyType kind) => kind switch
        {
            EnemyType.Asteroid => UpgradeStat.GrowthAsteroids,
            EnemyType.Planet => UpgradeStat.GrowthPlanets,
            EnemyType.Star => UpgradeStat.GrowthStars,
            EnemyType.Galaxy => UpgradeStat.GrowthGalaxies,
            _ => null,
        };

        // 판 시작 때 이 종류의 시작 공급 중 몇 마리를 다음 종류로 바꾸는가(원작 "소행성을 행성으로 업그레이드"). 마리 수.
        // 시작 공급보다 많으면 시작 공급만큼만 바뀐다. 사슬 앞쪽부터 바꾼다(소행성 → 행성을 먼저, 그다음 행성 → 별).
        public static UpgradeStat? Upgrade(EnemyType kind) => kind switch
        {
            EnemyType.Asteroid => UpgradeStat.AsteroidToPlanet,
            EnemyType.Planet => UpgradeStat.PlanetToStar,
            EnemyType.Star => UpgradeStat.StarToGalaxy,
            _ => null,
        };

        // 주기 출현 종류(혜성)의 등장 확률: 출현 주기마다 이 확률로 하나가 나온다. %, 기본 0 — 노드를 사야 나온다.
        public static UpgradeStat? SpawnChance(EnemyType kind) => kind switch
        {
            EnemyType.Comet => UpgradeStat.CometSpawnChance,
            _ => null,
        };

        // 픽업(혜성)이 나올 때 혜성 비가 될 확률(원작 "혜성이 내릴 확률"). 혜성 비면 종류의 혜성 비 수(EnemyDefinition.RainCount)만큼 나온다. %.
        public static UpgradeStat? RainChance(EnemyType kind) => kind switch
        {
            EnemyType.Comet => UpgradeStat.CometRainChance,
            _ => null,
        };

        // 이 종류가 파괴될 때 같은 종류를 하나 새로 요청할 확률(원작 "행성이 파괴될 때 새로운 행성을 생성할 확률"). %.
        public static UpgradeStat? RespawnChance(EnemyType kind) => kind switch
        {
            EnemyType.Asteroid => UpgradeStat.AsteroidRespawnChance,
            EnemyType.Planet => UpgradeStat.PlanetRespawnChance,
            EnemyType.Star => UpgradeStat.StarRespawnChance,
            EnemyType.Galaxy => UpgradeStat.GalaxyRespawnChance,
            _ => null,
        };

        // 이 종류가 파괴될 때 판의 제한 시간이 늘어날 확률(원작 "행성이 파괴될 때 시간이 추가될 확률"). 늘어나는 초는 TimeLimitDefinition.KillTimeBonus. %.
        public static UpgradeStat? TimeChance(EnemyType kind) => kind switch
        {
            EnemyType.Asteroid => UpgradeStat.AsteroidTimeChance,
            EnemyType.Planet => UpgradeStat.PlanetTimeChance,
            EnemyType.Star => UpgradeStat.StarTimeChance,
            EnemyType.Galaxy => UpgradeStat.GalaxyTimeChance,
            _ => null,
        };

        // 특수 성질의 생성 확률(원작 "전기 소행성 생성 확률" 등). %, 기본 0 — 노드를 사야 붙는다. 픽업의 성질은 언제나 붙어 확률이 없다.
        public static UpgradeStat? TraitChance(EnemyType kind, EnemyTraitType trait) => (kind, trait) switch
        {
            (EnemyType.Asteroid, EnemyTraitType.Golden) => UpgradeStat.GoldenAsteroidSpawnChance,
            (EnemyType.Asteroid, EnemyTraitType.Electric) => UpgradeStat.ElectricAsteroidSpawnChance,
            (EnemyType.Planet, EnemyTraitType.Moon) => UpgradeStat.MoonPlanetSpawnChance,
            (EnemyType.Star, EnemyTraitType.Electric) => UpgradeStat.ElectricStarSpawnChance,
            (EnemyType.Star, EnemyTraitType.Laser) => UpgradeStat.LaserStarSpawnChance,
            (EnemyType.Star, EnemyTraitType.Supernova) => UpgradeStat.SupernovaStarSpawnChance,
            (EnemyType.Galaxy, EnemyTraitType.Gravity) => UpgradeStat.GravityGalaxySpawnChance,
            _ => null,
        };

        // 이 성질의 동시 상한: 살아 있는 그 성질 적 + Breaker에 남은 그 버프 중첩(원작 "달 최대 개수").
        public static UpgradeStat? TraitMaxActive(EnemyType kind, EnemyTraitType trait) => (kind, trait) switch
        {
            (EnemyType.Planet, EnemyTraitType.Moon) => UpgradeStat.MoonPlanetMaxCount,
            _ => null,
        };

        // 황금 Gold 배율. 늘어난 %p ÷ 100을 배율에 더한다(시트 50% → 60250%면 ×50에 +602).
        public static UpgradeStat? TraitMultiplier(EnemyType kind, EnemyTraitType trait) => (kind, trait) switch
        {
            (EnemyType.Asteroid, EnemyTraitType.Golden) => UpgradeStat.GoldenAsteroidRewardScale,
            _ => null,
        };

        // 황금 치명타 Gold 배율(1 = 기본 Gold의 100%를 더 얹음 = 시트 100%). %p ÷ 100을 더한다.
        public static UpgradeStat? TraitCritRewardScale(EnemyType kind, EnemyTraitType trait) => (kind, trait) switch
        {
            (EnemyType.Asteroid, EnemyTraitType.Golden) => UpgradeStat.GoldenAsteroidCritRewardScale,
            _ => null,
        };

        // 피해(번개·레이저·슈퍼노바 폭발). 더한다. 슈퍼노바도 고정 피해다.
        public static UpgradeStat? TraitDamage(EnemyType kind, EnemyTraitType trait) => (kind, trait) switch
        {
            (EnemyType.Asteroid, EnemyTraitType.Electric) => UpgradeStat.ElectricAsteroidDamage,
            (EnemyType.Star, EnemyTraitType.Electric) => UpgradeStat.ElectricStarDamage,
            (EnemyType.Star, EnemyTraitType.Laser) => UpgradeStat.LaserStarDamage,
            (EnemyType.Star, EnemyTraitType.Supernova) => UpgradeStat.SupernovaStarDamage,
            _ => null,
        };

        // 치명타 확률(황금·번개·레이저, 0 ~ 1, 1을 넘지 않는다). %p ÷ 100을 더한다.
        public static UpgradeStat? TraitCritChance(EnemyType kind, EnemyTraitType trait) => (kind, trait) switch
        {
            (EnemyType.Asteroid, EnemyTraitType.Golden) => UpgradeStat.GoldenAsteroidCritChance,
            (EnemyType.Asteroid, EnemyTraitType.Electric) => UpgradeStat.ElectricAsteroidCritChance,
            (EnemyType.Star, EnemyTraitType.Electric) => UpgradeStat.ElectricStarCritChance,
            (EnemyType.Star, EnemyTraitType.Laser) => UpgradeStat.LaserStarCritChance,
            _ => null,
        };

        // 치명타 피해 배율(번개·레이저, 2 = ×2 = 시트 200%). %p ÷ 100을 더한다.
        public static UpgradeStat? TraitCritMultiplier(EnemyType kind, EnemyTraitType trait) => (kind, trait) switch
        {
            (EnemyType.Asteroid, EnemyTraitType.Electric) => UpgradeStat.ElectricAsteroidCritBonus,
            (EnemyType.Star, EnemyTraitType.Electric) => UpgradeStat.ElectricStarCritBonus,
            (EnemyType.Star, EnemyTraitType.Laser) => UpgradeStat.LaserStarCritBonus,
            _ => null,
        };

        // 번개 한 줄기가 옮겨 가는 최대 횟수(원작 "최대 연쇄"). 더한다.
        public static UpgradeStat? TraitMaxTargets(EnemyType kind, EnemyTraitType trait) => (kind, trait) switch
        {
            (EnemyType.Asteroid, EnemyTraitType.Electric) => UpgradeStat.ElectricAsteroidChain,
            (EnemyType.Star, EnemyTraitType.Electric) => UpgradeStat.ElectricStarChain,
            _ => null,
        };

        // 번개 줄기가 하나 더 나갈 확률(원작 "갈라질 확률", 0 ~ 1, 1을 넘지 않는다). %p ÷ 100을 더한다.
        public static UpgradeStat? TraitBranchChance(EnemyType kind, EnemyTraitType trait) => (kind, trait) switch
        {
            (EnemyType.Asteroid, EnemyTraitType.Electric) => UpgradeStat.ElectricAsteroidSplitChance,
            (EnemyType.Star, EnemyTraitType.Electric) => UpgradeStat.ElectricStarSplitChance,
            _ => null,
        };

        // 레이저 너비(시트 기본 100%). (1 + 늘어난 %p ÷ 100)배.
        public static UpgradeStat? TraitWidth(EnemyType kind, EnemyTraitType trait) => (kind, trait) switch
        {
            (EnemyType.Star, EnemyTraitType.Laser) => UpgradeStat.LaserStarWidth,
            _ => null,
        };

        // 폭발·중력장 반지름(시트 기본 100%). (1 + 늘어난 %p ÷ 100)배. 번개가 옮겨 가는 거리에는 수치가 없다.
        public static UpgradeStat? TraitRadius(EnemyType kind, EnemyTraitType trait) => (kind, trait) switch
        {
            (EnemyType.Star, EnemyTraitType.Supernova) => UpgradeStat.SupernovaStarRange,
            (EnemyType.Galaxy, EnemyTraitType.Gravity) => UpgradeStat.GravityGalaxyRadius,
            _ => null,
        };

        // 중력장이 끌어당기는 힘(끌리는 속도, 시트 기본 100%). (1 + 늘어난 %p ÷ 100)배.
        public static UpgradeStat? TraitForce(EnemyType kind, EnemyTraitType trait) => (kind, trait) switch
        {
            (EnemyType.Galaxy, EnemyTraitType.Gravity) => UpgradeStat.GravityGalaxyForce,
            _ => null,
        };

        // 중력장 지속 시간(초). 더한다.
        public static UpgradeStat? TraitDuration(EnemyType kind, EnemyTraitType trait) => (kind, trait) switch
        {
            (EnemyType.Galaxy, EnemyTraitType.Gravity) => UpgradeStat.GravityGalaxyDuration,
            _ => null,
        };

    }
}
