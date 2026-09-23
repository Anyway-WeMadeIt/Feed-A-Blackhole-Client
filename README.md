# Feed A Blackhole Client

백엔드 부트캠프 최종 프로젝트의 Unity 클라이언트 저장소입니다.
**A Game About Feeding A Black Hole**을 레퍼런스로 하는 인크리멘탈 게임을 개발합니다.
백엔드는 별도로 Spring Boot 기반으로 구현할 예정이며, API 연동은 아직 구현되지 않았습니다.

## 개발 환경

- Unity **6000.6.2f1** (`ProjectSettings/ProjectVersion.txt` 기준)
- Universal Render Pipeline (2D), Input System
- Git 및 Git LFS
- C# 개발용 IDE: Visual Studio, Rider 또는 VS Code

## 시작하기

Git LFS 설치 후 저장소를 복제합니다.

```sh
git lfs install
git clone <저장소 URL>
cd "<복제한 폴더>"
git lfs pull
git config --local commit.template .gitmessage
```

1. Unity Hub에서 **6000.6.2f1** Editor를 설치합니다.
2. `Assets`, `Packages`, `ProjectSettings`가 있는 저장소 루트를 프로젝트로 엽니다.
3. 패키지 복원과 에셋 임포트가 끝날 때까지 기다립니다.
4. `Assets/Scenes/SampleScene.unity`를 열고 Play 모드 진입 및 Console 오류 여부를 확인합니다.

현재 씬은 게임 구현 전의 기본 2D 씬입니다. Editor 버전과 패키지 버전 변경은 PR에서 팀과 공유합니다.
커밋 템플릿 설정은 복제본마다 한 번 적용해야 합니다. `git commit` 실행 시 템플릿이 열리며,
`git commit -m` 또는 일부 IDE의 커밋 입력 창에는 자동 적용되지 않습니다.

협업 규칙은 [CONTRIBUTING.md](CONTRIBUTING.md)를 참고하세요.
