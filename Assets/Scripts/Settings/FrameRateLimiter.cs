using System;
using UnityEngine;

namespace BlackHole.Unity
{
    // FPS Limit 설정을 Application.targetFrameRate에 넣는다. 시작할 때 한 번, 설정이 바뀔 때마다.
    // - 모바일은 targetFrameRate를 정하지 않으면 30fps로 돈다. 그래서 기본값(60)도 반드시 넣는다.
    //   기기 화면이 고른 값보다 느리면(60Hz 화면에서 120) OS가 화면에 맞춰 낮춘다.
    // - 에디터·PC에서는 품질 설정의 VSync가 켜져 있으면 targetFrameRate가 무시되고 모니터 주사율을 따른다
    //   (Game 뷰의 VSync 표시도 같다). 확인은 기기나 VSync를 끈 Game 뷰에서 한다.
    internal sealed class FrameRateLimiter : IDisposable
    {
        private readonly GameSettings _settings;

        public FrameRateLimiter(GameSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _settings.Changed += HandleSettingChanged;
            Apply();
        }

        public void Dispose() => _settings.Changed -= HandleSettingChanged;

        private void HandleSettingChanged(string id)
        {
            if (id == GameSettings.FpsLimit)
                Apply();
        }

        private void Apply() => Application.targetFrameRate = _settings.FrameRateLimit;
    }
}
