using System.Runtime.CompilerServices;

// 정의 검사(DefinitionGuard)처럼 Core 안에서만 쓰는 도구를 Core의 다른 시스템에 연다.
[assembly: InternalsVisibleTo("BlackHole.Core.Hq")]
[assembly: InternalsVisibleTo("BlackHole.Core.Enemies")]
[assembly: InternalsVisibleTo("BlackHole.Core.Battle")]
