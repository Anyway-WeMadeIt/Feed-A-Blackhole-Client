using System;

namespace BlackHole.Core
{
    // 사망이 확정된 뒤 따라오는 효과 판정.
    internal sealed class DeathBonuses
    {
        private readonly EnemyStatTable _stats;
        private readonly EnemySupply _supply;
        private readonly BattleRandom _respawnRandom;
        private readonly BattleRandom _timeBonusRandom;

        // 시간 추가가 성공한 뒤 다음 판정까지 기다리는 초(판 설정 KillTimeCooldown). 0이면 매 사망마다 판정.
        private readonly float _timeBonusCooldown;
        private float _timeBonusCooldownLeft;

        // 판 설정의 처치 추가 시간(초). 종류에 자기 값(EnemyDefinition.KillTimeBonus)이 있으면 그것을 쓴다.
        private readonly float _killTimeBonus;

        private float _timeBonusSeconds; // 아직 판(GameSession)이 가져가지 않은 시간 추가 초.
        // 이 판의 성장도. 종류의 재생성 규칙(EnemyDefinition.RespawnRuleAt)을 고른다. 판 안에서는 바뀌지 않는다(이정표에 닿으면 판이 끝난다).
        private readonly int _stage;

        // 이 판의 시간 추가 성공 수. 쿨타임으로 판정을 건너뛴 경우와 수거형은 세지 않는다.
        // 결산 로그의 timeBonusCount
        internal int TimeBonusCount { get; private set; }

        internal DeathBonuses(int seed, EnemyStatTable stats, EnemySupply supply, float killTimeBonus = 0, float timeBonusCooldown = 0, int stage = 0)
        {
            _stats = stats;
            _supply = supply;
            _respawnRandom = new BattleRandom(seed, RandomStream.Respawn);
            _timeBonusRandom = new BattleRandom(seed, RandomStream.TimeBonus);
            _killTimeBonus = killTimeBonus;
            _timeBonusCooldown = timeBonusCooldown;
            _stage = stage;
        }

        // 판 시간이 delta만큼 흘렀다. 시간 추가 쿨타임을 줄인다.
        internal void Advance(float delta)
        {
            if (_timeBonusCooldownLeft > 0)
                _timeBonusCooldownLeft = Math.Max(0, _timeBonusCooldownLeft - delta);
        }

        internal void Roll(Enemy dead)
        {
            EnemyDefinition kind = dead.Definition;

            if (kind.IsPickup)
                return;

            EnemyComposition composition = _stats.CompositionOf(kind);

            // 재생성 확률은 세대마다 감쇠를 곱해 줄인다: n세대의 확률 = RespawnChance × decay^n. 감쇠·최대 세대는 이 판 성장도의 규칙(EnemyDefinition.RespawnRuleAt).
            // decay 0이면 1세대까지만 — 사슬이면 처치 수가 1/(1-확률)배로 불어난다. 0 < decay < 1이면 사슬이 남되 원래 한 마리당 기대 처치 수는
            // Σ p^n × decay^(n(n-1)/2)로 유한하다(p 100%: decay 0.5 → 약 2.64, 0.7 → 약 3.19). 최대 세대가 있으면 그 세대에서 끊긴다.
            RespawnRule rule = kind.RespawnRuleAt(_stage);
            float respawnChance = RespawnChanceAt(composition.RespawnChance, rule.Decay, dead.RespawnGeneration, rule.MaxGeneration);

            if (respawnChance > 0 && _respawnRandom.Roll(respawnChance))
                _supply.Request(new SupplyRequest(kind, 1, respawnGeneration: dead.RespawnGeneration + 1));

            // 쿨타임 중이면 판정 자체를 건너뛴다(난수도 쓰지 않는다). 연쇄 처치가 시간을 몰아주지 않게.
            if (_timeBonusCooldownLeft > 0)
                return;

            if (_timeBonusRandom.Roll(composition.TimeChance))
            {
                _timeBonusSeconds += kind.KillTimeBonus > 0 ? kind.KillTimeBonus : _killTimeBonus;
                _timeBonusCooldownLeft = _timeBonusCooldown;
                TimeBonusCount++;
            }
        }

        // n세대 적의 재생성 확률. 0세대는 그대로, 그 뒤는 세대마다 decay를 곱한다(decay 0이면 0).
        // maxGeneration세대(0이면 제한 없음)의 적은 더는 재생성하지 않는다.
        internal static float RespawnChanceAt(float chance, float decay, int generation, int maxGeneration = 0)
        {
            if (maxGeneration > 0 && generation >= maxGeneration)
                return 0;

            if (generation <= 0)
                return chance;

            if (decay <= 0)
                return 0;

            return chance * (float)Math.Pow(decay, generation);
        }

        // 모아 둔 시간 추가 초를 넘기고 0으로 되돌린다.
        internal float TakeTimeBonusSeconds()
        {
            float taken = _timeBonusSeconds;
            _timeBonusSeconds = 0;
            return taken;
        }
    }
}
