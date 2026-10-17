using System;
using NUnit.Framework;

namespace BlackHole.ContentExport.Tests
{
    // 캠페인을 언제 끝낼지(계약 3.8a, C40)의 규칙.
    public sealed class CampaignPolicyTests
    {
        [Test]
        public void KeepsGoingWhileMilestonesAreReached()
        {
            var policy = new CampaignPolicy(maxStage: 3, stallBattles: 5, battleLimit: 100, postClearBattles: 3);

            Assert.IsNull(policy.AfterBattle(1, reachedMilestone: true));
            Assert.IsNull(policy.AfterBattle(1, reachedMilestone: false));
            Assert.IsNull(policy.AfterBattle(2, reachedMilestone: true));
            Assert.IsFalse(policy.Cleared);
            Assert.AreEqual(3, policy.Battles);
        }

        [Test]
        public void ClearingRunsThePostClearBattlesThenStops()
        {
            var policy = new CampaignPolicy(maxStage: 2, stallBattles: 300, battleLimit: 100, postClearBattles: 3);

            Assert.IsNull(policy.AfterBattle(1, true));
            Assert.IsFalse(policy.NextIsPostClear);

            // 마지막 이정표에 닿은 판: 클리어. 이 판 다음부터 클리어 뒤 판이다.
            Assert.IsNull(policy.AfterBattle(2, true));
            Assert.IsTrue(policy.Cleared);
            Assert.IsTrue(policy.NextIsPostClear);

            Assert.IsNull(policy.AfterBattle(2, false));
            Assert.IsNull(policy.AfterBattle(2, false));
            Assert.AreEqual(CampaignStopReason.Cleared, policy.AfterBattle(2, false));

            Assert.AreEqual(3, policy.PostClearBattles);
            Assert.AreEqual(5, policy.Battles);
        }

        [Test]
        public void ClearingStopsAtOnceWithoutPostClearBattles()
        {
            var policy = new CampaignPolicy(maxStage: 1, stallBattles: 300, battleLimit: 100, postClearBattles: 0);

            Assert.AreEqual(CampaignStopReason.Cleared, policy.AfterBattle(1, true));
            Assert.AreEqual(0, policy.PostClearBattles);
        }

        [Test]
        public void StallsWhenNoMilestoneIsReachedForTheGivenBattles()
        {
            var policy = new CampaignPolicy(maxStage: 4, stallBattles: 3, battleLimit: 100, postClearBattles: 3);

            Assert.IsNull(policy.AfterBattle(1, false));
            Assert.IsNull(policy.AfterBattle(1, false));
            Assert.AreEqual(CampaignStopReason.Stalled, policy.AfterBattle(1, false));
        }

        [Test]
        public void ReachingAMilestoneResetsTheStallCount()
        {
            var policy = new CampaignPolicy(maxStage: 4, stallBattles: 3, battleLimit: 100, postClearBattles: 3);

            Assert.IsNull(policy.AfterBattle(0, false));
            Assert.IsNull(policy.AfterBattle(0, false));
            Assert.IsNull(policy.AfterBattle(1, true));
            Assert.IsNull(policy.AfterBattle(1, false));
            Assert.IsNull(policy.AfterBattle(1, false));
            Assert.AreEqual(CampaignStopReason.Stalled, policy.AfterBattle(1, false));
        }

        [Test]
        public void StallRuleCanBeTurnedOff()
        {
            var policy = new CampaignPolicy(maxStage: 4, stallBattles: 0, battleLimit: 100, postClearBattles: 3);

            for (int i = 0; i < 50; i++)
                Assert.IsNull(policy.AfterBattle(1, false));
        }

        [Test]
        public void StopsAtTheBattleLimit()
        {
            var policy = new CampaignPolicy(maxStage: 4, stallBattles: 0, battleLimit: 2, postClearBattles: 3);

            Assert.IsNull(policy.AfterBattle(1, true));
            Assert.AreEqual(CampaignStopReason.BattleLimit, policy.AfterBattle(1, true));
        }

        [Test]
        public void ClearedAtTheLimitStillReportsCleared()
        {
            // 클리어 뒤 마지막 판이 상한과 겹치면 클리어로 끝난다(상한보다 클리어가 먼저 판정된다).
            var policy = new CampaignPolicy(maxStage: 1, stallBattles: 0, battleLimit: 2, postClearBattles: 1);

            Assert.IsNull(policy.AfterBattle(1, true));
            Assert.AreEqual(CampaignStopReason.Cleared, policy.AfterBattle(1, false));
        }

        [Test]
        public void RejectsInvalidSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignPolicy(-1, 300, 100, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignPolicy(2, 300, 0, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignPolicy(2, 300, 100, -1));
        }
    }
}
