using System.Collections.Generic;
using UnityEngine;

// 사운드 파일 저장 SO
[CreateAssetMenu(fileName = "UISoundSetup", menuName = "Scriptable Objects/UISoundSetup")]
public class UISoundSetup : ScriptableObject
{

    [Header("BGM"), SerializeField] private List<AudioClip> _bgmSound;
    [Header("Click"), SerializeField] private AudioClip _clickSound;
    [Header("Hover"), SerializeField] private AudioClip _hoverSound;
    [Header("Hit"), SerializeField] private AudioClip _hitSound;
    [Header("헛치는 사운드"), SerializeField] private AudioClip _whiffSound;
    [Header("Destroy"), SerializeField] private AudioClip _destroySound;
    [Header("블랙홀 레벨업"), SerializeField] private AudioClip _levelUpSound;
    [Header("Lightning"), SerializeField] private AudioClip _lightningSound;
    [Header("Razer"), SerializeField] private AudioClip _razerSound;
    [Header("달과 혜성 능력 얻을 때"), SerializeField] private AudioClip _moonCometSound;
    [Header("결산 때 나오는 사이즈 슬라이더 사운드"), SerializeField] private AudioClip _sliderSound;
    [Header("결산"), SerializeField] private AudioClip _closingSound;
    [Header("NodeUpgrade"), SerializeField] private List<AudioClip> _NodeUpgradeSound;
    [Header("화면 전환"), SerializeField] private AudioClip _switchingScreensSound;
    [Header("SuperNova"), SerializeField] private AudioClip _superNovaSound;

    public IReadOnlyList<AudioClip> BgmList { get { return _bgmSound; } }
    public AudioClip Click { get { return _clickSound; } }
    public AudioClip Hover { get { return _hoverSound; } }
    public AudioClip Hit { get { return _hitSound; } }
    public AudioClip Whiff { get { return _whiffSound; } }
    public AudioClip Destroyed { get { return _destroySound; } }
    public AudioClip LevelUp { get { return _levelUpSound; } }
    public AudioClip Lightning { get { return _lightningSound; } }
    public AudioClip Razer { get { return _razerSound; } }
    public AudioClip MoonComet { get { return _moonCometSound; } }
    public AudioClip Slider { get { return _sliderSound; } }
    public AudioClip Closing { get { return _closingSound; } }
    public List<AudioClip> NodeUpgrade { get { return _NodeUpgradeSound; } }
    public AudioClip SwitchingScreens { get { return _switchingScreensSound; } }
    public AudioClip SuperNova { get { return _superNovaSound; } }
}
