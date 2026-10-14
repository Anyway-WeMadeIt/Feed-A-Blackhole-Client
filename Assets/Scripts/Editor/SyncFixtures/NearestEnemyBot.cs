using System;
using BlackHole.Core;

namespace BlackHole.EditorTools.SyncFixtures
{
    // 최소 조준 봇: 지금 조준점에서 가장 가까운 살아 있는 적의 위치를 겨냥한다. 적이 없으면 조준하지 않는다.
    // 서버 동기화 픽스처(기대값 표, 골든 로그)를 만들 때 판을 돌리는 데 쓴다. 잘 하는 봇이 아니어도 되고,
    // 처치가 쌓여 Gold로 노드를 사고 진행이 이어지기만 하면 된다. 난수를 쓰지 않아 같은 판(시드)에서는 늘 같은 결과다.
    //
    // 블랙홀 둘레를 도는 봇은 쓰지 않는다: 시작 콘텐츠의 공격(피해 1, 간격 1초, 반경 0.87)으로는 적을 거의 맞히지 못해
    // 처치 0, Gold 0에서 진행이 멈춘다.
    internal sealed class NearestEnemyBot
    {
        private Point2 _aim;

        public string Name => "nearest";

        // 새 판을 시작할 때 부른다. 조준은 블랙홀(원점)에서 시작한다.
        public void Begin() => _aim = new Point2(0, 0);

        // 이번 프레임의 조준점. delta는 쓰지 않는다(매 프레임 가장 가까운 적을 다시 고른다).
        public Point2? Aim(World world, float delta)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            Enemy nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (Enemy enemy in world.Enemies)
            {
                float dx = enemy.Position.X - _aim.X;
                float dy = enemy.Position.Y - _aim.Y;
                float distance = dx * dx + dy * dy;

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = enemy;
                }
            }

            if (nearest == null)
                return null;

            _aim = nearest.Position;
            return _aim;
        }
    }
}
