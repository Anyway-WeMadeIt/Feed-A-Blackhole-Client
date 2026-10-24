using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlackHole.Unity
{
    // 소리를 실제로 내는 쪽. 효과음·BGM AudioSource, 설정의 음량, BGM 반복·셔플, 같은 효과음의 재생 간격 제한.
    // 어떤 소리를 언제 내는지는 모른다 — 전투 소리는 BattleSounds, UI 소리는 UISounds가 클립을 골라 Play로 넘긴다.
    // 씬에 하나 두고 GameBootstrap이 연결한다(정적 Instance 없음).
    public sealed class SoundPlayer : MonoBehaviour
    {
        [Header("컴포넌트 Reset버튼 누르면 됩니다")]
        [SerializeField] private AudioSource _sfxSource;
        [SerializeField] private AudioSource _bgmSource;

        [Header("UISoundSetup을 직접 추가"), SerializeField] private UISoundSetup _soundSetup;

        // 같은 소리는 이 시간 안에 중복 재생하지 않는다.
        private const float MinInterval = 0.05f;

        private Coroutine _bgmRoutine;
        private GameSettings _settings;
        // 클립마다 마지막으로 재생한 시각.
        private readonly Dictionary<AudioClip, float> _lastPlayTime = new();

        // 클립 묶음. BattleSounds·UISounds가 여기서 클립을 고른다. 연결하지 않았으면 null.
        public UISoundSetup Clips => _soundSetup;

        private void Reset()
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length > 0) _sfxSource = sources[0];
            if (sources.Length > 1) _bgmSource = sources[1];
        }

        private void OnDestroy()
        {
            if (_settings != null) _settings.Changed -= HandleSettingChanged;
        }

        // 설정의 음량을 따르고 BGM을 시작한다. 로그인·시작 동기화가 끝난 뒤 GameBootstrap이 부른다.
        public void Bind(GameSettings settings)
        {
            if (_settings != null) _settings.Changed -= HandleSettingChanged;
            _settings = settings;
            _settings.Changed += HandleSettingChanged;

            // 게임을 켠 직후에는 로드값을 읽기만 하고 실제로는 값을 바꾼 상황이 아니라 Changed 이벤트가 안 울림.
            // 그 때 사운드 설정이 안 된 상태로 들리기 때문에 강제로 AudioSource 건들여 슬라이더에 보이는 값과 일치시킴
            ApplyVolumes();
            StartBgm();
        }

        // 효과음 하나. 같은 클립이 MinInterval 안에 또 오면 내지 않는다. null이면 아무것도 하지 않는다.
        public void Play(AudioClip clip)
        {
            if (clip == null)
                return;

            float now = Time.unscaledTime;

            if (_lastPlayTime.TryGetValue(clip, out float last) && now - last < MinInterval)
                return;

            _lastPlayTime[clip] = now;
            _sfxSource.PlayOneShot(clip);
        }

        // 재생 중인 효과음을 모두 멈춘다(효과음 소스를 통째로 멈춘다).
        public void StopSfx()
        {
            if (_sfxSource != null && _sfxSource.isPlaying)
                _sfxSource.Stop();
        }

        #region BGM 재생

        public void StartBgm()
        {
            if (_soundSetup == null || _soundSetup.BgmList.Count == 0) return;

            if (_bgmRoutine != null) StopCoroutine(_bgmRoutine);
            _bgmRoutine = StartCoroutine(BgmLoop());
        }

        // 실행할 다음 BGM
        private int NextBgmIndex(int current)
        {
            int count = _soundSetup.BgmList.Count;

            if (count <= 1) return 0; // BGM이 하나 밖에 없다면 그 BGM만 계속 재생
            if (_settings != null && _settings.IsOn(GameSettings.ShuffleMusic)) // 셔플On이면 랜덤 재생
            {
                int next = Random.Range(0, count);

                while (next == current) next = Random.Range(0, count); // 같은 곡이 연속으로 재생되지 않게 함

                return next;
            }

            return (current + 1) % count; // 셔플이 아니라면 리스트의 첫 BGM 실행
        }

        // BGM 반복 코루틴
        private IEnumerator BgmLoop()
        {
            // 게임 시작시 Shuffle이 On이면 랜덤 재생, Off면 첫 BGM부터 실행
            int index = NextBgmIndex(-1);

            while (true)
            {
                AudioClip clip = _soundSetup.BgmList[index];

                if (clip != null)
                {
                    _bgmSource.clip = clip;
                    _bgmSource.Play();
                    yield return new WaitForSecondsRealtime(clip.length); // BGM길이
                }
                else yield return null; // null 칸이면 한 프레임 쉬고 다음 곡으로

                index = NextBgmIndex(index);
            }
        }

        #endregion

        #region 사운드 조절

        private void HandleSettingChanged(string id)
        {
            if (id == GameSettings.MasterVolume || id == GameSettings.EffectsVolume || id == GameSettings.MusicVolume)
                ApplyVolumes();
        }

        private void ApplyVolumes()
        {
            float masterVolume = _settings.LevelOf(GameSettings.MasterVolume);

            _sfxSource.volume = masterVolume * _settings.LevelOf(GameSettings.EffectsVolume);
            _bgmSource.volume = masterVolume * _settings.LevelOf(GameSettings.MusicVolume);
        }

        #endregion
    }
}
