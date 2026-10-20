using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace BlackHole.Unity
{
    // 모바일 진동. 레벨업 물결에 맞춰 짧고 묵직하게 한 번 울린다. 게임 상태를 바꾸지 않는다.
    // 에디터·PC에서는 아무것도 하지 않는다.
    // Android: Vibrator로 울린다(API 26 이상은 세기까지, 미만은 길이만).
    // iOS: Plugins/iOS/HapticsIOS.mm의 탭틱(Heavy)으로 울린다. (현재 해당 부분은 일부러 넣지 않은 상태여서 IOS 관련으로 하게 된다면 넣어질 예정)
    internal static class Haptics
    {
        // 진동 길이(밀리초)와 세기(1~255). Android에만 쓰인다.
        private const long DurationMs = 80;
        private const int Amplitude = 255;

        // 설정 화면의 "진동" 옵션과 연결한다.
        public static bool Enabled = true;

        public static void Play()
        {
            if (!Enabled)
                return;

#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidVibrate(DurationMs, Amplitude);
#elif UNITY_IOS && !UNITY_EDITOR
            _HapticHeavy();
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void _HapticHeavy();
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static int _sdk;
        private static bool _initialized;

        private static void AndroidVibrate(long milliseconds, int amplitude)
        {
            if (!_initialized)
            {
                _initialized = true;
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    _sdk = version.GetStatic<int>("SDK_INT");
            }

            if (_vibrator == null)
                return;

            if (_sdk >= 26)
            {
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, Mathf.Clamp(amplitude, 1, 255)))
                    _vibrator.Call("vibrate", effect);
            }
            else
            {
                _vibrator.Call("vibrate", milliseconds);
            }
        }
#endif

        // 호출되지 않는다. Unity가 이 참조를 보고 Android 빌드에 VIBRATE 권한을 자동으로 넣게 하려는 것이다.
        private static void KeepVibratePermission() => Handheld.Vibrate();
    }
}