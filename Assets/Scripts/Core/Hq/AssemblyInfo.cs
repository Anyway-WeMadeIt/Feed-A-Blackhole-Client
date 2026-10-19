using System.Runtime.CompilerServices;

// 판 진행(World·GameSession)만 바꾸는 연산을 Battle에 연다.
[assembly: InternalsVisibleTo("BlackHole.Core.Battle")]

// 서버용 기대값 표(RulesTableBuilder)가 레벨 계산(Hq)을 실제 코드로 돌려 읽는다.
[assembly: InternalsVisibleTo("BlackHole.ContentExport")]
