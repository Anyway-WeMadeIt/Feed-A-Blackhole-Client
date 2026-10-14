using System.Runtime.CompilerServices;

// 진행 상태의 노드 구매(ProgressState.BuyRank)를 노드 트리에 연다. 구매 규칙은 NodePurchase가 가진다.
[assembly: InternalsVisibleTo("BlackHole.Core.Nodes")]

// 동기화 로그 생성 테스트가 판 결과(BattleRawData)를 직접 만든다.
[assembly: InternalsVisibleTo("BlackHole.Net.Tests")]
