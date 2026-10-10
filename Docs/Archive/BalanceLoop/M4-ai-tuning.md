> 과거 M0~M6 구현 기록입니다. 현재 기능과 사용법은 [QA 안내](../../QA/README.md)를 참고하세요.

# M4 AI 조정

- 상태: 구현·커밋 완료, Unity 손 확인·한 바퀴 시연 대기
- 브랜치: `feat/aiTuning` (← `feat/feelNotes`)
- 커밋: 209a23e 장면 연결(M0 남은 일) · e11fb5a AI 조정 · (문서) 이 커밋
- M3에서 받은 것:
  - 메모 파일 `PlaytestData/notes.ndjson`(schema 1)과 세팅 키
  - `PlaytestJson`(쓰기·읽기)
  - 창의 `NoteFromWindow`·`CurrentSetupKey`, 패널의 `NewNote`, `HudSnapshot`
  - M2의 수치 지문과 `LiveDataSignal`

## 목표

AI가 메모·세팅·확정 정보·현재 수치를 읽고 의도에 맞게 수치를 바꾼다. 바꾼 값은 M2로 바로 반영되어 같은 세팅으로 다시 플레이한다. 모든 변경은 이유와 함께 기록되고 되돌릴 수 있다.

## 결정 (10-10, 사용자 "진행"으로 권장안 채택)

- Q3 AI 변경 방식: 초안 프로필 → 비교 플레이 → 승격.
- 승격은 사람이 창의 버튼으로 누른다. AI는 초안까지만 쓴다.

## 만든 것

### 1. AI 묶음 `PlaytestData/context/<setupKey>.json` (`Playtest/AiContext.cs`, `Editor/Playtest/AiContextService.cs`)

- 에디터가 쓴다. 쓰는 때:
  - 메모가 새로 쌓일 때(창·패널 어디서든). 1초마다 메모 파일을 본다.
  - 수치나 프로필(초안 포함)이 바뀔 때(`LiveDataSignal`). 최근에 쓴 묶음 3개만 다시 쓴다.
  - 창의 "AI 묶음 만들기"를 누를 때. 메모가 없어도 지금 세팅으로 쓴다.
  - 에디터를 켠 뒤 처음: 최근 메모 가운데 묶음이 없는 세팅만 쓴다.
- 목록 `PlaytestData/context/index.json`: 세팅 키, 이름, 메모 수, 마지막 메모, 초안 파일.
- 담는 것(schema 1, 칸 설명은 [AI-GUIDE](AI-GUIDE.md) 2절):
  - `setup`, `content`(원본 지문·고른 프로필), `notes`(원문, 최근 20개)
  - `report`: 확정 정보. 창과 같은 `TestSetupPreview`로 만든다.
  - `simulation`: 사람 없이 돌린 흐름. 설계에 없던 것을 더했다(아래).
  - `nodes`: 산 Rank와 다음 Rank의 비용·효과. 값마다 프로필 경로가 붙는다.
  - `values`: 노드가 아닌 모든 경로의 원본 값(173개).
  - `draft`: 검사 결과, 패치별 이전·이후·배율·원본 칸, 원본과 달라진 확정 정보(`compare`), 초안 흐름.
  - `changes`: 최근 변경 기록 10개.
- 흐름(`Playtest/PlaytestSimulation.cs`): 게임과 같은 길로 판을 만들고, 조준을 (0,4)에 고정한 채 1/60초씩 최대 60초를 돌린다. 2초마다 Level, 종류별 적 수, 처치, Gold, EXP, `cover`를 잰다. `cover`는 출현 띠 넓이 대비 적 원 넓이의 합이다.
  - 더한 이유: AI는 Unity를 돌릴 수 없다. "빽빽하다" 같은 느낌을 원본과 초안에서 같은 시드로 비교할 숫자가 필요했다.
  - 사람 플레이와 다르다. 비교 기준선으로만 쓰고, 실제 숫자는 메모의 `battle`을 본다.

### 2. AI 초안 `Assets/Playtest/Profiles/ai-draft.json`

- 형식은 밸런스 프로필과 같고, 패치마다 `reason`·`noteIds`를 적는다(`BalanceProfile.Patch`).
- git이 무시하는 작업 파일이다. 빌드의 PlaytestLibrary에도 넣지 않는다.
- 테스트 세팅 창 "AI 조정" 칸:
  - 초안의 패치를 `경로: 원본 값 → 초안 값 (×배율)`과 이유로 보인다.
  - 초안 켜기 / 원본으로:
    - 플레이 밖: 프로필을 고르고 확정 정보를 다시 계산한다.
    - 플레이 중: 같은 세팅·같은 시드로 판을 다시 시작한다(`PlaytestPanel.RestartWithProfile`). 개발 패널의 프로필 바꾸기도 이제 같은 길이다.
  - 초안을 원본에 반영, 초안 버리기, 이 세팅의 묶음 상태와 "AI 묶음 만들기", 최근 변경 5개와 되돌리기.

### 3. 승격 (`Playtest/ProfileTargets.cs`, `Playtest/NodeSheetEdits.cs`, `Editor/Playtest/ProfilePromoter.cs`)

- 대응표: 프로필 경로 → 원본 칸.

| 프로필 경로 | 원본 | 칸 |
| --- | --- | --- |
| `node/<id>/<rank>/cost` | `NodeCost.csv` | 그 행의 `Cost` |
| `node/<id>/<rank>/<StatId>` | `NodeEffects.csv` | 그 행의 `Value`(+ `표시`) |
| `battle/timeLimit` · `battle/killTimeBonus` | BattleRules | `timeLimit` · `killTimeBonus` |
| `breaker/<field>` | SkillSetup | `breaker<Field>`. `planetBonusDamage`·`starBonusDamage`는 에셋 칸이 없어 승격하지 않는다 |
| `placement/minDistance` · `maxDistance` | EnemySupplySetup | 같은 이름 |
| `enemy/<type>/<moveSpeed\|radius\|radiusStep\|spawnPeriod\|rainCount>` | Enemies/<Type> | 같은 이름 |
| `enemy/<type>/tier/<n>/<hp\|gold\|exp>` | Enemies/<Type> | `tiers[n-1].maxHealth\|gold\|exp` |
| `enemy/<type>/trait/<trait>/<field>` | Enemies/<Type> | `type`이 그 성질인 `traits` 원소의 칸 |
| `supply/<type>/count` | EnemySupplySetup | `kind`가 그 종류인 `startSupply` 원소의 `count`. 없는 종류는 승격하지 않는다(에셋에서 더한다) |
| `growth/levelExp/<n>` | HqGrowthSetup | `levelExp[n-1]` |
| `growth/milestone/<n>/<field>` | HqGrowthSetup | `milestones[n-1].<field>` |

- 순서:
  1. 모든 패치의 원본 칸을 찾고 이전 값을 읽는다. 하나라도 못 찾으면 아무것도 바꾸지 않는다.
  2. CSV는 파일마다 한 번 쓴다. 에셋은 `SerializedObject`로 고치고 저장한다.
  3. 변경 기록에 남기고, 초안을 `PlaytestData/drafts/<변경 ID>.json`으로 옮긴다. 고른 프로필이 초안이었으면 원본으로 돌린다.
- CSV 서식 지키기:
  - 고친 칸 말고는 한 글자도 바꾸지 않는다(줄 끝, BOM, 다른 칸의 따옴표, 분석용 칸).
  - 비용은 시트처럼 천 단위 쉼표로 쓰고, 쉼표가 들어가면 따옴표로 감싼다.
  - `표시` 칸("+25%")은 지금 글자가 이전 값의 표시와 같을 때만 새 값으로 바꾼다. 손으로 적은 글은 둔다.
  - 한 칸이 여러 줄인 CSV, 같은 행이 둘인 CSV는 고치지 않는다.
- 설계와 달라진 점: 초안 보관 위치를 `Assets/Playtest/Profiles/archive/` 대신 `PlaytestData/drafts/`로 바꿨다. 에셋으로 가져와지지 않고, 빌드에 들어가지 않고, 메모·변경 기록과 같은 곳에 남는다.

### 4. 변경 기록 `PlaytestData/changes.ndjson` (`Playtest/ChangeRecord.cs`, `Playtest/PlaytestChanges.cs`)

```json
{"schema":1,"id":"c-20261010-120000-4b24","atUtc":"2026-10-10T12:00:00Z","kind":"promote","author":"ai","appliedBy":"human",
 "profile":"ai-draft","profileNote":"…","revertOf":null,
 "patches":[{"path":"enemy/asteroid/radiusStep","target":"적 종류 asteroid · radiusStep","before":0.35,"after":0.25,"reason":"…","noteIds":["n-…"]}],
 "fingerprintBefore":"9a787fff","fingerprintAfter":"…","setupKeys":["f6f3b5bd"],"archive":"PlaytestData/drafts/c-….json","sheetSynced":false}
```

- `kind`: promote | revert. 되돌리기는 `revertOf`에 되돌린 변경 ID를 적는다.
- `sheetSynced`는 M5 전까지 늘 false다. 노드 경로를 반영했으면 창이 "기획 시트에도 옮긴다"를 보인다.

### 5. 되돌리기

- 기록의 이전 값으로 같은 길을 간다. 패치는 뒤에서부터 돌린다.
- 지금 값이 기록의 이후 값과 다르면(그 뒤에 또 바뀌었으면) 하지 않고 알린다.

### 6. 그 밖

- 프로필 경로:
  - 출현 띠 `placement/minDistance`·`maxDistance`를 더했다(사용자 메모 "생성 범위를 넓혀야").
  - 모든 경로의 지금 값 읽기를 더했다(`TryRead`, `ContentPaths`, `NodePaths`).
  - float 값은 사람이 적은 십진수 그대로 읽는다(0.35f → 0.35).
- 메모 태그 "밀도: 너무 빽빽함·너무 듬성함"을 더했다.
- AI 작업 지침 [AI-GUIDE.md](AI-GUIDE.md): 읽을 곳, 묶음 칸, 판단 순서, 손잡이 표, 초안 형식과 제한, 확인, 알리는 법.

## 검증

- 개발·릴리스·에디터 세 설정 컴파일 오류 0, 새 경고 0. 커밋 트리의 스크립트가 검증한 것과 같음을 확인했다.
- 헤드리스 2832개 통과. 기존 172개는 유지했고, 노드 CSV 행마다 검사하는 항목이 많아 수가 늘었다.
  - 경로 173개가 모두 읽힌다. 그중 171개가 같은 값 종류(Real·Int·Long)의 에셋 칸으로 풀리고, 그 칸이 에셋 클래스에 선언되어 있다. 에셋 칸이 없는 2개는 승격을 거부한다.
  - 노드 CSV: 비용 319행·효과 319행을 바꿨다 되돌리면 글자 하나 다르지 않다. 바꾸면 한 줄만 바뀌고, 게임 읽기로 새 값이 읽힌다.
  - 큰 비용의 쉼표·따옴표, 표시 칸, 손으로 적은 표시 칸 유지, BOM·CRLF·따옴표 칸 유지, 음수, 없는 행·같은 행 둘·짝 안 맞는 따옴표 거부.
  - 변경 기록 쓰고 읽기, 되돌린 변경 찾기, 패치의 reason·noteIds(없어도 됨).
  - 흐름: 같은 세팅·시드면 같은 결과다.
  - 묶음: 사용자 메모(n-20261010-053024-88f4)로 만든다. 메모 원문, 확정 정보, 흐름, 산 노드 55개의 경로별 값이 들어간다. 초안이 있으면 검사·이전 값·원본 칸·compare·흐름이 들어가고, 틀린 초안과 읽지 못한 초안은 오류로 보인다.
  - 메모에서 되살린 세팅의 세팅 키가 메모의 것과 같다.
- 실제 메모로 만든 첫 초안(T6 AI 몫): 메모 "소행성이 화면을 빽빽하게 채워서 빈 공간이 안 보임 / 소행성 사이로 빈 공간이 보였으면"(m4-trial, Lv5 25초에 소행성 314)에 대해:
  - 버그 의심: 없음. 크기(radiusStep)와 출현 띠는 레퍼런스 실측과 맞는 값이라 두었다.
  - 손잡이: `growth.asteroids-01`·`-02` 효과 25 → 12.5. Level업 성장 공급이 시작 수의 50%에서 25%가 된다.
  - 같은 시드 흐름: Lv3 소행성 250 → 172, cover 0.53 → 0.37. 초안 지문 6595b75b(헤드리스 계산).
  - 생성 범위 넓히기는 쓰지 않았다. 띠 끝(9.6)이 이미 카메라 세로 반 폭(10) 근처라 9.6 → 10이 cover를 약 8%만 줄이고, 그보다 넓히면 화면 밖에서 돈다.

## 작업

- [x] T1 컨텍스트 묶음 작성기와 형식(헤드리스로 실제 데이터 묶음 생성 확인)
- [x] T2 프로필 패치의 `reason`·`noteIds` 필드, 창의 "초안 있음" 표시와 켜기
- [x] T3 승격: CSV 행 편집기(서식 유지) + 에셋 대응표 편집기 + 변경 기록
- [x] T4 되돌리기
- [x] T5 `AI-GUIDE.md`
- [ ] T6 한 바퀴 시연: 메모 → AI 초안(완료) → 비교 → 승격 → 되돌리기(Unity, 아래 순서)
- [x] T7 컴파일·헤드리스, 문서 갱신(M4 결과, PLAN 점검, M5 문서 보정), 커밋

## 완료 기준

- [ ] 메모를 저장하면 그 세팅의 컨텍스트 묶음이 생기고, 확정 정보·메모·노드 행이 들어 있다.
  - 10-10 확인: Unity가 새 코드를 컴파일한 뒤 기존 메모로 `f6f3b5bd.json`·`index.json`을 썼다(에디터를 켠 뒤 처음 쓰기). 내용이 헤드리스 계산과 같다: 초안 valid, 지문 6595b75b, 흐름 숫자, 노드 55개, 값 173개.
  - 새 메모를 저장할 때 다시 써지는지는 아직 보지 않았다.
- [ ] AI가 쓴 초안이 몇 초 안에 반영되고, 원본과 같은 세팅·시드로 번갈아 플레이할 수 있다.
- [ ] 승격하면 CSV는 그 칸만, 에셋은 그 필드만 바뀐다(git diff로 확인). 변경 기록에 이전·이후 값이 남는다.
- [ ] 되돌리면 git diff가 비어 있는 상태로 돌아간다.
- [x] 헤드리스: CSV 행 편집이 서식을 유지하고, 대응표의 모든 경로가 에셋 필드로 풀린다.

## Unity 손 확인 순서 (T6 한 바퀴)

1. Unity를 앞으로 가져와 컴파일을 기다린다. 콘솔에 빨간 오류가 없어야 한다.
2. `PlaytestData/context/f6f3b5bd.json`이 생겼는지 본다(기존 메모의 묶음, 에디터를 켠 뒤 처음 1초 안).
3. Test Setup 창 "AI 조정" 칸에 "AI 초안 · 값 2개 · 꺼짐"과 두 줄(`25 → 12.5 (×0.5)`)이 보이는지 본다.
4. m4-trial을 열고 ▶ 플레이한다. 몇 초 뒤 창의 "초안 켜기"를 누른다.
   - 같은 세팅·같은 시드로 판이 다시 시작하는지 본다.
   - HUD 첫 줄에 `ai-draft`와 지문 `6595b75b`가 보이는지 본다.
   - "원본으로"를 누르면 `원본`과 지문 `9a787fff`로 같은 배치가 돌아오는지 본다.
   - 이 단계가 M2의 "플레이 중 수치가 바뀌면 같은 세팅·같은 시드로 다시 시작"도 함께 확인한다.
5. 초안을 켠 채 메모를 하나 저장한다. 묶음의 `notes[0].content.profile`이 `ai-draft`인지 본다.
6. 플레이를 멈추고 "초안을 원본에 반영"을 누른다.
   - `git diff`에 `NodeEffects.csv` 두 줄만 바뀌는지 본다(`Value` 12.5, `표시` +12.5%).
   - `PlaytestData/changes.ndjson`에 한 줄이 생기고, 초안이 `PlaytestData/drafts/`로 옮겨졌는지 본다.
   - 창 제목의 지문이 6595b75b인지 본다.
7. 변경 기록의 "되돌리기"를 누른다. `git diff`가 비고, 지문이 9a787fff로 돌아오는지 본다.

## 알려진 한계

- AI를 부르는 버튼은 없다. 채팅에서 AI에게 말한다(자동은 M6).
- 묶음 쓰기는 흐름 계산 때문에 묶음 하나에 원본·초안 두 번 판을 돌린다. 플레이 중 메모를 저장하면 에디터가 잠깐 멈칫할 수 있다.
- 흐름은 조준 고정이라 Level 오르는 속도가 사람보다 느리다(사용자 판 Lv5/25초, 흐름 Lv3/23초).
- 시작 공급에 없는 종류의 `supply/<type>/count`, `breaker/planetBonusDamage`·`starBonusDamage`는 승격하지 않는다.
- 초안은 하나뿐이다(`ai-draft`). 여러 안을 나란히 두려면 다른 이름의 프로필로 복사한다.

## 다음으로 넘기는 것 (M5)

- 노드 경로를 반영한 기록은 `sheetSynced: false`이고 `target`에 CSV 행이 적혀 있다. M5의 끌어오기 경고와 시트 반영이 이것을 읽는다.
- `NodeSheetEdits`(행 찾기·서식 유지 편집)와 표시 칸 규칙을 시트 반영에서 그대로 쓴다.
- 수치 지문을 전투 요약 계약에 넣는 일(Q6)은 아직 열려 있다.
