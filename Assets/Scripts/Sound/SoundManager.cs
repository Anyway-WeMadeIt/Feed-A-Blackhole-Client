using System.Collections;
using System.Collections.Generic;
using BlackHole.Unity;
using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("컴포넌트 Reset버튼 누르면 됩니다")]
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioSource _bgmSource;

    [Header("SoundSO를 직접 추가"), SerializeField] private UISoundSetup _soundSetup;

    private Coroutine _bgmRoutine;
    private GameSettings _settings;
    // 클립마다 "마지막으로 재생한 시각"을 저장하는 딕셔너리, key: AudioClip / value: 시각
    private readonly Dictionary<AudioClip, float> _lastPlayTime = new Dictionary<AudioClip, float>();

    private const int _lastBgmIndex = -1;
    private int _nodeUpgradeIndex; // 다음에 재생할 노드 업그레이드 효과음 번호
    private const float MinInterval = 0.05f; // 같은 소리는 이 시간 안에 중복 재생 안 함

    private bool _playHit;
    private bool _playDestroyed;

    /// <summary>
    /// 슬라이더 SFX 재생 길이
    /// </summary>
    public float SliderLength
    {
        get
        {
            if (_soundSetup.Slider == null) return 0f;
            return _soundSetup.Slider.length;
        }
    }

    private void Reset()
    {
        AudioSource[] sources = GetComponents<AudioSource>();
        if (sources.Length > 0) _sfxSource = sources[0];
        if (sources.Length > 1) _bgmSource = sources[1];
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void LateUpdate()
    {
        // 같은 프레임이면 파괴음부터 실행되고 타격음은 안 들림
        if (_playDestroyed) Play(_soundSetup.Destroyed);
        else if (_playHit) Play(_soundSetup.Hit);

        _playHit = false;
        _playDestroyed = false;
    }

    private void OnDestroy()
    {
        if (_settings != null) _settings.Changed -= HandleSettingChanged;
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// HitSound SFX 요청, Destroyed SFX와 동시에 재생되었을 때 Destroyed SFX만 재생시키기 위함
    /// </summary>
    public void RequestHitSound()
    {
        _playHit = true;
    }

    /// <summary>
    /// Destroyed SFX 요청
    /// </summary>
    public void RequestDestroyed()
    {
        _playDestroyed = true;
    }

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

    /// <summary>
    /// 노드 업그레이드 사운드 인덱스 초기화
    /// </summary>
    public void ResetNodeUpgradeIndex()
    {
        _nodeUpgradeIndex = 0;
    }

    #region SFX 재생

    /// <summary>
    /// 사운드 재생
    /// </summary>
    /// <param name="clip">재생할 sfx 파일</param>
    private void Play(AudioClip clip)
    {
        if (clip != null)
        {
            // 같은 소리의 재생 간격 제한
            float now = Time.unscaledTime; // 현 시각
            float last; // _lastPlayTime 딕셔너리의 value로 사용될 변수

            // 이 클립의 마지막 재생 기록이 있고, 지금과의 차이가 MinInterval보다 작으면 return
            if (_lastPlayTime.TryGetValue(clip, out last) && now - last < MinInterval) return;

            _lastPlayTime[clip] = now; // 이 클립의 마지막 재생 시각을 지금 시각으로 변경
            _sfxSource.PlayOneShot(clip);
        }
    }

    /// <summary>
    /// 클릭 사운드 재생
    /// </summary>
    public void PlayClick()
    {
        Play(_soundSetup.Click);
    }

    /// <summary>
    ///  호버 사운드 재생
    /// </summary>
    public void PlayHover()
    {
        Play(_soundSetup.Hover);
    }

    /// <summary>
    /// 타격 사운드 재생
    /// </summary>
    public void PlayHit()
    {
        Play(_soundSetup.Hit);
    }

    /// <summary>
    /// 타격 못했을 때 사운드 재생
    /// </summary>
    public void PlayWhiff()
    {
        Play(_soundSetup.Whiff);
    }

    /// <summary>
    /// 행성 파괴 사운드 재생
    /// </summary>
    public void PlayDestroyed()
    {
        Play(_soundSetup.Destroyed);
    }

    /// <summary>
    /// 블랙홀 레벨업 사운드 재생
    /// </summary>
    public void PlayLevelUp()
    {
        Play(_soundSetup.LevelUp);
    }

    /// <summary>
    /// 번개 효과 사운드 재생
    /// </summary>
    public void PlayLightning()
    {
        Play(_soundSetup.Lightning);
    }

    /// <summary>
    /// 레이저 효과 사운드 재생
    /// </summary>
    public void PlayRazer()
    {
        Play(_soundSetup.Razer);
    }

    /// <summary>
    /// 달, 혜성 획득 사운드 재생
    /// </summary>
    public void PlayMoonComet()
    {
        Play(_soundSetup.MoonComet);
    }

    /// <summary>
    /// 결산에서 블랙홀 사이즈 슬라이더 사운드 재생
    /// </summary>
    public void PlaySlider()
    {
        Play(_soundSetup.Slider);
    }

    /// <summary>
    /// 결산 완료되었을 때 사운드 재생
    /// </summary>
    public void PlayClosing()
    {
        Play(_soundSetup.Closing);
    }

    /// <summary>
    /// 노드 업그레이드 사운드 재생
    /// </summary>
    public void PlayNodeUpgrade()
    {
        List<AudioClip> clips = _soundSetup.NodeUpgrade;

        if (clips == null || clips.Count == 0) return;

        // 리스트 길이가 줄어든 경우를 대비해 마지막 번호로 보정
        if (_nodeUpgradeIndex >= clips.Count) _nodeUpgradeIndex = clips.Count - 1;

        Play(clips[_nodeUpgradeIndex]);

        // 마지막에 도달하면 더 올리지 않고 마지막 소리를 유지한다
        if (_nodeUpgradeIndex < clips.Count - 1) _nodeUpgradeIndex++;
    }

    /// <summary>
    /// 화면 전환 사운드 재생
    /// </summary>
    public void PlaySwitchingScreens()
    {
        Play(_soundSetup.SwitchingScreens);
    }

    /// <summary>
    /// 슈퍼노바 사운드 재생
    /// </summary>
    public void PlaySuperNova()
    {
        Play(_soundSetup.SuperNova);
    }

    #endregion

    #region BGM 재생

    /// <summary>
    /// BGM 사운드 재생
    /// </summary>
    public void StartBgm()
    {
        if (_soundSetup.BgmList.Count == 0) return; // Bgm 리스트 비어있으면 실행 X

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
        int index = NextBgmIndex(_lastBgmIndex);

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

    private void HandleSettingChanged(string id) // id 확인
    {
        if (id == GameSettings.MasterVolume || id == GameSettings.EffectsVolume || id == GameSettings.MusicVolume)
        {
            ApplyVolumes();
        }
    }

    private void ApplyVolumes() // 사운드 조절
    {
        float masterVolume = _settings.LevelOf(GameSettings.MasterVolume);

        _sfxSource.volume = masterVolume * _settings.LevelOf(GameSettings.EffectsVolume);
        _bgmSource.volume = masterVolume * _settings.LevelOf(GameSettings.MusicVolume);
    }

    #endregion
}
