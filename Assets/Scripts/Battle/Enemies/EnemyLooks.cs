using System;
using System.Collections.Generic;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 적 종류의 외형 조회: 종류 → 외형 칸(EnemyLook)·스프라이트, (종류, 색 등급) → 색. 외형은 적 종류 에셋(EnemyKind)이 가진다.
    // 스프라이트가 없으면 외형 칸이 고른 임시 도형(EnemyShapes)으로, 목록에 없는 종류는 다각형(흰색)으로 그린다. 적 화면과 조종 콘솔이 같은 외형을 쓴다.
    // 혜성(픽업)은 일반 적과 성격이 달라 여기서 다루지 않는다. 스프라이트가 아니라 셰이더로 그리며, 외형은 CometLook이 가진다.
    internal sealed class EnemyLooks : IDisposable
    {
        // 특수 성질 표식(속 채움)이 차지하는 크기(윤곽 대비). 나머지 테두리가 원래 색으로 보인다 [임시].
        // 원작의 황금 소행성은 원래 색의 윤곽에 속이 노랗다(BATTLE_COMPOSITION_PLAN 2.1). 다른 성질도 같은 방식의 임시 표식이다.
        public const float TraitFillScale = 0.65f;

        private readonly Dictionary<EnemyType, EnemyKind> _kinds = new Dictionary<EnemyType, EnemyKind>();
        private readonly EnemyShapes _shapes = new EnemyShapes();

        public EnemyLooks(IReadOnlyList<EnemyKind> kinds)
        {
            foreach (EnemyKind kind in kinds)
            {
                if (kind != null)
                    _kinds[kind.Type] = kind;
            }
        }

        // 종류의 외형 칸. 목록에 없는 종류는 기본값(다각형, 자전 없음, 바로 지움).
        public EnemyLook LookOf(EnemyType type) =>
            _kinds.TryGetValue(type, out EnemyKind kind) ? kind.Look : default;

        // 몸체 스프라이트. 에셋의 스프라이트(여럿이면 적 번호로 고름)가 없으면 임시 도형.
        public Sprite SpriteOf(EnemyType type, int enemyId)
        {
            EnemyLook look = LookOf(type);
            Sprite[] sprites = look.sprites;

            if (sprites != null && sprites.Length > 0)
            {
                Sprite sprite = sprites[(enemyId - 1) % sprites.Length];

                if (sprite != null)
                    return sprite;
            }

            return _shapes.Of(look.shape, enemyId);
        }

        // 성질 표식의 모양. 에셋의 표식 스프라이트 → 표식 도형 → 몸체 스프라이트(body) 순.
        public Sprite MarkerSpriteOf(EnemyType type, Sprite body, int enemyId)
        {
            EnemyLook look = LookOf(type);

            if (look.markerSprite != null)
                return look.markerSprite;

            return look.markerShape != EnemyShape.None ? _shapes.Of(look.markerShape, enemyId) : body;
        }

        public Color ColorOf(EnemyType type, int tier) =>
            _kinds.TryGetValue(type, out EnemyKind kind) ? kind.ColorOf(tier) : Color.white;

        // 색 등급과 톤(Enemy.Tone)의 색. 몸체·피격·사망 연출이 기록의 (Tier, Tone)으로 불러 같은 색을 쓴다.
        public Color ColorOf(EnemyType type, int tier, int tone) =>
            _kinds.TryGetValue(type, out EnemyKind kind) ? kind.ColorOf(tier, tone) : Color.white;

        // 성질의 표식 색. 없는 종류·성질은 투명이다(표식을 그리지 않는다).
        public Color TraitColorOf(EnemyType type, EnemyTraitType? trait) =>
            trait.HasValue && _kinds.TryGetValue(type, out EnemyKind kind) ? kind.TraitColorOf(trait.Value) : Color.clear;

        public void Dispose() => _shapes.Dispose();
    }
}
