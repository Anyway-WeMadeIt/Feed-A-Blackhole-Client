using System;

namespace BlackHole.ContentExport
{
    // 캠페인(빈 진행도부터 [판 → 결산 → 구매]를 이어 돌리는 것)을 언제 끝낼지 정한다(계약 3.8a).
    // 판을 돌리지 않고, 판 하나가 끝날 때마다 결과를 받아 끝낼지 답한다.
    //
    // 끝나는 조건:
    // - 마지막 이정표에 닿으면 클리어 뒤 판을 postClearBattles판 더 돌리고 끝낸다(Cleared). 마지막 이정표 이후의 서버 분기가 골든 로그에 담긴다.
    // - 이정표에 닿지 못한 채 stallBattles판이 이어지면 막힌 것으로 끝낸다(Stalled). 마지막 이정표를 못 넘는 봇은 Gold만 쌓아 long을 넘기 때문이다.
    // - 판 수가 battleLimit에 닿으면 끝낸다(BattleLimit).
    // Overflow, Capped는 판을 돌린 쪽이 알아채므로 여기서 다루지 않는다.
    public sealed class CampaignPolicy
    {
        private readonly int _maxStage;
        private readonly int _stallBattles;
        private readonly int _battleLimit;
        private readonly int _postClearBattles;
        private int _sinceMilestone;

        // 지금까지 끝낸 판 수.
        public int Battles { get; private set; }

        // 마지막 이정표에 닿았는가(그 판을 끝낸 뒤부터 true).
        public bool Cleared { get; private set; }

        // 클리어 뒤에 끝낸 판 수.
        public int PostClearBattles { get; private set; }

        // 다음 판이 클리어 뒤 판인가. 클리어 뒤 판은 결산까지만 하고 구매하지 않는다.
        public bool NextIsPostClear => Cleared;

        // stallBattles가 0 이하면 막힘 규칙을 쓰지 않는다.
        public CampaignPolicy(int maxStage, int stallBattles, int battleLimit, int postClearBattles)
        {
            if (maxStage < 0) throw new ArgumentOutOfRangeException(nameof(maxStage));
            if (battleLimit < 1) throw new ArgumentOutOfRangeException(nameof(battleLimit), "판 수 상한은 1 이상이다.");
            if (postClearBattles < 0) throw new ArgumentOutOfRangeException(nameof(postClearBattles));

            _maxStage = maxStage;
            _stallBattles = stallBattles;
            _battleLimit = battleLimit;
            _postClearBattles = postClearBattles;
        }

        // 판 하나가 끝나고 결산까지 마쳤을 때 부른다. growthStage는 결산 뒤 성장도, reachedMilestone은 이 판이 이정표에 닿았는가.
        // 끝내야 하면 그 이유를, 계속하면 null을 돌려준다.
        public CampaignStopReason? AfterBattle(int growthStage, bool reachedMilestone)
        {
            Battles++;

            if (Cleared)
            {
                PostClearBattles++;

                if (PostClearBattles >= _postClearBattles)
                    return CampaignStopReason.Cleared;
            }
            else if (growthStage >= _maxStage)
            {
                Cleared = true;

                if (_postClearBattles == 0)
                    return CampaignStopReason.Cleared;
            }
            else
            {
                _sinceMilestone = reachedMilestone ? 0 : _sinceMilestone + 1;

                if (_stallBattles > 0 && _sinceMilestone >= _stallBattles)
                    return CampaignStopReason.Stalled;
            }

            return Battles >= _battleLimit ? CampaignStopReason.BattleLimit : (CampaignStopReason?)null;
        }
    }
}
