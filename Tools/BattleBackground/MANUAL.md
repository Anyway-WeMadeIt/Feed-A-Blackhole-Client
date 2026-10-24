# 배경 세팅 매뉴얼 — 전투 배경 · 화면 배경

전투 배경과 UI 화면 4개(Title·ModeSelect·Settlement·Upgrade)의 움직이는 별빛·궤도 배경을 씬과 프리팹에 손으로 세팅하는 방법이다.
전투 배경은 메뉴 **Tools → Black Hole → Battle Background → Install in Open Scene**으로도 세팅할 수 있다(1장 끝).

## 0. 구성

| 에셋 | 위치 | 역할 |
|---|---|---|
| `BG_BattleWhite.png` · `BG_BattleBlack.png` | `Assets/Art/Backgrounds` | 전투의 정적 배경 그림 |
| `SG_UIBattleCelestial.shadergraph` | `Assets/VFX/Background` | 움직이는 별빛·궤도를 그리는 Canvas 셰이더 |
| `M_BattleWhite.mat` · `M_BattleBlack.mat` | `Assets/VFX/Background` | 위 셰이더의 테마별 값 |
| `Battle_Masks.png` · `Battle_Flow.png` | `Assets/VFX/Background` | 셰이더가 읽는 데이터 맵(그림 아님) |
| `BattleBackgroundDriver` | `Assets/Scripts/UI/Backgrounds` | 머티리얼 인스턴스, 화면비 보정, 시간, 정적 그림 채우기, 테마 교체 |
| `BattleBackgroundTheme` | `Assets/Scripts/UI/Backgrounds` | 전투 배경: 설정의 Dark Mode에 따라 그림과 머티리얼을 바꾼다 |
| `UIScreenAmbient` | `Assets/Scripts/UI/Backgrounds` | 화면 배경: UI 테마에 따라 머티리얼만 바꾼다 |
| `BattleBackgroundSetup` · `BattleBackgroundInstaller` | `Assets/Scripts/Editor/UI` | 텍스처 임포트 설정, 머티리얼 생성, Install in Open Scene 메뉴 |
| `BuildBattleMaps.py` 외 | `Tools/BattleBackground` | 데이터 맵 생성기와 참고본(5장) |

## 1. 전투 배경 (GameScene)

```
BattleBackground            Canvas
├─ CelestialBackground      BattleBackgroundDriver + BattleBackgroundTheme
│  ├─ Background            RawImage — 정적 그림
│  └─ Ambient               RawImage — 별빛·궤도 머티리얼
└─ Background_Image         예전 배경(꺼 둠)
```

1. **Canvas `BattleBackground`**
   - Render Mode: Screen Space - Camera
   - Render Camera: 씬의 `Camera`(전투 카메라)
   - Plane Distance: 900 (카메라 클리핑 범위 0.3~1000 안에서 적·블랙홀보다 뒤)
   - Sorting Layer: Default, Order in Layer: -100
2. **`CelestialBackground`**: Canvas의 첫 자식으로 빈 오브젝트(RectTransform)를 만든다. 앵커를 전체로 늘이고(Min 0,0 / Max 1,1) 네 변의 여백을 0으로 한다.
3. **`Background`**: `CelestialBackground`의 첫 자식.
   - RawImage: Texture = `BG_BattleWhite`, Material 비움, Color 흰색, Raycast Target 끔
   - 앵커 전체 늘이기, 여백 0
4. **`Ambient`**: `Background` 다음 자식(위에 그려진다).
   - RawImage: Texture 비움(흰색으로 그린다), Material = `M_BattleWhite`, Raycast Target 끔
   - 앵커 전체 늘이기, 여백 0
5. **`CelestialBackground`에 `BattleBackgroundDriver`**
   - Background = `Background`의 RawImage
   - Overlay = `Ambient`의 RawImage
   - Use Unscaled Time 켬(일시정지 중에도 움직인다)
6. **같은 오브젝트에 `BattleBackgroundTheme`**
   - Driver = 5의 드라이버
   - Light: Background = `BG_BattleWhite`, Ambient = `M_BattleWhite`
   - Dark: Background = `BG_BattleBlack`, Ambient = `M_BattleBlack`
7. **`GameBootstrap`**의 Battle Background 칸 = 6의 `BattleBackgroundTheme`
8. 예전 배경 `Background_Image`는 꺼 둔다.
9. 씬을 저장하고 Play에서 설정의 Dark Mode를 바꿔 배경이 바뀌는지 본다.

**메뉴로 할 때:** 1의 Canvas가 씬에 있으면, Install in Open Scene이 2~8을 해 준다.
- 이미 배경이 있으면 새로 만들지 않고 연결만 다시 한다.
- 실행할 때마다 4장의 임포트 설정을 텍스처 4장에 다시 적용하고 리임포트한다. 그래서 `.meta`가 바뀔 수 있다.
- 씬 저장은 직접 한다.

**동작 메모**
- `Background`의 UV Rect는 드라이버가 매 프레임 맞춘다. 그림 비율을 유지하며 화면을 채우므로 손으로 바꾸지 않는다.
- Play 중 `Ambient`는 드라이버가 만든 `(Instance)` 머티리얼을 쓴다. 그래서 Play 중에 바꾼 값은 Play가 끝나면 사라진다. 값을 영구히 바꾸려면 Play 전에 원본 `M_Battle*.mat`을 고친다.
- `GameBootstrap`이 시작할 때 설정을 `BattleBackgroundTheme.Bind`로 넘긴다. 그 뒤 Dark Mode가 바뀔 때마다 그림과 머티리얼을 바꾼다.

## 2. 화면 배경 (UIScreenAmbient)

Title·ModeSelect·Settlement·Upgrade 프리팹에 들어 있다. 새 화면에 붙일 때도 같은 순서로 한다.

```
TitleScreen                 화면 루트
├─ Background_Image         Image — 정적 그림(ImageBindings가 테마별로 바꾼다)
├─ Ambient                  RawImage + BattleBackgroundDriver + UIScreenAmbient
└─ SafeAreaRoot …
```

1. 정적 그림(`Background_Image`) 바로 다음 형제로 **`Ambient`**를 만든다.
   - 이름은 반드시 `Ambient`여야 한다. 화면이 `Refs` enum 이름으로 찾기 때문이다.
2. RawImage: Texture 비움, Material = `M_BattleWhite`, Raycast Target 끔, 앵커 전체 늘이기, 여백 0
3. `BattleBackgroundDriver`: Background 비움(정적 그림은 Image가 맡는다), Overlay = 2의 RawImage, Use Unscaled Time 켬
4. `UIScreenAmbient`: Driver = 3의 드라이버, Light = `M_BattleWhite`, Dark = `M_BattleBlack`
5. 화면 코드(`TitleScreen` 참고)
   ```csharp
   public enum Refs { /* … */ Ambient, /* … */ }

   private UIScreenAmbient Ambient => View.Component<UIScreenAmbient>(Refs.Ambient);

   protected override void OnPresented(in UIAppliedTheme theme) => Ambient?.Apply(theme.ThemeId);
   ```

## 3. 머티리얼

`M_BattleWhite`·`M_BattleBlack`은 Shader `SG_UIBattleCelestial`을 쓴다. Masks = `Battle_Masks`, Flow = `Battle_Flow`.

- 두 머티리얼을 지웠다면 Install in Open Scene이 아래 값으로 다시 만든다. 이미 있으면 덮어쓰지 않는다.
- 손으로 만들 때는 아래 표대로 넣는다. 표에 없는 속성은 셰이더 기본값을 그대로 둔다.

| 속성 | White | Black | 뜻 |
|---|---:|---:|---|
| LineOpacity | 0.045 | 0.035 | 움직이는 호의 강도 |
| StarOpacity | 0.14 | 0.18 | 별점의 강도 |
| FlowOpacity | 0.24 | 0.32 | 경로를 타는 빛의 강도 |
| LineColor | (0.36, 0.42, 0.49) | (0.34, 0.40, 0.49) | 호 색 |
| StarColor | (0.42, 0.50, 0.60) | (0.70, 0.78, 0.88) | 별점 색 |
| RotationSpeed | 0.2 | 0.2 | 도/초. 음수는 반대 방향 |
| FlowSpeed | 0.045 | 0.045 | 초당 경로 진행량 |
| TwinkleSpeed | 1.1 | 1.1 | 반짝임 속도 |
| OverallOpacity | 1 | 1 | 전체 강도 |
| PatternSize | 1.8 | 1.8 | 문양 크기 / 화면 높이 |
| CenterX · CenterY | 0.5 · 0.5 | 0.5 · 0.5 | 회전 중심 |

- `Aspect`·`Clock`·`UseExternalClock`은 드라이버가 매 프레임 넣으므로 손대지 않는다.
- 움직이는 선을 숨기려면 LineOpacity = 0, 흐르는 빛을 끄려면 FlowOpacity = 0으로 한다.

## 4. 텍스처 임포트 설정

| 텍스처 | sRGB | Alpha Is Transparency | 최대 크기 | 공통 |
|---|---|---|---|---|
| `BG_BattleWhite` · `BG_BattleBlack` | 켬 | 켬 | 2048 | Default 타입, Mip Map 끔, Clamp, Bilinear, 무압축, Non-Power of 2 = None, Read/Write 끔 |
| `Battle_Masks` · `Battle_Flow` | **끔** | 끔 | 1024 | 〃 |

- 위 경로로 새로 들여올 때(`.meta`가 없을 때)는 `BattleTextureImporter`가 자동으로 설정한다.
- 이미 있는 텍스처는 Install in Open Scene이 다시 적용한다.
- 데이터 맵을 JPG로 바꾸거나 색 보정을 하지 않는다. Masks와 Flow는 짝이라 하나만 바꾸면 빛과 선이 어긋난다.

## 5. 데이터 맵 다시 만들기

```sh
python Tools/BattleBackground/BuildBattleMaps.py   # 저장소 루트에서, numpy·Pillow 필요
```

- 출력: `Assets/VFX/Background/Battle_Masks.png`·`Battle_Flow.png`, `Tools/BattleBackground/Battle_Layout.svg`
- 문양(호 2개, 별 18개)은 이 스크립트 안의 경로로 정한다. svg는 결과를 보기 위한 참고본이라, svg를 고쳐도 PNG는 바뀌지 않는다.
- `Tools/BattleBackground/CelestialBattle_Reference.hlsl.txt`는 셰이더 그래프의 Custom Function에 들어 있는 HLSL을 설명용으로 옮긴 사본이다. 이 파일을 고쳐도 셰이더는 바뀌지 않는다.
- 데이터 맵 2장은 RGBA32로 약 8 MiB를 쓰고, 전체 화면 UI 레이어 2장을 합성한다. 성능은 목표 기기에서 확인한다.
