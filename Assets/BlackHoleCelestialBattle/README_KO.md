# Celestial Battle Kit — Black / White

전투 가독성을 위해 단순화한 블랙·화이트 배경과 Canvas Shader Graph입니다. 중앙은 비우고, 작은 별빛과 짧은 궤도 효과가 가장자리에서만 움직이도록 구성했습니다.

## 0. Feed-A-Blackhole 연결

- 위치는 키트 경로 그대로 `Assets/BlackHoleCelestialBattle`입니다. 키트 밖에 있던 `Source/`는 이 폴더 안으로 옮겼고, `Source/BuildBattleMaps.py`의 경로도 그에 맞췄습니다.
- 설치: GameScene을 열고 **Tools → Black Hole → Battle Background → Install in Open Scene**을 한 번 실행한 뒤 씬을 저장합니다.
  - 씬의 `BattleBackground` Canvas(Screen Space - Camera, Plane Distance 900, Order -100) 아래 첫 자식으로 `CelestialBackground`(Background + Ambient)를 만듭니다.
  - 이전 배경 `Background_Image`는 지우지 않고 끕니다.
  - `CelestialBackground`에 `BattleBackgroundTheme`을 붙여 White·Black 그림과 머티리얼을 연결하고, GameBootstrap의 **Battle Background** 칸에 연결합니다.
  - 다시 실행해도 새로 만들지 않고 연결만 다시 합니다.
- 테마 전환: 설정 창의 **Dark Mode**가 꺼지면 White, 켜지면 Black입니다. `BattleBackgroundTheme`(`Assets/Scripts/UI/Backgrounds`)이 배경 그림·움직이는 문양 머티리얼과 일반 피해 숫자 색(검정 ↔ 아이보리)을 함께 바꿉니다. 전투 HUD의 칩·아이콘은 밝은 칩 위에 있어 바꾸지 않습니다.
- 아래 1절의 Create 메뉴(새 Canvas 생성)는 이 프로젝트에서는 쓰지 않아도 됩니다. 머티리얼 조절값(4절)과 텍스처 규칙(6절)은 그대로 적용됩니다.

## 1. 설치와 적용

1. 압축 안의 **Assets/BlackHoleCelestialBattle**을 Unity 프로젝트의 Assets 아래에 복사합니다. 폴더 이름과 경로를 유지하세요.
2. Unity 스크립트 컴파일과 에셋 임포트가 끝날 때까지 기다립니다.
3. 메뉴 **Tools → Black Hole → Battle Background**를 엽니다.
4. 실제 전투 오브젝트의 표시 방식에 맞춰 다음 메뉴를 실행합니다.

| 메뉴 | 적용 대상 |
|---|---|
| Create White - World Background | SpriteRenderer 등 월드에 표시되는 적·블랙홀 뒤의 화이트 배경 |
| Create Black - World Background | SpriteRenderer 등 월드에 표시되는 적·블랙홀 뒤의 블랙 배경 |
| Create White - UI Canvas | 선택한 Canvas 또는 화면 RectTransform 안의 화이트 배경 |
| Create Black - UI Canvas | 선택한 Canvas 또는 화면 RectTransform 안의 블랙 배경 |
| Selected Background - White / Black | 생성한 BattleBackground 또는 그 자식을 선택하고 테마 교체 |

메뉴 실행 시 **M_BattleWhite.mat / M_BattleBlack.mat 두 머티리얼을 생성하고 텍스처를 연결**합니다. 이후에는 머티리얼을 직접 조절할 수 있습니다. 이미 존재하는 머티리얼 설정은 테마 메뉴가 덮어쓰지 않습니다.

### 월드 전투 배경

World Background 메뉴는 `MainCamera` 태그가 있는 카메라를 사용합니다. 별도의 **Screen Space - Camera Canvas**를 만들고, Plane Distance를 카메라 클리핑 구간의 뒤쪽 90% 위치로, Sorting Layer를 Default, Order를 -1000으로 설정합니다. 이 기본값은 실제 씬에서 확인해야 합니다.

- 배경은 적·블랙홀보다 뒤에 있어야 합니다. 게임이 별도 Sorting Layer를 사용한다면 생성한 Canvas에도 적보다 뒤쪽의 Sorting Layer를 지정하세요.
- 배경 오브젝트의 Layer가 카메라 Culling Mask에 포함되어 있어야 합니다.
- 멀티 카메라/카메라 스택을 사용하는 프로젝트에서는 실제 전투 카메라를 Canvas의 Render Camera로 지정하세요.
- Screen Space - Overlay의 불투명 배경은 월드의 적을 가릴 수 있습니다. 월드 전투에는 위 World Background 메뉴부터 사용하세요.

### UI 배경

UI Canvas 메뉴는 선택한 부모의 첫 번째 자식으로 배경을 만듭니다. 아무것도 선택하지 않으면 미리보기용 Overlay Canvas를 생성합니다. 이미 불투명한 배경 Image가 있다면 서로 가리지 않도록 기존 배경을 끄거나 순서를 조절하세요.

생성된 배경을 프리팹으로 저장하고, 프로젝트의 전투 화면 수명에 맞춰 활성화·비활성화하세요. **기존 ScreenFlow/프리팹에 자동 연결하거나 GitHub에 커밋하는 작업은 포함하지 않았습니다.**

## 2. 구성

| 파일 | 용도 |
|---|---|
| Textures/BG_BattleWhite.png | 화이트 배경 PNG 원본 |
| Textures/BG_BattleBlack.png | 같은 배치의 블랙 배경 PNG 원본 |
| Shaders/SG_UIBattleCelestial.shadergraph | 공통 Canvas SG. 두 테마는 머티리얼 색과 강도로 구분 |
| Textures/Battle_Masks.png | 선·별·약한 주변 빛을 담은 데이터 맵, 1024² |
| Textures/Battle_Flow.png | 빛이 선을 따라 흐르도록 만든 위상 데이터 맵, 1024² |
| Textures/Battle_OverlayWhite.png | 일반 UI에서 사용할 수 있는 투명 흰색 보조 문양 |
| Runtime/BattleBackgroundDriver.cs | 머티리얼 인스턴스 관리, 화면 비율 보정, 시간, 배경 Cover, 테마 교체 API |
| Editor/BattleBackgroundSetup.cs | 텍스처 설정, 두 머티리얼 생성, 씬 배경 생성·테마 변경 메뉴 |
| Source/BuildBattleMaps.py | 같은 경로에서 Masks/Flow를 재생성하는 소스. numpy·Pillow 필요 |
| Source/Battle_Layout.svg | 기본 문양 경로·별 위치의 벡터 참고본 |
| Preview_White/Black.png 및 gif | 셰이더 수식으로 별도 계산한 미리보기, Unity 캡처 아님 |

정적 PNG에는 모서리의 옅은 호와 작은 점이 들어 있습니다. **그림에 그려진 호·점은 고정**이고, SG의 별도 얇은 오버레이가 회전·반짝임·흐르는 빛을 담당합니다. 움직이는 빛은 SG 오버레이의 경로를 따릅니다. 전체 배경 그림을 회전시키는 구성은 아닙니다.

## 3. 이전 버전에서 줄인 것

- 연결된 별자리 도형과 촘촘한 눈금을 제거했습니다.
- 애니메이션 경로는 짧은 호 2개, 별 위치는 18개로 줄였습니다. 회전·화면 범위·중앙 페이드 때문에 모든 점이 동시에 보이지는 않습니다.
- SG는 화면 중앙 직사각형 구간에서 완전히 투명해지고 가장자리로 갈수록 부드럽게 나타납니다.
- 회전은 기본 **0.2도/초**, 한 바퀴 약 30분입니다.
- 경로를 따라가는 빛은 약한 강도로 나타났다 사라집니다. 무작위 독립 유성 시스템은 아닙니다.
- 두 데이터 맵은 2048²에서 1024²로 줄였습니다.

## 4. 조절값

| 속성 | White | Black | 설명 |
|---|---:|---:|---|
| RotationSpeed | 0.2 | 0.2 | 도/초. 음수는 반대 방향 |
| LineOpacity | 0.045 | 0.035 | 움직이는 호의 강도 |
| StarOpacity | 0.14 | 0.18 | 움직이는 별점의 강도 |
| FlowOpacity | 0.24 | 0.32 | 경로를 타는 빛의 강도 |
| FlowSpeed | 0.045 | 0.045 | 초당 경로 진행량 |
| TwinkleSpeed | 1.1 | 1.1 | 별점의 반짝임 속도 |
| OverallOpacity | 1 | 1 | SG 전체 강도 |
| PatternSize | 1.8 | 1.8 | 문양 크기 / UI 높이 |
| CenterX / CenterY | 0.5 / 0.5 | 0.5 / 0.5 | 회전 중심 |

더 단순하게 하려면 LineOpacity=0으로 움직이는 선을 숨기거나, FlowOpacity=0으로 흐르는 빛을 끌 수 있습니다. `Ambient` 자식을 끄면 정적 배경 PNG만 남습니다.

Play 중에는 RawImage가 사용하는 `(Instance)` 머티리얼을 조절해야 즉시 반영됩니다. 이 변경은 Play 종료 후 사라집니다. 영구적인 설정은 Play 전에 원본 머티리얼을 편집하세요.

블랙 테마에서 순수 검정 블랙홀의 윤곽이 묻힌다면 **블랙홀 자체의 얇은 테두리나 흡수 소용돌이**로 구분하세요. 배경 PNG에는 블랙홀 그림을 넣지 않았습니다.

## 5. 시간과 런타임 테마 교체

기본값은 unscaled time이므로 Time.timeScale=0이어도 배경은 움직입니다. BattleBackgroundDriver의 Use Unscaled Time을 끄면 게임 시간에 따라 정지합니다.

게임의 설정 메뉴 등에서 테마를 바꿀 때는 생성된 드라이버를 참조하고, 프로젝트에서 연결한 텍스처와 머티리얼을 전달합니다.

```csharp
// Texture2D와 Material 참조는 Inspector 또는 기존 로더에서 연결하세요.
battleBackground.SetTheme(blackBackgroundTexture, blackBackgroundMaterial);
```

각 드라이버는 Play 중 자기 머티리얼 인스턴스를 사용하고 비활성화 시 정리합니다. 공유 머티리얼의 시간값을 수정하지 않습니다. 화면 비율 보정으로 회전하는 원이 타원으로 늘어나지 않도록 하고, 정적 배경은 비율을 유지하며 화면을 채웁니다.

## 6. SG와 텍스처 설정

SG 구조는 `UV + Time + 노출 속성 → CelestialBattle Custom Function → Base Color / Alpha`입니다. **Custom Function String 모드 안에 HLSL 계산을 포함**한 SG이며, 모든 수학 연산을 개별 노드로 분해한 버전은 아닙니다. 외부 HLSL GUID 의존은 없습니다. `CelestialBattle_Reference.hlsl.txt`는 설명용 사본이고, 그것만 편집해서는 SG가 바뀌지 않습니다.

두 데이터 맵은 같은 회전 UV로 샘플링합니다. Flow의 RG에 cos/sin 위상을 기록하여 경계에서의 위상 보간을 돕고, B는 경로 마스크, A는 경로별 시간 오프셋으로 사용합니다. Masks는 R=선, G=별 중심, B=주변 빛입니다.

설치 메뉴와 전용 임포터가 다음 설정을 적용합니다.

- 데이터 맵: **sRGB OFF**, Alpha Is Transparency OFF, Mip Maps OFF, Clamp, Bilinear, 무압축, 1024.
- 배경·투명 보조 문양: sRGB ON, Mip Maps OFF, Clamp, Bilinear, 최대 2048, 원본 비율 유지.
- 플랫폼별 Texture Import Override가 있다면 실제 빌드의 설정도 확인하세요.
- Flow를 JPG로 바꾸거나 색 보정하지 마세요. Masks/Flow 중 하나만 다른 문양으로 교체하면 빛과 선이 맞지 않습니다.
- 맵 변경은 Source/BuildBattleMaps.py를 수정해 두 맵을 함께 재생성하세요. SVG만 수정해도 PNG가 자동 갱신되지는 않습니다.
- 생성/테마 메뉴를 다시 실행하면 해당 폴더 텍스처에 위 임포트 기본값을 다시 적용합니다. 수동 최적화 설정은 메뉴 사용 후 변경하세요.

## 7. 검증 범위

기준 버전: 앞서 GitHub dev에서 확인한 Unity **6000.6.2f1 / URP 17.6.0 / uGUI 2.6.0**. 첨부 Canvas Shader Graph 구조를 재사용한 새 그래프입니다. 이전 키트와 경로·클래스명·그래프 ID를 분리했습니다.

수행: 그래프 JSON 객체/슬롯/연결 검사, PNG 형식·채널 검사, 두 테마의 셰이더 수식 미리보기 계산, ZIP 무결성 검사.

**Unity 에디터가 없는 환경이므로 Unity 임포트·C# 컴파일·SG 컴파일·실제 전투 실행·WebGL 빌드는 아직 검증하지 못했습니다.** 생성 메뉴는 SG 오류를 감지하면 생성을 중단하고 Console 메시지를 표시합니다. Console의 첫 오류, 실제 레이어 순서, 전투 가독성을 확인한 뒤 사용하세요.

별도의 수식 렌더러 미리보기와 Unity는 색 공간·Canvas 합성 때문에 차이가 있을 수 있습니다. 배경의 효과는 의도적으로 매우 약하므로 움직임 확인 시 잠시 강도나 회전 속도를 올려보세요.

두 1024² 데이터 맵이 RGBA32로 올라갈 때 합계 약 **8 MiB**, 정적 배경 메모리는 별도입니다. UI 레이어 2장과 전체 화면 합성 비용이 있으므로 최종 성능은 목표 기기에서 프로파일링해야 합니다.

## 8. 제작 기록

블랙·화이트 배경은 이미지 생성 도구로 제작했습니다. 문양·흐름 맵은 재현 가능한 수학적 경로에서 생성했습니다. 원작의 파편·UI·블랙홀 이미지는 포함하지 않았습니다.

White 생성 방향: 거의 균일한 밝은 회백색, 중앙 75% 비움, 모서리의 극히 약한 호 2개와 희소한 작은 별점, 추가 게임 오브젝트·텍스트 없음.
Black 생성 방향: White의 배치를 유지하고 거의 검정인 차콜 바탕과 낮은 대비의 회청색 선·별점으로 변경.

Unity 참고:
- https://docs.unity3d.com/Manual/urp/prebuilt-shader-graphs-urp-canvas.html
- https://docs.unity.cn/Packages/com.unity.shadergraph@17.6/manual/Custom-Function-Node.html
- https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Canvas-planeDistance.html
