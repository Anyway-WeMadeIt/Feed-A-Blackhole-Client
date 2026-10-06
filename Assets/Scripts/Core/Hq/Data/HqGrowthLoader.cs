using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // HqGrowthData(저작 형식) → HqGrowthDefinition(검증된 정의).
    // 데이터가 없으면 HqGrowthDefinition.None이다(블랙홀이 Level 0·성장도 0에 머문다).
    // 오류가 하나라도 있으면 null을 돌려주고, 모든 진단을 into에 더한다(부분 통과 금지). 진단 경로는 "Growth."로 시작한다.
    public static class HqGrowthLoader
    {
        public static HqGrowthDefinition Load(HqGrowthData item, List<ContentDiagnostic> into)
        {
            if (into == null)
                throw new ArgumentNullException(nameof(into));

            if (item == null)
                return HqGrowthDefinition.None;

            var milestones = new List<HqMilestone>();
            int errors = into.Count;

            for (int i = 0; item.Milestones != null && i < item.Milestones.Count; i++)
            {
                HqMilestoneData mark = item.Milestones[i];

                if (mark == null)
                {
                    into.Add(new ContentDiagnostic($"Growth.Milestones[{i}]", "데이터가 없다."));
                    continue;
                }

                try
                {
                    milestones.Add(new HqMilestone(mark.Level, mark.TargetGold, mark.FieldScale));
                }
                catch (ArgumentException error)
                {
                    into.Add(new ContentDiagnostic($"Growth.Milestones[{i}]", error.Message));
                }
            }

            if (into.Count > errors)
                return null;

            // Level 사다리의 오류는 LevelExp에, 이정표가 사다리 밖·순서가 틀린 것은 Milestones에 붙인다.
            try
            {
                return new HqGrowthDefinition(item.LevelExp ?? new List<long>(), milestones);
            }
            catch (ArgumentException error)
            {
                into.Add(new ContentDiagnostic(error.ParamName == "milestones" ? "Growth.Milestones" : "Growth.LevelExp", error.Message));
                return null;
            }
        }
    }
}
