using System;

namespace BlackHole.Core
{
    // 시간제 종료의 공유 정의.
    public sealed class TimeLimitDefinition
    {
        public float Duration { get; }

        // 적이 파괴될 때 시간 추가가 성공하면(판 구성의 TimeChance) 제한 시간에 더하는 초.
        public float KillTimeBonus { get; }

        // 한 판에 파괴로 더할 수 있는 시간의 합(초). 0이면 제한 없음. 재생성·시간 추가가 쌓이면 판이 끝나지 않으므로 둔다.
        public float MaxKillTimeBonus { get; }

        // 시간 추가가 성공한 뒤 다음 판정까지 기다리는 초. 0이면 매 사망마다 판정. 연쇄 처치가 시간을 몰아주지 않게 둔다.
        public float KillTimeCooldown { get; }

        public TimeLimitDefinition(float duration, float killTimeBonus = 0, float maxKillTimeBonus = 0, float killTimeCooldown = 0)
        {
            Duration = DefinitionGuard.Positive(duration, nameof(duration));

            if (float.IsNaN(killTimeBonus) || float.IsInfinity(killTimeBonus) || killTimeBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(killTimeBonus), "0 이상의 유한한 값이 필요하다.");

            if (float.IsNaN(maxKillTimeBonus) || float.IsInfinity(maxKillTimeBonus) || maxKillTimeBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(maxKillTimeBonus), "0 이상의 유한한 값이 필요하다(0 = 제한 없음).");

            if (float.IsNaN(killTimeCooldown) || float.IsInfinity(killTimeCooldown) || killTimeCooldown < 0)
                throw new ArgumentOutOfRangeException(nameof(killTimeCooldown), "0 이상의 유한한 값이 필요하다(0 = 쿨타임 없음).");

            KillTimeBonus = killTimeBonus;
            MaxKillTimeBonus = maxKillTimeBonus;
            KillTimeCooldown = killTimeCooldown;
        }

        // 산 노드의 수치 값으로 이 판의 제한 시간을 계산함.
        public TimeLimitDefinition Upgraded(UpgradeStatValues upgrades)
        {
            float duration = Duration + upgrades.GainOf(UpgradeStat.Timer);

            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(upgrades), $"제한 시간은 0보다 큰 유한한 값이어야 한다. 노드 반영 값: {duration}.");

            return new TimeLimitDefinition(duration, KillTimeBonus, MaxKillTimeBonus, KillTimeCooldown);
        }
    }
}
