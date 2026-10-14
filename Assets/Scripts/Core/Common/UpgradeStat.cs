namespace BlackHole.Core
{
    // 업그레이드 수치(UpgradeStats 시트의 StatId). 시트 이름의 점(.)으로 나뉜 부분마다 첫 글자를 대문자로 붙인 이름이다
    // (breaker.critChance → BreakerCritChance, NodeContentLoader). 수치는 전투 코드가 읽어야 효과가 있으므로 시트만으로는 늘지 않는다.
    // 시트에는 이 목록의 수치가 모두, 한 번씩 있어야 한다. 순서는 시트 순서다.
    public enum UpgradeStat
    {
        Timer, // timer · 세션 타이머
        GrowthTime, // growth.time · 블랙홀 성장 시 추가되는 시간
        GrowthAsteroids, // growth.asteroids · 블랙홀 성장 시 생성되는 소행성 수
        GrowthPlanets, // growth.planets · 블랙홀 성장 시 생성되는 행성 수
        GrowthStars, // growth.stars · 블랙홀 성장 시 생성되는 별 수
        BreakerDamage, // breaker.damage · 브레이커 피해
        BreakerRadius, // breaker.radius · 브레이커 범위
        BreakerSpeed, // breaker.speed · 브레이커 속도
        BreakerCritChance, // breaker.critChance · 브레이커 치명타 확률
        BreakerCritBonus, // breaker.critBonus · 브레이커 치명타 보너스
        BreakerPlanetBonusDamage, // breaker.planetBonusDamage · 행성에 대한 브레이커의 보너스 피해
        BreakerStarBonusDamage, // breaker.starBonusDamage · 별에 대한 브레이커의 보너스 대미지
        AsteroidCount, // asteroid.count · 소행성 수
        AsteroidSize, // asteroid.size · 소행성 크기
        AsteroidMassScale, // asteroid.massScale · 소행성 질량
        AsteroidTimeChance, // asteroid.timeChance · 소행성이 파괴될 때 시간이 추가될 확률
        AsteroidRespawnChance, // asteroid.respawnChance · 소행성이 파괴될 때 새로운 소행성을 생성할 확률
        AsteroidToPlanet, // asteroid.toPlanet · 소행성을 행성으로 업그레이드
        PlanetToStar, // planet.toStar · 행성을 별로 업그레이드
        ElectricAsteroidSpawnChance, // electricAsteroid.spawnChance · 전기 소행성 생성 확률
        ElectricAsteroidDamage, // electricAsteroid.damage · 전기 소행성 피해
        ElectricAsteroidCritChance, // electricAsteroid.critChance · 전기 소행성 치명타 확률
        ElectricAsteroidChain, // electricAsteroid.chain · 전기 소행성 최대 연쇄
        ElectricAsteroidSplitChance, // electricAsteroid.splitChance · 전기 소행성이 갈라질 확률
        ElectricAsteroidCritBonus, // electricAsteroid.critBonus · 전기 소행성 치명타 보너스
        GoldenAsteroidSpawnChance, // goldenAsteroid.spawnChance · 황금 소행성 생성 확률
        GoldenAsteroidRewardScale, // goldenAsteroid.rewardScale · 황금 소행성 보너스 돈 스케일
        GoldenAsteroidCritChance, // goldenAsteroid.critChance · 황금 소행성 치명타 확률
        GoldenAsteroidCritRewardScale, // goldenAsteroid.critRewardScale · 황금 소행성 보너스 치명타 돈 스케일
        MoonPlanetSpawnChance, // moonPlanet.spawnChance · 달 생성 확률
        MoonPlanetSpeedScale, // moonPlanet.speedScale · 브레이커 속도에 대한 달 버프 스케일
        MoonPlanetRangeScale, // moonPlanet.rangeScale · 브레이커 범위에 대한 달 버프 스케일
        MoonPlanetDuration, // moonPlanet.duration · 달 버프 지속시간
        MoonPlanetMaxCount, // moonPlanet.maxCount · 달 최대 개수
        PlanetSize, // planet.size · 행성 크기
        PlanetRespawnChance, // planet.respawnChance · 행성이 파괴될 때 새로운 행성을 생성할 확률
        PlanetMassScale, // planet.massScale · 행성 질량
        PlanetTimeChance, // planet.timeChance · 행성이 파괴될 때 시간이 추가될 확률
        CometSpawnChance, // comet.spawnChance · 치명타 혜성 생성 확률
        CometDuration, // comet.duration · 치명타 혜성 버프 지속시간
        CometCritBonus, // comet.critBonus · 치명타 혜성 보너스 치명타 대미지
        CometRainChance, // comet.rainChance · 혜성이 내릴 확률
        StarRespawnChance, // star.respawnChance · 별이 파괴될 때 새로운 별을 생성할 확률
        StarTimeChance, // star.timeChance · 별이 파괴될 때 시간이 추가될 확률
        StarSize, // star.size · 별 크기
        StarMassScale, // star.massScale · 별 질량
        ElectricStarSpawnChance, // electricStar.spawnChance · 전기 별 생성 확률
        ElectricStarDamage, // electricStar.damage · 전기 별 대미지
        ElectricStarCritChance, // electricStar.critChance · 전기 별 치명타 확률
        ElectricStarChain, // electricStar.chain · 전기 별 최대 연쇄
        ElectricStarSplitChance, // electricStar.splitChance · 전기 별 갈라질 확률
        ElectricStarCritBonus, // electricStar.critBonus · 전기 별 치명타 보너스
        LaserStarSpawnChance, // laserStar.spawnChance · 레이저 별 생성 확률
        LaserStarWidth, // laserStar.width · 레이저 너비
        LaserStarCritChance, // laserStar.critChance · 레이저 치명타 확률
        LaserStarDamage, // laserStar.damage · 레이저 대미지
        LaserStarCritBonus, // laserStar.critBonus · 레이저 치명타 보너스
        SupernovaStarRange, // supernovaStar.range · 슈퍼노바 범위
        SupernovaStarDamage, // supernovaStar.damage · 슈퍼노바 대미지(고정 피해. 원작은 현재체력 비례 supernovaStar.hpDamage)
        SupernovaStarSpawnChance, // supernovaStar.spawnChance · 슈퍼노바 별 생성 확률

        // 신규(원작에 없음): 은하, 중력장 은하.
        GrowthGalaxies, // growth.galaxies · 블랙홀 성장 시 생성되는 은하 수
        StarToGalaxy, // star.toGalaxy · 별을 은하로 업그레이드
        GalaxySize, // galaxy.size · 은하 크기
        GalaxyMassScale, // galaxy.massScale · 은하 질량
        GalaxyRespawnChance, // galaxy.respawnChance · 은하가 파괴될 때 새로운 은하를 생성할 확률
        GalaxyTimeChance, // galaxy.timeChance · 은하가 파괴될 때 시간이 추가될 확률
        BreakerGalaxyBonusDamage, // breaker.galaxyBonusDamage · 은하에 대한 브레이커의 보너스 피해
        GravityGalaxySpawnChance, // gravityGalaxy.spawnChance · 중력장 은하 생성 확률
        GravityGalaxyRadius, // gravityGalaxy.radius · 중력장 범위
        GravityGalaxyForce, // gravityGalaxy.force · 중력장이 적을 끌어당기는 힘
        GravityGalaxyDuration, // gravityGalaxy.duration · 중력장 지속 시간
    }
}
