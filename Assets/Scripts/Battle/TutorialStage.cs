using System;
using System.Collections.Generic;
using BlackHole.Core;

namespace BlackHole.Unity
{
    // 새 게임의 첫 화면(튜토리얼 판). 판 규칙은 원래대로이고 놓는 적, 블랙홀 성장, 카메라·블랙홀 크기만 정한다.
    // - 시작 연출 없이 블랙홀 가까이(CameraZoom)에서 머문다. 시간 제한은 없다(GameSession.BeginStaged).
    // - 이정표는 Level 3 하나다(이정표 칸 셋). 무리마다 빨간 소행성(색 등급 0, 크기 1)을 놓고, 그 무리를 다 부수면 Level이 하나 오른다.
    //     Level 0: 1개 — 블랙홀은 거의 보이지 않는다.
    //     Level 1: 2개 — 블랙홀이 커지며 보이기 시작한다.
    //     Level 2: 3개 — 마지막 칸이라 이정표 칸이 맥동한다(MilestoneRow).
    //     Level 3: 이정표 → 달성 연출 → 결산 → 업그레이드. 결산은 진행 상태에 넣지 않는다(ScreenFlow).
    //   다음 무리는 Level업 게이지 바퀴가 다 돈 뒤에 놓는다(BattleSystem) — 일반 판의 성장 공급과 같은 시각이다.
    // - 판 시작 로그를 남기지 않아 서버 진행도와 무관하다(ScreenFlow.StartTutorial).
    internal static class TutorialStage
    {
        // 카메라 배율(전장 전체 = 1). 판 시작 연출의 시작 배율과 같다 — 화면 세로 반 폭이 약 3이다.
        public const float CameraZoom = 0.3f;

        // 블랙홀 크기 표(Level, 검은 중심 반지름). 처음엔 거의 보이지 않다가 무리를 부술 때마다 커지고,
        // 이정표(Level 3)에서 일반 판의 시작 크기(Level 0 = 0.111)에 닿는다.
        public static readonly IReadOnlyList<(int Level, float Radius)> BlackHoleRadii = new[]
        {
            (0, 0.008f),
            (1, 0.05f),
            (2, 0.08f),
            (3, 0.111f),
        };

        private const int RedTier = 0;

        // 무리마다 소행성 자리(블랙홀 = 원점). [i]는 Level i에서 놓는 무리다. 배율 0.3의 화면 안, 블랙홀 둘레에.
        private static readonly Point2[][] Waves =
        {
            new Point2[] { new(1.8f, 0.7f) },
            new Point2[] { new(-1.9f, 0.6f), new(1.5f, -1.3f) },
            new Point2[] { new(1.9f, 0.9f), new(-1.8f, 0.8f), new(0.1f, -1.9f) },
        };

        // 이정표 Level(= 무리 수).
        public static int GoalLevel => Waves.Length;

        // Level level에서 놓을 무리. 없으면(이정표 Level 이상) 빈 목록.
        public static IReadOnlyList<StagedEnemy> WaveAt(EnemyContent content, int level)
        {
            if (level < 0 || level >= Waves.Length)
                return Array.Empty<StagedEnemy>();

            EnemyDefinition asteroid = AsteroidOf(content);
            Point2[] positions = Waves[level];
            var enemies = new StagedEnemy[positions.Length];

            for (int i = 0; i < enemies.Length; i++)
                enemies[i] = new StagedEnemy(asteroid, RedTier, SizeRule.Base, positions[i]);

            return enemies;
        }

        // Level n + 1에 닿는 누적 EXP = 0 ~ n번째 무리를 모두 부순 EXP(빨간 소행성 하나의 EXP × 지금까지 놓은 수).
        // 이정표는 마지막 Level 하나. 목표 잔액 0(결산 Gold 없음)·전장 배율 1. 진행 상태에 결산하지 않아 콘텐츠의 이정표와 겹치지 않는다.
        public static HqGrowthDefinition GrowthFrom(EnemyContent content)
        {
            long exp = AsteroidOf(content).Tiers[RedTier].Exp;

            if (exp <= 0)
                throw new InvalidOperationException("튜토리얼 소행성(빨강)의 EXP가 0이라 게이지가 차지 않는다.");

            var levelExp = new long[Waves.Length];
            long total = 0;

            for (int i = 0; i < Waves.Length; i++)
            {
                total += exp * Waves[i].Length;
                levelExp[i] = total;
            }

            return new HqGrowthDefinition(
                levelExp,
                new[] { new HqMilestone(GoalLevel, 0, HqGrowthDefinition.StartFieldScale) });
        }

        private static EnemyDefinition AsteroidOf(EnemyContent content)
        {
            foreach (EnemyDefinition kind in content.Enemies)
            {
                if (kind.Type == EnemyType.Asteroid)
                    return kind;
            }

            throw new InvalidOperationException("튜토리얼에 쓸 소행성이 콘텐츠에 없다.");
        }
    }
}
