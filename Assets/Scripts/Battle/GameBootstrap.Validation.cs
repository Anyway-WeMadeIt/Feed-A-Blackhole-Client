using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlackHole.Unity
{
    // GameBootstrap의 연결 검사. 빠진 연결은 에디터에서 인스펙터 값을 바꿀 때(OnValidate)와 메뉴(Tools/Black Hole/Check Game Bootstrap)에서
    // 경고로 보이고, 실행 때는 Awake가 같은 목록으로 조립을 멈춘다. 조립 코드(GameBootstrap.cs)에는 검사가 없다.
    public sealed partial class GameBootstrap
    {
        private static readonly Type[] RequiredViews =
        {
            typeof(TitleScreen),
            typeof(UpgradeScreen),
            typeof(BattleScreen),
            typeof(SettlementScreen),
            typeof(NodeTreeView),
            typeof(ModeSelectPanel),
            typeof(SettingsPanel),
            typeof(PausePanel),
            typeof(ConfirmPanel),
        };

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 프리팹 편집 중이거나 플레이 중에는 조용히 둔다. 씬의 객체를 인스펙터에서 바꿀 때만 알린다.
            if (Application.isPlaying || !gameObject.scene.IsValid())
                return;

            ReportLinks(warnOnly: true);
        }

        [UnityEditor.MenuItem("Tools/Black Hole/Check Game Bootstrap")]
        private static void CheckInOpenScene()
        {
            GameBootstrap bootstrap = FindAnyObjectByType<GameBootstrap>(FindObjectsInactive.Include);

            if (bootstrap == null)
                Debug.LogError("[조립 검사] 열린 씬에 GameBootstrap이 없다.");
            else
                bootstrap.ReportLinks(warnOnly: false);
        }

        private void ReportLinks(bool warnOnly)
        {
            List<string> missing = MissingLinks();
            List<string> warnings = LinkWarnings();

            if (missing.Count == 0 && warnings.Count == 0)
            {
                if (!warnOnly)
                    Debug.Log("[조립 검사] GameBootstrap: 문제 없음.", this);
                return;
            }

            if (missing.Count > 0)
            {
                string text = "[조립 검사] GameBootstrap에 연결하지 않은 것이 있다:\n- " + string.Join("\n- ", missing);
                if (warnOnly) Debug.LogWarning(text, this);
                else Debug.LogError(text, this);
            }

            if (warnings.Count > 0)
                Debug.LogWarning("[조립 검사] GameBootstrap:\n- " + string.Join("\n- ", warnings), this);
        }
#endif

        // 조립에 꼭 필요한데 비어 있는 연결. 비어 있으면 실행하지 않는다.
        private List<string> MissingLinks()
        {
            var missing = new List<string>();

            // 외형. 머티리얼까지 있어야 그린다.
            if (_breakerLook == null || _breakerLook.Material == null || _breakerLook.OrbMaterial == null || _breakerLook.CometAuraMaterial == null)
                missing.Add("Breaker 외형(BreakerLook)과 그 링·버프 구체·혜성 배경 원 머티리얼");
            if (_explosionLook == null || _explosionLook.Material == null)
                missing.Add("폭발 외형(ExplosionLook)과 머티리얼");
            if (_cometLook == null || _cometLook.Material == null)
                missing.Add("혜성 외형(CometLook)과 머티리얼");
            if (_lightningLook == null || _lightningLook.Material == null)
                missing.Add("번개 외형(LightningLook)과 머티리얼");
            if (_starLightningLook == null || _starLightningLook.Material == null)
                missing.Add("전기 별 번개 외형(StarLightningLook)과 머티리얼");
            if (_laserLook == null || _laserLook.Material == null)
                missing.Add("레이저 외형(LaserLook)과 머티리얼");

            // UI.
            if (_screenTransitionLook == null || _screenTransitionLook.Material == null)
                missing.Add("화면 전환 외형(ScreenTransitionLook)과 머티리얼");
            if (_rootLayer == null || _panelLayer == null)
                missing.Add("Root Layer와 Panel Layer");

            foreach (Type type in RequiredViews)
            {
                if (_views == null || !Array.Exists(_views, view => view != null && view.GetType() == type))
                    missing.Add($"Registered Views의 {type.Name}");
            }

            if (_presentations == null)
            {
                missing.Add("화면 Presentation 묶음(ScreenPresentations)");
            }
            else
            {
                foreach (string slot in _presentations.MissingReferences())
                    missing.Add($"ScreenPresentations의 {slot} 칸");
            }

            if (_fonts == null)
                missing.Add("글꼴 묶음(UIFontSet)");
            if (_palette == null || _sprites == null)
                missing.Add("팔레트(UIPalette)와 그림 짝(UISpriteSet)");

            return missing;
        }

        // 실행은 되지만 어긋난 연결. 경고로만 알린다.
        private List<string> LinkWarnings()
        {
            var warnings = new List<string>();

            if (_sound == null)
                warnings.Add("SoundPlayer가 없어 소리 없이 실행한다.");

            // Theme가 다른 글꼴 묶음·팔레트를 가리키면 같은 화면 안에서 TextBindings 글자와 나머지 글자가 갈린다.
            if (_presentations != null && _fonts != null && _palette != null)
            {
                var others = new List<string>();

                foreach (ThemeSpec theme in _presentations.Themes())
                {
                    if (theme != null && (theme.fonts != _fonts || theme.palette != _palette) && !others.Contains(theme.name))
                        others.Add(theme.name);
                }

                if (others.Count > 0)
                    warnings.Add($"이 Theme들은 GameBootstrap과 다른 글꼴 묶음 또는 팔레트를 가리킨다: {string.Join(", ", others)}. 같은 UIFontSet·UIPalette를 연결한다.");
            }

            return warnings;
        }
    }
}
