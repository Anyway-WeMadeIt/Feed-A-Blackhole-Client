#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace BlackHole.Unity
{
    // 한 프레임의 측정값. 읽지 못한 값은 시간이면 NaN, 개수면 -1이다.
    internal struct PerfSample
    {
        // 앞 프레임과의 실제 간격(ms). 플레이어가 느끼는 프레임 시간이다(VSync 대기 포함).
        public float DeltaMs;
        // 메인 스레드가 일한 시간(ms) = 메인 스레드 프레임 시간 − Present 대기. FrameTimingManager 값이라 몇 프레임 늦게 들어온다.
        public float MainMs;
        public float RenderMs;
        public float GpuMs;
        public long GcBytes;
        public long GcCount;
        public long DrawCalls;
        public long Batches;
        public long SetPass;
        public int Alive;
        public int LevelUps;
    }

    // 여러 프레임의 합계·최대. HUD(0.5초)와 CSV 한 줄(1초), 판 전체 평균에 같이 쓴다.
    internal sealed class PerfWindow
    {
        public int Frames;
        public double SumDelta;
        public float MaxDelta;
        public double SumMain;
        public float MaxMain;
        public int MainFrames;
        public double SumRender;
        public int RenderFrames;
        public double SumGpu;
        public float MaxGpu;
        public int GpuFrames;
        public long GcBytes;
        public long GcCount;
        public int GcFrames;
        public double SumDraw;
        public double SumBatches;
        public double SumSetPass;
        public int RenderStatFrames;
        public int LastAlive;
        public int MaxAlive;
        public int LevelUps;

        public void Reset()
        {
            Frames = 0;
            SumDelta = 0;
            MaxDelta = 0;
            SumMain = 0;
            MaxMain = 0;
            MainFrames = 0;
            SumRender = 0;
            RenderFrames = 0;
            SumGpu = 0;
            MaxGpu = 0;
            GpuFrames = 0;
            GcBytes = 0;
            GcCount = 0;
            GcFrames = 0;
            SumDraw = 0;
            SumBatches = 0;
            SumSetPass = 0;
            RenderStatFrames = 0;
            LastAlive = 0;
            MaxAlive = 0;
            LevelUps = 0;
        }

        public void Add(in PerfSample s)
        {
            Frames++;
            SumDelta += s.DeltaMs;
            if (s.DeltaMs > MaxDelta)
                MaxDelta = s.DeltaMs;

            if (!float.IsNaN(s.MainMs))
            {
                SumMain += s.MainMs;
                MainFrames++;
                if (s.MainMs > MaxMain)
                    MaxMain = s.MainMs;
            }

            if (!float.IsNaN(s.RenderMs))
            {
                SumRender += s.RenderMs;
                RenderFrames++;
            }

            if (!float.IsNaN(s.GpuMs))
            {
                SumGpu += s.GpuMs;
                GpuFrames++;
                if (s.GpuMs > MaxGpu)
                    MaxGpu = s.GpuMs;
            }

            if (s.GcBytes >= 0)
            {
                GcBytes += s.GcBytes;
                GcCount += s.GcCount > 0 ? s.GcCount : 0;
                GcFrames++;
            }

            if (s.Batches >= 0)
            {
                SumDraw += s.DrawCalls;
                SumBatches += s.Batches;
                SumSetPass += s.SetPass;
                RenderStatFrames++;
            }

            LastAlive = s.Alive;
            if (s.Alive > MaxAlive)
                MaxAlive = s.Alive;

            LevelUps += s.LevelUps;
        }

        public double Fps => SumDelta > 0 ? Frames * 1000.0 / SumDelta : double.NaN;
        public double AvgDelta => Frames > 0 ? SumDelta / Frames : double.NaN;
        public double AvgMain => MainFrames > 0 ? SumMain / MainFrames : double.NaN;
        public double AvgRender => RenderFrames > 0 ? SumRender / RenderFrames : double.NaN;
        public double AvgGpu => GpuFrames > 0 ? SumGpu / GpuFrames : double.NaN;
        public double MaxMainOrNaN => MainFrames > 0 ? MaxMain : double.NaN;
        public double MaxGpuOrNaN => GpuFrames > 0 ? MaxGpu : double.NaN;
        public double GcKb => GcFrames > 0 ? GcBytes / 1024.0 : double.NaN;
        public double GcKbPerFrame => GcFrames > 0 ? GcBytes / 1024.0 / GcFrames : double.NaN;
        public double AvgDraw => RenderStatFrames > 0 ? SumDraw / RenderStatFrames : double.NaN;
        public double AvgBatches => RenderStatFrames > 0 ? SumBatches / RenderStatFrames : double.NaN;
        public double AvgSetPass => RenderStatFrames > 0 ? SumSetPass / RenderStatFrames : double.NaN;
    }

    // 프레임 시간 분포. 0.25ms 칸 1000개(0~250ms)에 세어 두고 백분위를 구한다. 판 길이와 관계없이 메모리가 고정이고 할당이 없다.
    internal sealed class PerfHistogram
    {
        private const float BucketMs = 0.25f;
        private const int Buckets = 1000;

        private readonly int[] _counts = new int[Buckets + 1];
        private int _total;
        private float _max;

        public void Reset()
        {
            System.Array.Clear(_counts, 0, _counts.Length);
            _total = 0;
            _max = 0;
        }

        public void Add(float ms)
        {
            if (float.IsNaN(ms) || ms < 0)
                return;

            int index = (int)(ms / BucketMs);
            _counts[index < Buckets ? index : Buckets]++;
            _total++;
            if (ms > _max)
                _max = ms;
        }

        // p(0~1) 백분위. 칸의 위쪽 경계를 돌려준다(0.25ms 단위로 올림). 표본이 없으면 NaN.
        public double Percentile(double p)
        {
            if (_total == 0)
                return double.NaN;

            int target = (int)System.Math.Ceiling(p * _total);
            if (target < 1)
                target = 1;

            int seen = 0;
            for (int i = 0; i < Buckets; i++)
            {
                seen += _counts[i];
                if (seen >= target)
                    return System.Math.Min((i + 1) * BucketMs, _max);
            }

            return _max;
        }

        public double Max => _total > 0 ? _max : double.NaN;

        // 이 값(ms)보다 오래 걸린 프레임 수.
        public int CountOver(float ms)
        {
            int from = (int)(ms / BucketMs);
            int count = 0;
            for (int i = from; i <= Buckets; i++)
                count += _counts[i];

            return count;
        }
    }
}
#endif
