> 과거 M0~M6 구현 기록입니다. 현재 기능과 사용법은 [QA 안내](../../QA/README.md)를 참고하세요.

# AI 작업 지침 (밸런스 루프)

이 문서는 사람이 남긴 느낌 메모를 읽고 수치 초안을 만드는 AI(Claude 등)가 따른다.
AI는 초안까지만 만든다. 원본(노드 CSV·에셋)에 반영하는 것은 사람이 테스트 세팅 창에서 한다.

## 1. 읽을 곳

| 파일 | 내용 |
| --- | --- |
| `PlaytestData/context/index.json` | 묶음 목록. 세팅 키마다 이름, 메모 수, 마지막 메모 ID와 시각이 있다. 가장 최근 메모가 있는 묶음부터 본다. |
| `PlaytestData/context/<setupKey>.json` | 판단에 필요한 전부(아래 2). 에디터가 메모를 저장할 때, 수치나 초안이 바뀔 때 다시 쓴다. |
| `PlaytestData/notes.ndjson` | 메모 원문 전체. 묶음에는 그 세팅의 최근 20개만 있다. |
| `PlaytestData/changes.ndjson` | 변경 기록 전체: 반영(promote)·되돌리기(revert), 시트에서 끌어옴(pull, author `sheet`), 시트에 반영(sheet). |

- AI는 Unity를 돌릴 수 없다. 필요한 숫자는 묶음에 미리 계산되어 있다.
- 묶음이 없거나 오래됐으면 사람에게 창의 "AI 묶음 만들기"를 눌러 달라고 한다.
- 레퍼런스 실측(원작 화면에서 잰 크기·카메라·출현 띠)은 프로젝트 문서 `claude/reference-scale.md`에 있다.

## 2. 묶음의 칸

| 칸 | 내용 |
| --- | --- |
| `setup` | 세팅: 이정표 단계, 시작 Level, 시드, 산 노드와 Rank. 메모를 쓴 판이 실제로 쓴 값이다. |
| `content` | 원본 수치 지문(`baseFingerprint`), 사람이 고른 프로필, 초안이 있는지. |
| `notes` | 이 세팅의 메모 원문(최근 것이 앞). `intent`, `difficulty`(−2~2), `fun`(1~5), `tags`, `text`, 그리고 적은 순간의 `battle`(Level, 흐른 시간, 적 수, 5초 속도). |
| `report` | 확정 정보. 테스트 세팅 창에 보이는 것과 같고, 원본 값 기준이다. |
| `simulation` | 사람 없이 돌린 흐름: 조준을 한 곳에 고정하고 2초마다 Level, 적 수, `cover`(출현 띠 넓이 대비 적 원 넓이의 합)를 잰다. 사람 플레이와 다르니 원본과 초안을 비교하는 기준선으로만 쓴다. |
| `nodes` | 산 노드마다 산 Rank와 다음 Rank의 비용·효과. `path`는 프로필에 그대로 쓰는 경로다. |
| `values` | 노드가 아닌 모든 경로의 지금 원본 값. |
| `draft` | 초안이 있으면 채워진다: `valid`, `errors`, 패치마다 `before`·`after`·`ratio`·`target`(반영될 원본 칸), `compare`(원본과 달라진 확정 정보 칸), 초안의 `simulation`. |
| `changes` | 최근 변경 기록 10개. |

## 3. 판단 순서

1. **메모를 읽는다.** `intent`가 가장 중요하다. 그다음 `difficulty`·`tags`, 마지막으로 `text`를 본다.
   - 메모의 `content.fingerprint`가 지금 `baseFingerprint`와 다르면 이전 수치에서 적은 메모다. 참고만 한다.
   - `battle`이 있으면 그 순간의 실제 숫자(Level, 적 수)로 문제의 크기를 잰다.
2. **버그나 잘못 들어간 값부터 의심한다.** 단위(Percent·Flat), 자릿수, 레퍼런스와 다른 값이 그런 예다. 그런 값이 있으면 고치지 말고 사람에게 먼저 알린다.
3. **가장 작은 손잡이 하나를 고른다.** 의도에 직접 닿고 부작용이 적은 것을 고른다.
   - 레퍼런스에서 잰 값(적 크기와 `radiusStep`, 출현 띠, 이정표 전장 배율)은 되도록 두고, 메모가 그 값을 문제 삼을 때만 바꾼다.
4. **바꾼 뒤의 숫자를 예상해 적는다.** 식(예: Level업 성장 공급 = 시작 수 × %)이나 흐름 비교를 쓴다.

자주 쓰는 손잡이:

| 느낌 | 먼저 볼 경로 | 비고 |
| --- | --- | --- |
| 적이 너무 빽빽함·듬성함 | `node/growth.asteroids-0N/1/growth.asteroids`(Level업 성장 공급 %), `node/asteroid.count-0N/1/asteroid.count`(시작 공급 추가), `supply/asteroid/count` | 성장 공급은 시작 수의 %다. 시작 수를 줄이면 성장 공급도 같이 준다. |
| 판이 짧음·김 | `battle/timeLimit`, `battle/killTimeBonus`, `node/growth.time-01/1/growth.time` | |
| Level이 안 오름·너무 빨리 오름 | `growth/levelExp/<Level>`, `enemy/<종류>/tier/<n>/exp` | |
| Gold 부족·과다, 황금 과함 | `enemy/asteroid/trait/golden/multiplier`, `enemy/<종류>/tier/<n>/gold`, 노드 `cost` | |
| 출현 범위 | `placement/minDistance`, `placement/maxDistance` | 카메라 세로 반 폭(10 × 전장 배율)을 넘으면 화면 밖에서 돈다. 레퍼런스는 띠 끝이 카메라 끝과 같다. |
| 적 크기 | `enemy/<종류>/radius`, `enemy/<종류>/radiusStep` | 크기 k의 반지름 = radius × (1 + (k−1) × radiusStep). 레퍼런스 실측값이다. |

## 4. 초안 쓰기

- 파일은 `Assets/Playtest/Profiles/ai-draft.json` 하나다. 새 초안은 덮어쓴다. git이 무시하는 작업 파일이다.
- 형식은 밸런스 프로필과 같고, 패치마다 `reason`과 `noteIds`를 적는다.

```json
{
  "name": "ai-draft",
  "note": "소행성 밀도 완화(메모 n-20261010-053024-88f4)",
  "patches": [
    {
      "path": "node/growth.asteroids-01/1/growth.asteroids",
      "value": 12.5,
      "reason": "Level업 성장 공급 50% → 25%의 절반. Lv3 흐름 소행성 250 → 172 예상",
      "noteIds": ["n-20261010-053024-88f4"]
    }
  ]
}
```

- 경로 문법은 `Assets/Scripts/Playtest/BalanceProfilePatcher.cs` 머리 주석에 있다. 묶음의 `nodes[].ranks[].values[].path`와 `values`의 키를 그대로 쓰면 된다.
- 제한:
  - 한 번에 값 3개 이하로 바꾼다.
  - 한 값은 ×0.5~×2 안에서만 바꾼다. 사람이 넓히라고 하면 예외다.
  - 노드 ID, Rank 수, 선, 단위는 고치지 않는다(시트 구조다).
  - `reason`과 `noteIds`는 반드시 적는다.
  - 다른 프로필 파일, 노드 CSV, 에셋은 직접 고치지 않는다. 반영은 사람이 한다.
  - `breaker/planetBonusDamage`·`breaker/starBonusDamage`는 에셋 칸이 없어 반영되지 않는다. 노드 효과 경로를 쓴다.

## 5. 쓴 뒤 확인

- Unity가 켜져 있으면 몇 초 안에 묶음의 `draft` 칸이 다시 써진다.
  - `valid`가 false면 `errors`를 보고 고친다.
  - `compare`와 `draft.simulation`이 예상과 맞는지 본다.
- Unity가 꺼져 있으면 검사는 다음에 켤 때 한다. 사람에게 그렇다고 말한다.

## 6. 사람에게 알리기

채팅에 세 줄로 알린다.

1. 무엇을: 경로, 이전 값 → 이후 값
2. 왜: 메모 ID와 의도
3. 예상: 숫자

그리고 비교하는 법을 한 줄로 덧붙인다: 테스트 세팅 창 "AI 조정"의 "초안 켜기"와 "원본으로"를 누르면 같은 세팅, 같은 시드로 번갈아 플레이할 수 있다.

## 7. 반영과 되돌리기 (사람이 한다)

- 창의 "초안을 원본에 반영"은 노드 CSV의 그 칸(서식 유지)과 에셋의 그 칸만 고친다. `PlaytestData/changes.ndjson`에 이전·이후 값, 이유, 메모 ID, 지문 앞뒤를 남기고, 초안은 `PlaytestData/drafts/`로 옮긴다.
- 노드 경로를 반영한 기록은 아직 기획 시트에 없다. 사람이 Sheet Sync 창(BlackHole > Sheet Sync)의 "시트에 반영"으로 옮긴다. 뒤의 `sheet` 기록의 `syncOf`에 들면 옮겨진 것이다.
- 기획자가 시트에서 바꾼 값은 `pull` 기록(author `sheet`)으로 들어온다. 초안을 쓰기 전에 최근 pull을 본다. 기획자가 방금 바꾼 칸을 AI가 바로 되돌리지 않게 한다.
- 창의 "되돌리기"는 기록의 이전 값으로 돌린다. 그 뒤에 값이 또 바뀌었으면 하지 않는다.

## 8. 자동 실행 (M6)

Unity 에디터가 이 PC의 Claude Code를 헤드리스로 실행해 초안을 맡길 수 있다(테스트 세팅 창의 "AI에게 초안 맡기기", 또는 "메모 저장 때 자동으로 맡기기"). 자세한 것은 [M6 문서](M6-auto-loop.md).

- 지시는 `PlaytestData/ai-runs/<runId>/prompt.md`로 들어온다. 이 지침을 따르되, 다른 점은 다음과 같다.
  - 사람이 대화에 없다. 묻지 않고 끝까지 한다.
  - 쓸 수 있는 파일은 `Assets/Playtest/Profiles/ai-draft.json` 하나다. 명령 실행, 웹, 다른 파일 쓰기는 막혀 있다.
  - 초안 검사(5절)는 실행이 끝난 뒤 에디터가 한다. 묶음의 draft 칸을 기다리지 않는다. 검사에 실패하면 에디터가 오류를 붙여 한 번 더 맡긴다.
  - 마지막 답은 세 줄(6절)만 쓴다. 에디터 창에 그대로 보인다. 초안을 쓰지 않으면 첫 줄을 `초안 없음: <이유>`로 한다.
- 실행 기록은 `PlaytestData/ai-runs.ndjson`에 남는다. 채팅에서 일할 때도 최근 자동 실행의 결과를 여기서 볼 수 있다.
