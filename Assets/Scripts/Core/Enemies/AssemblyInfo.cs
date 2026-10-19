using System.Runtime.CompilerServices;

// 판 진행(World·GameSession)만 바꾸는 연산을 Battle에 연다.
[assembly: InternalsVisibleTo("BlackHole.Core.Battle")]
[assembly: InternalsVisibleTo("BlackHole.Core.Nodes")]

// 서버용 기대값 표(RulesTableBuilder)가 황금 치명 보너스 식(DeathRewards.CritBonusOf)을 그대로 부른다.
[assembly: InternalsVisibleTo("BlackHole.ContentExport")]
