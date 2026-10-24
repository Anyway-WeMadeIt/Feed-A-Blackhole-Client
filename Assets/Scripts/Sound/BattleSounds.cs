using UnityEngine;

namespace BlackHole.Unity
{
    // 전투 소리. 전투 화면 객체(BreakerView·EnemyView·DeathEffectView·BattleSystem·GameHost)가 생성자로 받는다.
    // 타격·파괴는 요청만 받아 두고 프레임 끝(FlushFrame)에 한 번 낸다 — 같은 프레임이면 파괴음만 들린다.
    // SoundPlayer가 없으면(소리 없이 실행) 모든 호출이 아무것도 하지 않는다.
    public sealed class BattleSounds
    {
        private readonly SoundPlayer _player;
        private readonly UISoundSetup _clips;
        private bool _hitRequested;
        private bool _destroyedRequested;

        public BattleSounds(SoundPlayer player)
        {
            _player = player;
            _clips = player != null ? player.Clips : null;
        }

        // 타격음 요청. 같은 프레임에 파괴음 요청이 있으면 파괴음만 낸다.
        public void RequestHit() => _hitRequested = true;

        // 파괴음 요청.
        public void RequestDestroyed() => _destroyedRequested = true;

        // 프레임의 요청을 소리로 낸다. GameHost가 매 프레임 끝에 부른다.
        public void FlushFrame()
        {
            if (_destroyedRequested) Play(_clips?.Destroyed);
            else if (_hitRequested) Play(_clips?.Hit);

            _hitRequested = false;
            _destroyedRequested = false;
        }

        public void PlayWhiff() => Play(_clips?.Whiff);
        public void PlayLevelUp() => Play(_clips?.LevelUp);
        public void PlayLightning() => Play(_clips?.Lightning);
        public void PlayLaser() => Play(_clips?.Lazer);
        public void PlayMoonComet() => Play(_clips?.MoonComet);
        public void PlaySuperNova() => Play(_clips?.SuperNova);

        private void Play(AudioClip clip)
        {
            if (_player != null)
                _player.Play(clip);
        }
    }
}
