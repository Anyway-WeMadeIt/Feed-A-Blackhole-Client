#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace BlackHole.Unity
{
    // 측정 한 번(판 하나, 또는 손으로 켠 기록)을 파일로 남긴다.
    // - perf_<날짜시각>_<커밋>_<라벨>.csv: 1초에 한 줄(FPS, 프레임·메인·GPU 시간, GC, 드로우콜, 살아 있는 적, 레벨업, 메모리).
    // - summary.csv: 측정이 끝날 때 한 줄(백분위, 예산 초과 프레임 수, 평균). 런북 기록표에 옮겨 적는 값이 여기 있다.
    // 위치: Application.persistentDataPath/perf. 안드로이드는 /sdcard/Android/data/<패키지>/files/perf (adb pull로 꺼낸다).
    // 기록 중에는 문자열을 만들지 않는다(줄은 StringBuilder → char[] → StreamWriter). 시작·끝에서만 할당한다.
    internal sealed class PerfLog
    {
        private const string RowHeader =
            "t_s,frames,fps,frame_avg_ms,frame_max_ms,main_avg_ms,main_max_ms,render_avg_ms,gpu_avg_ms,gpu_max_ms," +
            "gc_kb,gc_count,draw_avg,batches_avg,setpass_avg,alive,alive_max,levelups,gc_used_mb,total_used_mb";

        private const string SummaryHeader =
            "date,commit,branch,device,label,scenario,profile,aim,seed,target_fps,seconds,frames,avg_fps,p50_ms,p95_ms,p99_ms,max_ms," +
            "frames_over_20ms,frames_over_33ms,main_avg_ms,main_p95_ms,main_max_ms,render_avg_ms,gpu_avg_ms," +
            "gc_kb_per_frame,gc_kb_total,draw_avg,batches_avg,alive_max,levelups,file";

        // 줄을 디스크에 밀어 넣는 간격(줄 수). 앱이 강제로 꺼져도 최대 이만큼만 잃는다.
        private const int FlushEveryRows = 5;
        // 런북 예산: 상위 1% 프레임 20ms, 33.3ms는 2프레임 이상 놓친 프레임.
        private const float SlowFrameMs = 20f;
        private const float VerySlowFrameMs = 33.3f;
        private const float SuspendedFrameMs = 2000f;

        private readonly StringBuilder _sb = new(512);
        private char[] _chars = new char[512];
        private readonly PerfWindow _second = new();
        private readonly PerfWindow _total = new();
        private readonly PerfHistogram _frames = new();
        private readonly PerfHistogram _main = new();

        private StreamWriter _writer;
        private string _fileName;
        private string _label;
        private int _seed;
        private DateTime _startedAt;
        private double _elapsedMs;
        private double _nextRowMs;
        private int _rows;

        // 테스트 조건(요약 줄에 적는다). 테스트 패널이 넣고, 없으면 "-".
        public string Scenario { get; set; }
        public string Profile { get; set; }
        public string Aim { get; set; }

        public bool IsRecording => _writer != null;
        public string Label => _label;
        public int Seed => _seed;
        public double ElapsedSeconds => _elapsedMs / 1000.0;

        // seed가 없으면(손으로 켠 기록) -1.
        public void Begin(string label, int seed)
        {
            if (IsRecording)
                End();

            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "perf");
                Directory.CreateDirectory(directory);

                _startedAt = DateTime.Now;
                _label = label;
                _seed = seed;
                string seedPart = seed >= 0 ? "_s" + seed : "";
                _fileName = "perf_" + _startedAt.ToString("yyMMdd_HHmmss") + "_" + BuildStamp.Commit + "_" + label + seedPart + ".csv";

                _writer = new StreamWriter(Path.Combine(directory, _fileName), false, new UTF8Encoding(false), 8192);
                _writer.WriteLine(RowHeader);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Perf] CSV를 열지 못했습니다: " + e.Message);
                _writer = null;
                return;
            }

            _second.Reset();
            _total.Reset();
            _frames.Reset();
            _main.Reset();
            _elapsedMs = 0;
            _nextRowMs = 1000;
            _rows = 0;
        }

        public void Add(in PerfSample sample, long gcUsedBytes, long totalUsedBytes)
        {
            if (!IsRecording)
                return;

            // 백그라운드에 갔다 온 프레임(간격이 몇 초)은 끊김이 아니라 멈춤이다. 통계에서 뺀다.
            if (sample.DeltaMs > SuspendedFrameMs)
                return;

            _second.Add(sample);
            _total.Add(sample);
            _frames.Add(sample.DeltaMs);
            _main.Add(sample.MainMs);
            _elapsedMs += sample.DeltaMs;

            if (_elapsedMs >= _nextRowMs)
            {
                WriteRow(gcUsedBytes, totalUsedBytes);
                _nextRowMs += 1000;

                // 오래 멈췄다 돌아오면(백그라운드 등) 밀린 칸을 건너뛴다.
                if (_nextRowMs <= _elapsedMs)
                    _nextRowMs = _elapsedMs + 1000;
            }
        }

        public void Flush()
        {
            if (IsRecording)
                _writer.Flush();
        }

        // 기록을 닫고 요약을 남긴다. 기록 중이 아니면 아무것도 하지 않는다.
        public void End(long gcUsedBytes = -1, long totalUsedBytes = -1)
        {
            if (!IsRecording)
                return;

            try
            {
                if (_second.Frames > 0)
                    WriteRow(gcUsedBytes, totalUsedBytes);

                _writer.Flush();
                _writer.Dispose();
                WriteSummary();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Perf] CSV를 닫는 중 문제: " + e.Message);
            }
            finally
            {
                _writer = null;
            }
        }

        private void WriteRow(long gcUsedBytes, long totalUsedBytes)
        {
            StringBuilder sb = _sb.Clear();
            PerfWindow w = _second;

            PerfText.Fixed(sb, _elapsedMs / 1000.0, 1).Append(',');
            PerfText.Int(sb, w.Frames).Append(',');
            PerfText.Fixed(sb, w.Fps, 1).Append(',');
            PerfText.Fixed(sb, w.AvgDelta, 2).Append(',');
            PerfText.Fixed(sb, w.MaxDelta, 2).Append(',');
            PerfText.Fixed(sb, w.AvgMain, 2).Append(',');
            PerfText.Fixed(sb, w.MaxMainOrNaN, 2).Append(',');
            PerfText.Fixed(sb, w.AvgRender, 2).Append(',');
            PerfText.Fixed(sb, w.AvgGpu, 2).Append(',');
            PerfText.Fixed(sb, w.MaxGpuOrNaN, 2).Append(',');
            PerfText.Fixed(sb, w.GcKb, 2).Append(',');
            PerfText.Int(sb, w.GcCount).Append(',');
            PerfText.Fixed(sb, w.AvgDraw, 0).Append(',');
            PerfText.Fixed(sb, w.AvgBatches, 0).Append(',');
            PerfText.Fixed(sb, w.AvgSetPass, 0).Append(',');
            PerfText.Int(sb, w.LastAlive).Append(',');
            PerfText.Int(sb, w.MaxAlive).Append(',');
            PerfText.Int(sb, w.LevelUps).Append(',');
            AppendMb(sb, gcUsedBytes).Append(',');
            AppendMb(sb, totalUsedBytes);
            sb.Append('\n');

            WriteBuilder(sb);
            _second.Reset();

            if (++_rows % FlushEveryRows == 0)
                _writer.Flush();
        }

        private void WriteSummary()
        {
            string directory = Path.Combine(Application.persistentDataPath, "perf");
            string path = Path.Combine(directory, "summary.csv");
            bool header = !File.Exists(path);

            StringBuilder sb = _sb.Clear();
            sb.Append(_startedAt.ToString("yyyy-MM-dd HH:mm:ss")).Append(',');
            sb.Append(BuildStamp.Commit).Append(',');
            sb.Append(Clean(BuildStamp.Branch)).Append(',');
            sb.Append(Clean(SystemInfo.deviceModel)).Append(',');
            sb.Append(_label).Append(',');
            sb.Append(Clean(Scenario)).Append(',');
            sb.Append(Clean(Profile)).Append(',');
            sb.Append(Clean(Aim)).Append(',');
            PerfText.Int(sb, _seed).Append(',');
            PerfText.Int(sb, Application.targetFrameRate).Append(',');
            PerfText.Fixed(sb, _elapsedMs / 1000.0, 1).Append(',');
            PerfText.Int(sb, _total.Frames).Append(',');
            PerfText.Fixed(sb, _total.Fps, 1).Append(',');
            PerfText.Fixed(sb, _frames.Percentile(0.50), 2).Append(',');
            PerfText.Fixed(sb, _frames.Percentile(0.95), 2).Append(',');
            PerfText.Fixed(sb, _frames.Percentile(0.99), 2).Append(',');
            PerfText.Fixed(sb, _frames.Max, 2).Append(',');
            PerfText.Int(sb, _frames.CountOver(SlowFrameMs)).Append(',');
            PerfText.Int(sb, _frames.CountOver(VerySlowFrameMs)).Append(',');
            PerfText.Fixed(sb, _total.AvgMain, 2).Append(',');
            PerfText.Fixed(sb, _main.Percentile(0.95), 2).Append(',');
            PerfText.Fixed(sb, _main.Max, 2).Append(',');
            PerfText.Fixed(sb, _total.AvgRender, 2).Append(',');
            PerfText.Fixed(sb, _total.AvgGpu, 2).Append(',');
            PerfText.Fixed(sb, _total.GcKbPerFrame, 3).Append(',');
            PerfText.Fixed(sb, _total.GcKb, 1).Append(',');
            PerfText.Fixed(sb, _total.AvgDraw, 0).Append(',');
            PerfText.Fixed(sb, _total.AvgBatches, 0).Append(',');
            PerfText.Int(sb, _total.MaxAlive).Append(',');
            PerfText.Int(sb, _total.LevelUps).Append(',');
            sb.Append(_fileName);

            string line = sb.ToString();

            using (var writer = new StreamWriter(path, true, new UTF8Encoding(false)))
            {
                if (header)
                    writer.WriteLine(SummaryHeader);

                writer.WriteLine(line);
            }

            Debug.Log("[Perf] " + SummaryHeader + "\n[Perf] " + line);
        }

        private void WriteBuilder(StringBuilder sb)
        {
            if (_chars.Length < sb.Length)
                _chars = new char[sb.Length * 2];

            sb.CopyTo(0, _chars, 0, sb.Length);
            _writer.Write(_chars, 0, sb.Length);
        }

        private static StringBuilder AppendMb(StringBuilder sb, long bytes) =>
            bytes >= 0 ? PerfText.Fixed(sb, bytes / (1024.0 * 1024.0), 1) : sb.Append('-');

        // CSV 칸을 깨지 않게 쉼표·줄바꿈을 공백으로 바꾼다.
        private static string Clean(string value) =>
            string.IsNullOrEmpty(value) ? "-" : value.Replace(',', ' ').Replace('\n', ' ').Replace('\r', ' ');
    }
}
#endif
