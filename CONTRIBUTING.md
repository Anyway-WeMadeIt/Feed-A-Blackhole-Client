# 협업 규칙

## 브랜치와 PR

- `main`을 기준으로 작업 브랜치를 만들고 PR로 병합합니다.
- 브랜치는 `feat/주제`, `fix/주제`, `chore/주제`처럼 목적을 드러내는 이름을 사용합니다.
- PR은 하나의 목적에 집중하고, 최소 1명의 팀원 리뷰 후 병합하는 것을 기본으로 합니다.
- PR 템플릿에 변경 이유, 검증 결과와 관련 이슈를 작성합니다. 화면 변경은 스크린샷이나 GIF를 첨부합니다.
- 백엔드 API 계약을 변경하는 작업은 요청·응답과 오류 처리의 변경점도 PR에 설명합니다.

이 문서는 팀의 작업 규칙입니다. Git 호스팅 서비스의 브랜치 보호, 필수 리뷰 및 CI 검사는 별도 설정이 필요하며 현재 자동으로 강제하지 않습니다.

## 커밋

```text
type(scope): 설명
```

`scope`는 선택이며, 제목과 본문은 한국어로 작성할 수 있습니다.

| type | 용도 |
| --- | --- |
| `feat` | 기능 추가 |
| `fix` | 버그 수정 |
| `refactor` | 동작을 유지하는 코드 구조 개선 |
| `docs` | 문서 변경 |
| `test` | 테스트 추가·수정 |
| `chore` | 설정, 패키지 및 기타 유지보수 |

예: `feat(upgrade): 블랙홀 흡입 범위 업그레이드 추가`

저장소 루트에서 `git config --local commit.template .gitmessage`를 실행하면 `git commit`에 템플릿이 적용됩니다.
템플릿의 `#` 주석은 최종 커밋 메시지에서 제외됩니다. 커밋 메시지 형식 검사 훅은 사용하지 않습니다.

## C# 컨벤션

- `.editorconfig`를 기준으로 UTF-8, LF, 공백 4칸, 새 줄 중괄호를 사용합니다.
- 타입·네임스페이스·메서드(로컬 함수 포함)·프로퍼티·이벤트·enum 멤버는 `PascalCase`를 사용합니다.
- 인터페이스는 `ICollectable`처럼 `I` + `PascalCase`, 제네릭 타입 매개변수는 `T`, `TItem`처럼 `T` 접두사를 사용합니다.
- 매개변수·지역 변수는 `camelCase`, 상수(`const`, 지역 상수 포함)는 `PascalCase`를 사용합니다.
- private/internal/private protected 필드는 `_camelCase`를 사용합니다. `static`·`readonly` 필드에도 같은 규칙을 적용하고, `const`는 상수 규칙을 따릅니다.
- public/protected/protected internal 필드는 `PascalCase`를 사용합니다. Inspector 노출은 필요한 경우 `[SerializeField] private`로 선언하며 이 필드도 `_camelCase`를 사용합니다.
- 클래스와 파일 이름을 일치시키고, 접근 제한자를 명시합니다.
- 변경과 무관한 파일 전체 포맷팅은 피합니다. Unity 직렬화 파일은 Editor가 생성하는 형식을 유지합니다.

명명 규칙은 `.editorconfig`를 지원하는 IDE에서 `suggestion` 수준으로 진단합니다. Unity 컴파일 오류나 CI 실패를 발생시키는 설정은 아닙니다.
표준 .NET 명명 규칙은 `[SerializeField]` 같은 특성에 따른 구분, 클래스와 파일명 일치 여부, Unity 메시지의 정확한 이름까지 검사하지 못하므로 이 부분은 코드 리뷰로 확인합니다.
기존 직렬화 필드의 이름을 변경할 때는 씬·프리팹 데이터가 유지되는지 확인하고, 필요한 경우 `[FormerlySerializedAs]`로 이전 이름을 연결합니다. `.editorconfig` 추가만으로 기존 이름이 자동 변경되지는 않습니다.

## Unity 에셋과 Git

- `Assets`, `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings`를 함께 관리합니다.
- 에셋 추가·이동·삭제는 Unity Editor에서 수행하고 관련 `.meta` 파일도 함께 커밋합니다.
- `Visible Meta Files` 및 `Force Text` 설정을 유지합니다.
- 같은 씬이나 프리팹의 동시 편집은 미리 조율하고, 충돌 해결 후 Editor에서 다시 확인합니다.
- `.gitattributes`의 이미지·오디오 등 바이너리 파일에는 Git LFS가 적용됩니다. 팀원 모두 Git LFS를 설치합니다.
- `Library`, `Temp`, `Logs`, `UserSettings`, IDE 캐시와 OS 생성 파일은 커밋하지 않습니다.
- `.vscode`에서는 `extensions.json`, `launch.json`, `settings.json`, `tasks.json`만 공유합니다. 개인 절대 경로는 넣지 않습니다.
- 인증 정보는 커밋하지 않습니다. `.env.example`에는 예시 값만 기록합니다. Unity는 `.env`를 기본으로 로드하지 않으며, 향후 클라이언트 연동 설정에도 서버 비밀키를 넣지 않습니다.

## 제출 전 확인

1. Console 컴파일 오류가 없는지 확인합니다.
2. 변경한 기능을 해당 씬에서 실행하고 확인 절차와 결과를 PR에 기록합니다.
3. 에셋의 Missing Script, 누락된 참조 및 `.meta` 누락을 확인합니다.
4. `git diff --check`와 `git status --short`로 공백 오류와 불필요한 파일을 확인합니다.
5. 검증하지 못한 항목은 PR에 이유를 명시합니다.
