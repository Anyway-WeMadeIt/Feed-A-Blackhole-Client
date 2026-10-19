using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 적 종류 하나의 저작 에셋: 규칙 수치, 외형. 종류(EnemyType)마다 에셋이 하나이고 적 종류 목록(EnemyCatalog)에 넣는다.
    // 움직임은 모든 종류가 HQ 공전이다(EnemyBehaviors).
    // 규칙 칸은 Core의 저작 형식(EnemyData)으로 옮겨져 EnemyContentLoader가 검증한다.
    // 외형 칸(스프라이트, 색 등급의 색)은 Core로 가지 않고 화면(EnemyView)만 읽는다. 규칙과 외형이 한 에셋에 있어 외형 연결이 빠지지 않는다.
    // 모든 칸은 이 에셋을 Inspector에서 직접 고친다.
    //
    // 종류는 계열(소행성·행성·별·은하, 픽업인 혜성)이고 색은 종류 안에 둔다.
    // - 반지름: 크기 1의 반지름. 모든 색이 같다.
    // - 색 등급: 같은 윤곽(스프라이트)에 색마다 색·HP·Gold·EXP가 다르다. 공급되는 종류는 6색(빨주노초파보), 픽업은 한 줄이다.
    // - 어떤 색이 나오는지는 그 종류의 질량(노드 asteroid.massScale 등, MassRule), 어떤 크기가 나오는지는 그 종류의 크기
    //   (노드 asteroid.size 등, SizeRule)가 정한다. 둘 다 종류마다 따로이고 이 에셋에는 칸이 없다.
    // - 특수 성질: 황금·전기·달·레이저·슈퍼노바·중력장은 종류가 아니라 출현 때 한 마리에 붙는 성질이다(최대 하나, 배타).
    //   성질 종류(EnemyTraitType)가 사망 효과를 정하고, 성질마다 효과 수치와 표식 색이 있다. 붙는 확률은 노드(electricAsteroid.spawnChance 등)가 정하고 기본 0%다.
    //   성질이 붙은 적(특수 적)도 사망 효과의 피해를 받는다(달·픽업 제외). 효과가 특수 적을 죽이면 연쇄한다(DeathEffects).
    // - 출현 주기: 0보다 크면 공급되지 않는 픽업(혜성)이다. 주기마다 등장 확률(노드 comet.spawnChance)로 나오고, 성질 하나가 언제나 붙는다.
    [CreateAssetMenu(fileName = "EnemyKind", menuName = "BlackHole/Enemy Kind")]
    public sealed class EnemyKind : ScriptableObject
    {
        [Serializable]
        public struct Trait
        {
            [Tooltip("성질 종류(종류 안에서 유일). 사망 효과를 정한다: Golden 황금, Electric 연쇄 번개, Moon 달 버프, Laser 레이저, Supernova 폭발, Comet 혜성 버프, Gravity 중력장.")]
            public EnemyTraitType type;
            [Tooltip("이 성질이 붙은 적의 표식 색. Core는 모른다.")]
            public Color color;
            [Tooltip("Golden: Gold 배율.")]
            public float multiplier;
            [Tooltip("Golden: 치명타 Gold 배율.")]
            public float critRewardScale;
            [Tooltip("Electric·Laser: 피해 · Supernova: 폭발 고정 피해.")]
            public float damage;
            [Tooltip("Electric: 번개가 한 번 옮겨 가는 최대 거리. Supernova: 폭발 반경(크기 1 기준, 죽은 별의 크기 배율만큼 넓어진다). Gravity: 끌어들이는 범위(은하 크기와 무관).")]
            public float radius;
            [Tooltip("Electric: 한 줄기가 옮겨 가는 최대 횟수.")]
            public int maxTargets;
            [Tooltip("Electric(별만): 확률과 무관하게 늘 나가는 줄기의 수(1 이상). 소행성은 늘 1이라 읽지 않는다.")]
            public int branches;
            [Tooltip("Electric: 줄기가 하나 더 나갈 확률(0 ~ 1).")]
            public float branchChance;
            [Tooltip("Electric·Laser·Golden: 치명타 확률(0 ~ 1).")]
            public float critChance;
            [Tooltip("Electric·Laser: 치명타일 때 피해 배율.")]
            public float critMultiplier;
            [Tooltip("Laser: 레이저 너비.")]
            public float width;
            [Tooltip("이 성질의 동시 상한: 살아 있는 그 성질 적 + Breaker에 남은 그 버프 중첩. 0이면 상한 없음. 다 찼으면 뽑혀도 붙지 않는다(원작 달 최대 개수, 노드 moonPlanet.maxCount).")]
            public int maxActive;
            [Tooltip("Gravity: 끌리는 속도(초당 거리). 범위 안의 적은 공전을 멈추고 이 속도로 중심에 다가가 뭉친다.")]
            public float force;
            [Tooltip("Gravity: 중력장 지속 시간(초). 끝나면 끌려간 자리에서 다시 공전한다.")]
            public float duration;
        }

        [Serializable]
        public struct RespawnStage
        {
            [Tooltip("이 이정표 단계(성장도)부터 적용한다. 0 = 처음부터, 2 = Lv20 이정표를 넘은 뒤의 판, 3 = Lv30 뒤.")]
            [Min(0)]
            public int stage;
            [Tooltip("이 단계부터의 재생성 세대 감쇠(0~1). n세대 적의 재생성 확률 = 노드 respawnChance × decay^n. 0이면 1세대까지만.")]
            [Range(0, 1)]
            public float decay;
            [Tooltip("최대 세대. 이 세대까지 나오고 더는 재생성하지 않는다. 0이면 제한 없음(감쇠만).")]
            [Min(0)]
            public int maxGeneration;
        }

        [Serializable]
        public struct Tier
        {
            [Tooltip("이 색 등급을 그리는 색. Core는 모른다.")]
            public Color color;
            public float maxHealth;
            [Tooltip("사망 때 판의 합계에 드는 Gold의 기본값(크기 1). 0 이상.")]
            public long gold;
            [Tooltip("사망 때 블랙홀에 드는 EXP. 0 이상. 크기는 곱하고, 황금은 곱하지 않는다.")]
            public long exp;
        }

        [Tooltip("적 종류. 적 종류 목록에는 종류마다 에셋 하나만 넣는다.")]
        [SerializeField] private EnemyType type;

        [Tooltip("초당 이동 거리. 모든 색 등급이 같다. 0이 아닌 값이고, 부호가 공전 방향이다(양수 = 반시계, 음수 = 시계방향).")]
        [SerializeField] private float moveSpeed = 1;

        [Tooltip("크기 1의 반지름. 모든 색이 같다. 공격 판정과 화면에 그리는 크기다. 크기 k는 1 + radiusStep × (k − 1)배.")]
        [SerializeField] private float radius = 0.25f;

        [Tooltip("크기가 1 오를 때 늘어나는 반지름(크기 1의 반지름 대비, 0 이상). 원작 실측: 소행성 0.35, 행성 0.09, 별 0.33. 픽업은 크기가 없다.")]
        [SerializeField] private float radiusStep = 0.5f;

        [Header("종류 사이")]
        [Tooltip("판 시작 때 시작 공급 중 변환 수(노드 asteroid.toPlanet·planet.toStar·star.toGalaxy, 마리 수)만큼 바뀌는 다음 종류. 비우면 변환하지 않는다. 적 종류 목록에 있어야 한다.")]
        [SerializeField] private EnemyKind upgradesTo;
        [Tooltip("0보다 크면 픽업(혜성): 공급되지 않고 이 주기(초)마다 등장 확률(노드 comet.spawnChance, %)로 나온다. 성질이 정확히 하나여야 한다.")]
        [SerializeField] private float spawnPeriod;
        [Tooltip("픽업: 혜성 비(노드 comet.rainChance)일 때 한꺼번에 나오는 수. 0이면 혜성 비가 없다.")]
        [SerializeField] private int rainCount;
        [Tooltip("파괴 때 시간 추가(노드 timeChance)가 성공하면 더하는 초. 0이면 판 설정(BattleRules.killTimeBonus)을 쓴다.")]
        [Min(0)]
        [SerializeField] private float killTimeBonus;
        [Tooltip("재생성 세대 감쇠(0~1). 재생성으로 나온 적이 죽을 때 재생성 확률(노드 respawnChance)에 세대마다 이 값을 곱한다. 0이면 1세대까지만(사슬 금지), 1이면 감쇠 없음.")]
        [Range(0, 1)]
        [SerializeField] private float respawnDecay;
        [Tooltip("이정표 단계별 재생성 규칙. 판을 시작할 때 성장도 이하에서 가장 높은 단계의 줄을 쓴다. 맞는 줄이 없으면 위의 respawnDecay(최대 세대 제한 없음). 단계는 겹치면 안 된다.")]
        [SerializeField] private List<RespawnStage> respawnStages = new List<RespawnStage>();
        [Tooltip("픽업: 혜성 비가 한 번 내린 뒤 다시 내릴 수 있을 때까지의 쿨타임(초). 그동안은 하나씩만 나온다. 0이면 없음.")]
        [Min(0)]
        [SerializeField] private float rainCooldown;
        [Tooltip("픽업: 판 시작 뒤 이 시간(초)까지는 혜성 비가 내리지 않는다. 0이면 없음.")]
        [Min(0)]
        [SerializeField] private float rainDelay;
        [Header("색 등급 (번호가 적의 색 등급, 빨주노초파보)")]
        [SerializeField] private List<Tier> tiers = new List<Tier>();

        [Header("특수 성질 (출현 때 최대 하나가 붙는다)")]
        [SerializeField] private List<Trait> traits = new List<Trait>();

        [Header("외형 (Core는 모른다)")]
        [Tooltip("모든 색 등급이 같은 윤곽을 쓴다. 비우면 임시 원으로 그린다.")]
        [SerializeField] private Sprite sprite;

        public EnemyType Type => type;
        public Sprite Sprite => sprite;

        // 색 등급의 색. 없는 번호는 흰색이다.
        public Color ColorOf(int tier) => tier >= 0 && tier < tiers.Count ? tiers[tier].color : Color.white;

        // 색 등급과 톤(Enemy.Tone)의 색. 톤 0은 색 등급 색 그대로. 톤 1부터는 그 색 등급 색에서 색상·채도·밝기만 정해진 폭으로 옮긴 톤온톤이다 —
        // 질량이 올라 나오는 색이 3개 아래로 줄면(파·보 → 보라만) 규칙이 마지막 색 등급을 톤으로 나눈다(MassRule.LastTierTones, Enemy.Tone).
        // 톤 1 = 조금 푸른 쪽, 톤 2 = 조금 붉은 쪽, 그 뒤는 번갈아 더 벌린다. 밝기는 바꾸지 않는다 — 피해 음영(UpdateShade)이 밝기로 드러나므로
        // 밝기가 다른 톤은 맞은 적처럼 보인다. 알파는 색 등급 색의 것. 에셋에 따로 넣는 값은 없다.
        public Color ColorOf(int tier, int tone)
        {
            Color baseColor = ColorOf(tier);

            if (tone <= 0)
                return baseColor;

            Color.RGBToHSV(baseColor, out float hue, out float saturation, out float value);
            int step = (tone + 1) / 2;
            float sign = tone % 2 == 1 ? -1f : 1f;

            Color color = Color.HSVToRGB(
                Mathf.Repeat(hue + sign * ToneHueStep * step / 360f, 1f),
                Mathf.Clamp01(saturation + sign * ToneSaturationStep * step),
                value);
            color.a = baseColor.a;
            return color;
        }

        // 톤 한 단계가 색 등급 색에서 옮기는 폭: 색상(도)과 채도. 같은 색으로 읽히되 나란히 놓으면 구분되는 정도 —
        // 마지막 색은 배경이고 새로 나오는 상위 종류의 색이 주목점이라 폭을 작게 둔다. 밝기는 피해 음영의 몫이라 건드리지 않는다.
        private const float ToneHueStep = 8f;
        private const float ToneSaturationStep = 0.04f;

        // 성질의 표식 색. 없는 성질은 투명이다.
        public Color TraitColorOf(EnemyTraitType trait)
        {
            for (int i = 0; i < traits.Count; i++)
            {
                if (traits[i].type == trait)
                    return traits[i].color;
            }

            return Color.clear;
        }

        internal EnemyData ToData()
        {
            var data = new EnemyData
            {
                Type = type,
                MoveSpeed = moveSpeed,
                Radius = radius,
                RadiusStep = radiusStep,
                UpgradesTo = upgradesTo != null ? upgradesTo.Type : (EnemyType?)null,
                SpawnPeriod = spawnPeriod,
                RainCount = rainCount,
                KillTimeBonus = killTimeBonus,
                RespawnDecay = respawnDecay,
                RainCooldown = rainCooldown,
                RainDelay = rainDelay,
            };

            foreach (Trait trait in traits)
            {
                data.Traits.Add(new EnemyTraitData
                {
                    Type = trait.type,
                    MaxActive = trait.maxActive,
                    Effect = new DeathEffectData
                    {
                        Multiplier = trait.multiplier,
                        CritRewardScale = trait.critRewardScale,
                        Damage = trait.damage,
                        Radius = trait.radius,
                        MaxTargets = trait.maxTargets,
                        Branches = trait.branches,
                        BranchChance = trait.branchChance,
                        CritChance = trait.critChance,
                        CritMultiplier = trait.critMultiplier,
                        Width = trait.width,
                        Force = trait.force,
                        Duration = trait.duration,
                    },
                });
            }

            foreach (RespawnStage stage in respawnStages)
                data.RespawnStages.Add(new RespawnStageData { Stage = stage.stage, Decay = stage.decay, MaxGeneration = stage.maxGeneration });

            foreach (Tier tier in tiers)
                data.Tiers.Add(new EnemyTierData { MaxHealth = tier.maxHealth, Gold = tier.gold, Exp = tier.exp });

            return data;
        }
    }
}
