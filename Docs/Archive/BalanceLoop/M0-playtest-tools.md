> 과거 M0~M6 구현 기록입니다. 현재 기능과 사용법은 [QA 안내](../../QA/README.md)를 참고하세요.

# M0 공용 테스트 도구

- 상태: 완료 (Unity 손 확인 일부 남음)
- 브랜치: `feat/playtestTools` (← `feat/analytics` 1695da9)
- 커밋: 66421d1 판 조작 훅 · 702708e 프로필·시나리오 · c085c0a 개발 패널·HUD·메모 · 7a5c816 예시 JSON·PlaytestLibrary

## 목표

값만 바꾼 버전(밸런스 프로필)과 원하는 시점(시나리오)으로 빠르게 플레이하고, 판을 조작하고, 숫자를 보며 느낌을 적는다. 조작한 판은 통계에서 구분한다.

## 만든 것

| 부분 | 내용 | 파일 |
| --- | --- | --- |
| 판 조작 훅 | 적 소환(종류·등급·성질·크기)·치우기·이동 배율, EXP·Level, 시간 추가·고정. 더한 시간은 통계에 세지 않는다 | `Core/Battle/BattleCheats.cs` 외 Core 4곳 |
| 밸런스 프로필 | 경로·값 패치 묶음. 로더 앞에서 적용해 원래 검증을 거친다. 틀리면 아무것도 바꾸지 않는다 | `Playtest/BalanceProfile*.cs`, `GameContentLoader` |
| 시나리오 | 성장도·Gold·노드(자동 구매)·시작 Level·시드·시간 고정 | `Playtest/PlaytestScenario.cs`, `UI/Flow/ScreenFlow.Playtest.cs` |
| 저장 보호 | 프로필별 저장 폴더, 시나리오 실행은 저장 안 함 | `PlaytestSession`, `ProgressStore` |
| 개발 패널 | F1·세 손가락. 시나리오·프로필·판·적·메모 탭, HUD | `Playtest/PlaytestPanel.cs`, `PlaytestHud.cs`, `PlaytestNotes.cs` |
| 통계 표시 | contentVersion = 프로필 이름, 조작·시나리오 판은 `+test` | `ContentTag`, `BattleAnalytics` |
| 예시 | 프로필 3개, 시나리오 5개, PlaytestLibrary | `Assets/Playtest/` |

## 검증

- 개발·릴리스·에디터 컴파일 오류 0. 커밋 2·3은 그 시점 트리로 따로 컴파일했다.
- 헤드리스 테스트 107개 통과(경로 전부, 원자성, 시나리오 5개, 판 조작, 같은 시드 재현, contentVersion).

## 남은 일

- [x] GameScene의 GameBootstrap Playtest 칸에 PlaytestLibrary 연결(장면 한 줄 커밋, 209a23e). 연결 전에도 에디터는 폴더를 바로 읽어 동작한다. 개발 빌드는 연결해야 예시가 들어간다.
- [ ] Unity 손 확인: F1 패널, 시나리오 시작, 프로필 바꿔 다시 시작, 적 탭, 메모 저장, DB의 content_version
  - 10-10 사용자: F1 패널과 메모 저장(M3 형식, battle 채워짐)을 확인했다. 프로필 바꿔 다시 시작은 M4 T6에서 같은 세팅·같은 시드로 바뀐 길로 확인한다.
- [ ] 개발 빌드 APK에서 세 손가락 터치
