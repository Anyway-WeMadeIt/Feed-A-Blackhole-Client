> 과거 M0~M6 구현 기록입니다. 현재 기능과 사용법은 [QA 안내](../../QA/README.md)를 참고하세요.

# M3 플레이 메모

- 상태: 구현 완료, Unity 손 확인 대기
- 브랜치: `feat/feelNotes` (← `feat/liveData` 710122a)
- 커밋: 3ee824c 기능 · 이 문서를 담은 docs 커밋

## 목표

특정 세팅(노드·이정표·Level) 기준의 느낌을 에디터(테스트 세팅 창)나 게임(개발 패널)에서 적는다. 적는 순간 그때의 세팅·수치 지문·판 상태와 함께 JSON 한 줄로 바로 쌓이고, 사람과 AI가 같은 파일을 읽는다.

## 결정

- Q2 메모 위치 → **레포 `PlaytestData/notes.ndjson`, git 무시**(10-10, 권장안). 팀이 같이 봐야 하면 그때 커밋 대상으로 바꾼다.
- 메모는 덧붙이기만 한다. 고치거나 지우지 않는다. 읽을 때 깨진 줄은 건너뛰고 그 수를 알린다.
- JSON은 자체 도구 `PlaytestJson`으로 쓰고 읽는다. JsonUtility는 null과 순서 있는 객체를 다루지 못하기 때문이다. M4 묶음도 이 도구를 쓴다.

## 만든 것

| 파일 | 내용 |
| --- | --- |
| `Playtest/PlaytestJson.cs` | `JsonObject`(넣은 순서 유지), 쓰기(한 줄·들여쓰기), 읽기(틀리면 FormatException) |
| `Playtest/FeelNote.cs` | 메모 한 줄(`FeelNote` → JSON), 읽은 메모(`FeelNoteView`), 세팅 키, 난이도 이름, 태그 어휘, 줄 단위 읽기 |
| `Playtest/PlaytestNotes.cs` | 파일 경로(에디터: 레포 `PlaytestData/`, 개발 빌드: `persistentDataPath/playtest/`), 덧붙이기, 읽기(파일이 그대로면 다시 읽지 않음), 바뀜 확인용 `Stamp` |
| `Playtest/PlaytestHud.cs` | `HudSnapshot`(흐른·남은 시간, 단계·Level·목표, EXP, 처치, Gold, 피해, 5초 속도, 종류별 적). HUD 글도 이 숫자로 만든다 |
| `Playtest/PlaytestPanel.cs` | 메모 탭(난이도·재미·태그·느낌·의도). 판이 바뀌는 순간 그 판의 세팅을 뜬다. `NewNote`를 창에 연다 |
| `Editor/Playtest/TestSetupWindow.cs` | 메모 칸과 이 세팅의 최근 메모 10개. 플레이 중에는 패널의 `NewNote`(실제로 플레이한 판)를 쓴다 |
| `.gitignore` | `/PlaytestData/` |

## 메모 한 줄 (schema 1, 실제 형식)

```json
{"schema":1,"id":"n-20261010-063012-3fa1","atUtc":"2026-10-10T06:30:12Z","source":"panel","build":"1.0","setupKey":"7a0cf5d4",
 "setup":{"name":"stage0-mid","growthStage":0,"startLevel":0,"seed":3,"gold":0,"nodes":[{"nodeId":"timer-01","rank":1}],"reachableInGame":null},
 "content":{"profile":"golden-x5","fingerprint":"a1b2c3d4","baseFingerprint":"9a787fff"},
 "battle":{"battleId":"…","elapsed":4,"remaining":10.3,"stage":0,"level":0,"goalLevel":10,"exp":3,"kills":3,"gold":216,"damage":204,
           "rates5s":{"span":3.98,"kills":0.75,"gold":54.27,"exp":0.75,"damage":51.26},"alive":{"asteroid":104},"cheated":false},
 "difficulty":-2,"fun":null,"tags":["golden-too-strong"],"text":"…","intent":"…"}
```

(실제 파일은 한 줄이다. 위는 읽기 좋게 줄을 나눴다.)

- `source`: `window`(테스트 세팅 창) 또는 `panel`(개발 패널)
- `setup`은 그 판이 시작한 상태다. 노드는 실제로 적용된 Rank이고, 콘텐츠에 없는 노드는 빠진다.
  - `startLevel`은 판이 실제로 시작한 Level이다. 시나리오 판은 `max(세팅 Level, 단계 시작 Level)`이고, 보통 판은 단계 시작 Level이다.
  - 창에서 적으면 슬라이더 Level이다.
- `reachableInGame`: 창에서 적으면 true/false다. 패널은 노드 트리를 모르므로 null이다.
- `setupKey` = SHA-1(`stage=..;level=..;nodes=id:rank,…` 이름 순)의 앞 8자리다. 이름·시작 Level 표기(0과 10)·콘텐츠에 없는 노드와 관계없이, 같은 상태면 같은 키다.
- `battle`은 플레이 중에만 채운다. `rates5s`는 표본이 0.5초보다 짧으면 null이다. `cheated`는 그 판이 테스트 표시(`+test`) 대상인지다.
- `difficulty`: −2(너무 쉬움) ~ +2(너무 어려움). `fun`: 1~5. 고르지 않으면 null이다.
- 태그 어휘(ID = 파일에 들어가는 값):

| 묶음 | 태그 |
| --- | --- |
| 속도 | `too-slow` 너무 느림, `too-fast` 너무 빠름, `level-stall` Level 정체, `level-rush` Level 급등 |
| 재미 | `boring` 지루함, `satisfying` 시원함, `chaotic` 정신없음 |
| 보상 | `gold-too-low` Gold 부족, `gold-too-high` Gold 과다, `golden-too-strong` 황금 과함, `node-pointless` 노드 효과 없음 |
| 기타 | `bug` 버그, `visual` 연출, `ui` UI |

계획에서 바꾼 것:

- 난이도 태그(`too-easy` 등)는 빼고 `difficulty` 하나로 받는다.
- `source`·`build`·`battle.stage/goalLevel/exp/damage`·`rates5s.span`을 더했다.
- 패널은 판이 시작할 때 세팅을 떠 둔다. 결산이 진행 상태(Gold·단계)를 바꾼 뒤에 적어도 그 판의 세팅이 남는다.

## 화면

- **테스트 세팅 창 "메모" 칸**
  - 난이도 5단계, 재미 1~5, 태그(묶음별 버튼, 고르면 파란색), 느낌, 의도, "메모 저장"
  - 아래 목록은 "이 세팅의 메모 N개 · 세팅 키 xxxxxxxx"와 최근 10개다.
    - 각 줄: 시각·난이도·재미·태그·판 Level과 흐른 시간·프로필, 그다음 느낌과 의도
    - 지문이 지금과 다르면 흐리게 "이전 수치(지문)"로 보인다.
  - 메모 파일이 바뀌면 목록이 1초 안에 다시 그려진다(`OnInspectorUpdate`). 패널에서 적어도 바로 보인다.
- **개발 패널 메모 탭**: 같은 칸을 IMGUI로 둔다. 저장하면 `메모 n-…를 저장했다(세팅 키 …)`라고 알린다.

## 작업

- [x] T1 JSON 도구·메모 형식 v1·세팅 키 + 헤드리스 테스트
- [x] T2 HudSnapshot과 HUD 글 분리
- [x] T3 PlaytestNotes schema 1 쓰기·읽기, 경로, `.gitignore`
- [x] T4 창의 메모 칸과 세팅별 목록
- [x] T5 패널 메모 탭
- [x] T6 컴파일·헤드리스
- [x] T7 문서 갱신, 커밋

## 검증

- 개발·릴리스·에디터 세 설정 컴파일 오류 0, 새 경고 0
- 헤드리스 172개 통과(기존 138 + 34)
  - JSON 왕복(따옴표·역슬래시·줄바꿈·탭·한글, null·bool·정수·9e12·실수, 중첩, 빈 객체·배열, 들여쓰기), 틀린 JSON 6가지 거부
  - 세팅 키: 순서 무관, Rank 0 무시, Level·Rank가 다르면 다르다. 창(Level 10 명시)과 패널(시나리오 startLevel 0)이 같은 키를 낸다
  - HudSnapshot: 실제 판 4초에서 속도·처치·Gold가 잡히고, HUD 글이 같은 숫자를 쓴다
  - 메모 왕복: 깨진 줄과 schema 0 줄은 건너뛰고(2줄) 나머지를 읽는다. 난이도 −2·재미 null·태그·글·판 상태가 그대로 돌아온다

## 완료 기준

- [x] 파일이 한 줄에 JSON 하나이고, 줄 단위로 읽을 수 있다(헤드리스)
- [x] 깨진 줄이 있어도 나머지를 읽고 수를 알린다(헤드리스)
- [ ] 에디터에서 세팅만 보고 메모를 저장하면 `PlaytestData/notes.ndjson`에 battle이 null인 한 줄이 생긴다
- [x] 플레이 중 패널에서 저장하면 battle과 지문이 채워진다(10-10 사용자 메모 n-20261010-053024-88f4: battle·rates5s·alive·지문 9a787fff)
- [ ] 플레이 중 창에서 저장해도 같다
- [ ] 같은 노드·단계·Level을 다른 이름으로 저장해도 창의 목록에 함께 보인다
- [ ] 수치를 바꾼 뒤(M2) 이전 메모가 "이전 수치"로 표시된다

## Unity 손 확인 순서

1. Test Setup 창의 "메모" 칸에서 난이도 "쉬움"과 태그 "황금 과함"을 고르고, 느낌·의도를 적어 저장한다.
   - 알림에 메모 ID와 세팅 키가 뜨는지 본다.
   - 아래 목록에 한 줄 생기는지 본다.
   - 레포 `PlaytestData/notes.ndjson`에 battle이 null인 줄이 있는지 본다.
2. 같은 세팅으로 ▶ 플레이하고 몇 초 뒤 F1 → 메모 탭에서 저장한다.
   - 창의 목록에 1초 안에 나타나는지 본다.
   - 파일의 그 줄에 battle·rates5s·alive가 채워졌는지 본다.
3. 판이 끝나 결산 화면일 때 패널에서 메모를 저장한다. setup의 단계가 그 판의 단계(이정표에 닿았어도 판 시작 단계)인지 본다.
4. 창에서 세팅 이름만 바꾼다. 목록이 그대로인지 본다(같은 세팅 키).
5. M2처럼 CSV 값 하나를 바꾼다. 이전 메모가 흐리게 "이전 수치"로 바뀌는지 본다.
6. 파일 끝에 아무 글 한 줄을 붙여 넣는다. 목록 머리에 "읽지 못한 줄 1개"가 보이고 나머지는 그대로인지 본다.

## 알려진 한계

- 패널에서 적은 메모는 `reachableInGame`이 null이다(패널은 노드 트리를 모른다).
- 개발 빌드(폰)의 메모는 기기 안에 있어 adb pull로 꺼내야 한다.
- 창의 목록은 최근 10개만 보인다(파일에는 모두 있다).
- 메모는 PC마다 따로다(git 무시). 팀 공유는 아직 없다.

## 다음으로 넘기는 것 (M4)

- AI 묶음은 이 세팅 키로 메모를 모은다. 묶음에는 화면용 `FeelNoteView`가 아니라 메모 원문(JSON 객체 전체)이 필요하다. M4에서 `PlaytestNotes`에 원문 읽기를 더한다.
- 창의 `NoteFromWindow`(세팅 → 메모의 setup)와 `CurrentSetupKey`를 묶음 만들기에서 그대로 쓴다.
- `PlaytestJson`의 들여쓰기 쓰기로 묶음 파일을 사람도 읽기 좋게 쓴다.
