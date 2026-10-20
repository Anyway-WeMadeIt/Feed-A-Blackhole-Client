using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 사망 효과의 화면. 매 프레임 판의 효과 기록을 읽어 번개·폭발·레이저를 그리고, 판의 지금 상태를 읽어 중력장을 그린다.
    // 게임 상태를 바꾸지 않고, 피해를 다시 계산하지 않는다
    internal sealed class DeathEffectView : IDisposable
    {
        private readonly LightningBolts _lightning;
        private readonly StarLightningBolts _starLightning;
        private readonly ExplosionRings _explosions;
        private readonly LaserBeams _lasers;
        private readonly GravityFieldRings _gravity;

        private long _drawn; // 그린 기록의 가장 큰 번호. 번개·폭발·레이저는 같은 번호 줄을 쓴다.

        public DeathEffectView(Transform parent, LightningLook lightningLook, StarLightningLook starLightningLook, ExplosionLook explosionLook, LaserLook laserLook)
        {
            _lightning = new LightningBolts(parent, lightningLook);
            _starLightning = new StarLightningBolts(parent, starLightningLook);
            _explosions = new ExplosionRings(parent, explosionLook);
            _lasers = new LaserBeams(parent, laserLook);
            _gravity = new GravityFieldRings(parent);
        }

        public void Synchronize(World world, bool paused, float delta)
        {
            if (!paused)
            {
                _lightning.Age(delta);
                _starLightning.Age(delta);
                _explosions.Age(delta);
                _lasers.Age(delta);
            }

            long drawn = _drawn;
            IReadOnlyList<LightningHit> hits = world.DeathEffects.LightningHits;
            // 연쇄: 판은 옮겨 간 순서대로 기록한다. 앞 줄기가 끝난 자리에서 시작하는 줄기는 같은 연쇄의 다음 줄기다.
            int hop = 0;
            Point2? chainEnd = null;

            for (int i = 0; i < hits.Count; i++)
            {
                if (hits[i].Sequence <= _drawn)
                    continue;

                drawn = Math.Max(drawn, hits[i].Sequence);
                hop = chainEnd.HasValue && chainEnd.Value.Equals(hits[i].From) ? hop + 1 : 0;
                chainEnd = hits[i].To;
                _lightning.Play(hits[i].From, hits[i].To, hop);
                SoundManager.Instance?.PlayLightning();
            }

            // 전기 별 번개: 기록이 따로다(StarLightningHits). 모든 적중이 같은 순간이라 홉 순서가 없다 — 한 발동의 줄기를 한꺼번에 그리고 소리는 한 번만 낸다.
            IReadOnlyList<StarLightningHit> starHits = world.DeathEffects.StarLightningHits;
            float fieldScale = world.Hq.FieldScale;
            bool starStruck = false;

            for (int i = 0; i < starHits.Count; i++)
            {
                if (starHits[i].Sequence <= _drawn)
                    continue;

                drawn = Math.Max(drawn, starHits[i].Sequence);
                _starLightning.Play(starHits[i].From, starHits[i].To, fieldScale);
                starStruck = true;
            }

            if (starStruck)
                SoundManager.Instance?.PlayLightning();

            IReadOnlyList<ExplosionBlast> explosions = world.DeathEffects.Explosions;

            for (int i = 0; i < explosions.Count; i++)
            {
                if (explosions[i].Sequence <= _drawn)
                    continue;

                drawn = Math.Max(drawn, explosions[i].Sequence);
                _explosions.Play(explosions[i].Center, explosions[i].Radius);
                SoundManager.Instance.PlaySuperNova();
            }

            IReadOnlyList<LaserBurst> lasers = world.DeathEffects.LaserBursts;

            for (int i = 0; i < lasers.Count; i++)
            {
                if (lasers[i].Sequence <= _drawn)
                    continue;

                drawn = Math.Max(drawn, lasers[i].Sequence);
                _lasers.Play(lasers[i].Start, lasers[i].End, lasers[i].Definition.Width, lasers[i].Critical, fieldScale);
                SoundManager.Instance?.PlayLazer();
            }

            _drawn = drawn;
            _lasers.ShowTelegraphs(world.DeathEffects.LaserTelegraphs, fieldScale);

            // 중력장은 한 번 일어나는 기록이 아니라 지금 상태라, 번호로 거르지 않고 매 프레임 목록 그대로 넘긴다.
            _gravity.Show(world.DeathEffects.GravityFields, paused, delta);
        }

        public void Reset()
        {
            _lightning.Reset();
            _starLightning.Reset();
            _explosions.Reset();
            _lasers.Reset();
            _gravity.Reset();
            _drawn = 0;
        }

        public void Dispose()
        {
            _lightning.Dispose();
            _starLightning.Dispose();
            _explosions.Dispose();
            _lasers.Dispose();
            _gravity.Dispose();
        }
    }
}
