using System;
using System.Collections.Generic;

namespace BlackHole.Core
{
    // Level업의 성장 공급을 시간차를 두고 내보내는 예약표(World.Step 5·6).
    // Level·EXP·이정표·성장 시간은 지금처럼 Step 4에서 즉시 정해진다(서버의 levelForExp 계약, 이정표 종료, 치트가 그 즉시성에 기댄다).
    // 늦추는 것은 적의 "생성"과 화면의 "연출"뿐이다.
    // - Level이 n 오르면 바퀴(Wheel) n개를 예약한다. 바퀴 하나는 게이지가 한 바퀴 도는 LevelGaugeSeconds 동안이고, 앞 바퀴의 끝에 이어 붙는다
    //   (시작 시각 = max(지금, 마지막 바퀴의 끝)). 앞 연출이 끝나기 전에 또 Level이 올라도 겹치지 않고 줄을 선다.
    // - 바퀴가 끝나는 시각(게이지가 찬 순간)부터 그 Level의 성장 공급을 SpawnInterval 간격으로 한 마리씩 요청한다. 바퀴가 겹치면 앞 바퀴의 개체가 먼저다.
    //   한 바퀴 안의 순서(Order): 종류별로 몰아서(소행성 전부 → 행성 전부 → 별) 내보내면 어색하므로 종류를 섞는다.
    //   종류마다 개체를 바퀴 길이에 고르게 펼치고(k번째 = (k − ½) ÷ 그 종류의 수), 성장 공급 목록의 뒤쪽 종류(높은 단계)일수록
    //   KindStagger씩 뒤로 민다 — 낮은 단계가 먼저 깔리되 높은 단계가 그 사이로 섞여 들어온다. 난수 없이 판마다 같은 순서다(결정성).
    //   예: 소행성 6·행성 3·별 1 → 소,소,행,소,소,행,소,별,소,행 (별은 뒤쪽이지만 마지막은 아니다).
    // - 화면은 PendingWheels·WheelProgress를 읽어 게이지와 블랙홀을 그린다: 보여줄 Level = Hq.Level − PendingWheels,
    //   바퀴가 도는 중이면 진행도 = WheelProgress, 아니면 Hq.Progress. Hq.Level이 먼저 올라 있고 화면이 뒤따른다.
    // - 재생성·시작 공급·치트 소환은 여기를 거치지 않는다(즉시).
    // - 이정표에 닿은 Step은 5·6을 건너뛰므로 그 Level의 바퀴는 예약되지 않는다. 판이 끝나면 남은 예약은 버린다(Clear) — 어차피 치워질 적이다.
    // - 느린 기기에서 한 Step에 여러 시각이 지나면 그만큼 한 번에 나온다. 판 시간 기준이라 어느 기기든 같은 시각에 같은 수가 나온다(결정성).
    //   Step당 생성 상한은 두지 않는다 — 두면 느린 기기일수록 적이 늦게 나와 기기별로 밸런스가 갈린다.
    // 시간은 World.Step 1에서 delta만큼 흐른다(Advance). 이번 Step에 예약한 바퀴도 이번 Step의 delta는 이미 흘러 있으므로 다음 Step부터 돈다.
    public sealed class GrowthPacer
    {
        // 게이지가 한 바퀴 도는 시간(초) — Level 하나의 연출 길이이자 그 Level의 성장 공급이 나오기 시작하는 지연. 실기 테스트의 출발값.
        public const float LevelGaugeSeconds = 0.3f;
        // 한 바퀴의 성장 공급 안에서 개체 사이의 간격(초). 실기 테스트의 출발값.
        public const float SpawnInterval = 0.03f;
        // 한 바퀴 안에서 성장 공급 목록의 다음 종류가 앞 종류보다 뒤로 밀리는 정도(바퀴 길이 = 1 기준).
        private const float KindStagger = 0.2f;
        private const float TimeEpsilon = 1e-5f;

        // 예약된 바퀴 하나. _order를 앞에서부터 한 마리씩 내보낸다.
        private sealed class Wheel
        {
            public float StartAt;
            public float ReleaseAt => StartAt + LevelGaugeSeconds;
            public int Spawned;
        }

        // 한 바퀴의 성장 공급을 내보내는 순서(종류를 섞은 것). 판 동안 같다.
        private readonly IReadOnlyList<EnemyDefinition> _order;
        private readonly List<Wheel> _wheels = new();
        private float _now;
        // 마지막으로 예약한 바퀴의 끝. 다음 바퀴는 이 뒤에 붙는다.
        private float _lastEnd;

        internal GrowthPacer(IReadOnlyList<SupplyRequest> growthSupply)
        {
            if (growthSupply == null) throw new ArgumentNullException(nameof(growthSupply));
            _order = OrderOf(growthSupply);
        }

        // 한 바퀴의 순서: (종류 순번 × KindStagger + (k − ½) ÷ 그 종류의 수)가 작은 것부터. 같으면 목록 앞 종류가 먼저(안정 정렬).
        internal static IReadOnlyList<EnemyDefinition> OrderOf(IReadOnlyList<SupplyRequest> growthSupply)
        {
            var slots = new List<(float Key, int Kind, EnemyDefinition Enemy)>();

            for (int kind = 0; kind < growthSupply.Count; kind++)
            {
                SupplyRequest request = growthSupply[kind];

                for (int k = 1; k <= request.Count; k++)
                    slots.Add((kind * KindStagger + (k - 0.5f) / request.Count, kind, request.Enemy));
            }

            // List.Sort는 안정 정렬이 아니라 종류 순번을 둘째 키로 넣는다.
            slots.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Kind.CompareTo(b.Kind));

            var order = new EnemyDefinition[slots.Count];

            for (int i = 0; i < slots.Count; i++)
                order[i] = slots[i].Enemy;

            return order;
        }

        // 아직 게이지가 다 차지 않은 바퀴의 수. 화면이 보여줄 Level = Hq.Level − 이 수.
        public int PendingWheels
        {
            get
            {
                int count = 0;

                for (int i = 0; i < _wheels.Count; i++)
                {
                    if (_now < _wheels[i].ReleaseAt - TimeEpsilon)
                        count++;
                }

                return count;
            }
        }

        // 바퀴가 도는 중인가(PendingWheels > 0).
        public bool IsAnimating => PendingWheels > 0;

        // 지금 도는 바퀴의 진행도(0 ~ 1). 도는 바퀴가 없으면 1.
        public float WheelProgress
        {
            get
            {
                for (int i = 0; i < _wheels.Count; i++)
                {
                    Wheel wheel = _wheels[i];

                    if (_now < wheel.ReleaseAt - TimeEpsilon)
                        return Math.Clamp((_now - wheel.StartAt) / LevelGaugeSeconds, 0f, 1f);
                }

                return 1f;
            }
        }

        // 판 시간을 흘린다(World.Step 1).
        internal void Advance(float delta) => _now += delta;

        // 오른 Level마다 바퀴 하나를 마지막 바퀴 뒤에 예약한다(World.Step 5).
        internal void Schedule(int levels)
        {
            for (int i = 0; i < levels; i++)
            {
                float startAt = Math.Max(_now, _lastEnd);
                _wheels.Add(new Wheel { StartAt = startAt });
                _lastEnd = startAt + LevelGaugeSeconds;
            }
        }

        // 시각이 된 개체를 바퀴 순서로 공급에 요청한다(World.Step 6, ProcessRequests 앞). 다 내보낸 바퀴는 지운다.
        internal void Release(EnemySupply supply)
        {
            for (int i = 0; i < _wheels.Count;)
            {
                Wheel wheel = _wheels[i];

                while (!IsDone(wheel) && wheel.ReleaseAt + wheel.Spawned * SpawnInterval <= _now + TimeEpsilon)
                    RequestNext(wheel, supply);

                if (IsDone(wheel) && _now >= wheel.ReleaseAt - TimeEpsilon)
                    _wheels.RemoveAt(i);
                else
                    i++;
            }
        }

        // 남은 예약을 전부 지금 요청한다(테스트 도구: 판을 이 Level에서 시작한 것처럼). 바퀴도 지운다.
        internal void Flush(EnemySupply supply)
        {
            foreach (Wheel wheel in _wheels)
            {
                while (!IsDone(wheel))
                    RequestNext(wheel, supply);
            }

            _wheels.Clear();
            _lastEnd = _now;
        }

        // 판 정리: 남은 예약을 버린다.
        internal void Clear()
        {
            _wheels.Clear();
            _lastEnd = _now;
        }

        private bool IsDone(Wheel wheel) => wheel.Spawned >= _order.Count;

        private void RequestNext(Wheel wheel, EnemySupply supply) =>
            supply.Request(new SupplyRequest(_order[wheel.Spawned++], 1));
    }
}
