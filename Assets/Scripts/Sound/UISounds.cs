using System.Collections.Generic;
using UnityEngine;

namespace BlackHole.Unity
{
    // UI 소리. 화면 흐름(ScreenFlow)은 생성자로, 프리팹 화면은 IUISoundView.SetSounds로 받는다.
    // SoundPlayer가 없으면(소리 없이 실행) 모든 호출이 아무것도 하지 않는다.
    public sealed class UISounds
    {
        private readonly SoundPlayer _player;
        private readonly UISoundSetup _clips;
        // 다음에 낼 노드 구매 효과음 번호. 업그레이드 화면에 들어갈 때 0으로 돌린다.
        private int _nodeUpgradeIndex;

        public UISounds(SoundPlayer player)
        {
            _player = player;
            _clips = player != null ? player.Clips : null;
        }

        // 결산의 슬라이더 효과음 길이(초). 없으면 0.
        public float SliderLength => _clips?.Slider != null ? _clips.Slider.length : 0f;

        public void PlayClick() => Play(_clips?.Click);
        public void PlayHover() => Play(_clips?.Hover);
        public void PlaySlider() => Play(_clips?.Slider);
        public void PlayClosing() => Play(_clips?.Closing);
        public void PlaySwitchingScreens() => Play(_clips?.SwitchingScreens);

        // 결산 연출을 건너뛸 때 슬라이더 효과음을 멈춘다. 효과음 소스를 통째로 멈추지만 결산 중에 재생 중인 효과음은 슬라이더뿐이다.
        public void StopSlider()
        {
            if (_player != null)
                _player.StopSfx();
        }

        // 노드 구매 효과음. 살 때마다 다음 번호의 소리를 내고, 마지막에 닿으면 마지막 소리를 유지한다.
        public void PlayNodeUpgrade()
        {
            List<AudioClip> clips = _clips?.NodeUpgrade;
            if (clips == null || clips.Count == 0) return;

            // 리스트 길이가 줄어든 경우를 대비해 마지막 번호로 보정
            if (_nodeUpgradeIndex >= clips.Count) _nodeUpgradeIndex = clips.Count - 1;
            Play(clips[_nodeUpgradeIndex]);

            if (_nodeUpgradeIndex < clips.Count - 1) _nodeUpgradeIndex++;
        }

        public void ResetNodeUpgradeIndex() => _nodeUpgradeIndex = 0;

        private void Play(AudioClip clip)
        {
            if (_player != null)
                _player.Play(clip);
        }
    }
}
