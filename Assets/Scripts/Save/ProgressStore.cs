using System;
using System.Collections.Generic;
using System.IO;
using BlackHole.Core;
using BlackHole.Net;
using BlackHole.Sync;
using UnityEngine;

namespace BlackHole.Save
{
    // 진행 저장의 창구. 앱 시작 때 콘텐츠를 불러온 뒤 한 번 불러 검사한다.
    // 불러오기: 본 파일 → 읽지 못하거나 깨졌으면 보관하고 직전 저장 → 그것도 그렇으면 보관하고 이어 할 저장이 없다.
    // 저장: 새 게임·계속을 고른 뒤, 진행 상태가 바뀔 때마다(노드 구매, 결산)와 앱이 내려갈 때.
    // 고르기 전에는 저장하지 않는다 — 타이틀에서 앱이 내려가도 이전 저장을 덮지 않는다. 예외는 서버 응답(ApplyResponse)이다.
    //
    // 서버 동기화(저장 포맷 2): 진행도와 로그 장부(SyncLedger)를 한 파일에 한 번의 원자적 쓰기로 저장한다.
    // 진행도를 바꾸는 일(노드 구매, 판 시작·결산, 새 게임)은 Commit(StartNew)으로 로그를 더하며 저장하므로,
    // 진행도만 저장되고 로그가 빠지는 일이 없다. 서버 응답은 ISyncStore.ApplyResponse로 반영한다.
    public sealed class ProgressStore : ISyncStore, IProgressCommitter
    {
        private readonly ProgressSaveFile _file;
        private readonly ProgressState _progress;
        private readonly NodeContent _nodes;
        private readonly HqGrowthDefinition _growth;
        private readonly Func<DateTime> _utcNow;
        private SyncLedger _ledger = new();
        // 이어 한 저장. 다음 저장 때 게임에 쓰지 않은 원래 값을 되돌려 적는다. 새 게임이면 null.
        private SavedProgress _basis;
        // 이번 실행에서 새 게임·계속을 골랐는가.
        private bool _started;
        // 테스트 세션: 테스트 도구가 진행 상태를 시나리오로 바꿨다. 이번 실행은 저장하지 않고 로그도 만들지 않는다(저장 파일을 지킨다).
        private bool _testSession;

        // 앱 시작 때 불러온 저장. 없으면 null.
        public SavedProgress Saved { get; private set; }

        // 계속할 진행이 있는가: 불러온 저장이 있거나, 이번 실행에서 이미 진행 중이다.
        public bool CanContinue => _started || Saved != null;

        public bool IsTestSession => _testSession;

        // ---- 동기화(ISyncStore) ----

        public IReadOnlyList<PendingLog> Pending => _ledger.Pending;

        public event Action LogAppended;

        public RejectedLogDto LastRejection => _ledger.LastRejection;

        public bool LastRejectionShown => _ledger.LastRejectionShown;

        private ProgressStore(ProgressSaveFile file, ProgressState progress, NodeContent nodes, HqGrowthDefinition growth, Func<DateTime> utcNow)
        {
            _file = file;
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _nodes = nodes;
            _growth = growth;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public static ProgressStore Load(string directory, NodeContent nodes, HqGrowthDefinition growth, ProgressState progress, Func<DateTime> utcNow = null)
        {
            var file = new ProgressSaveFile(directory);
            var store = new ProgressStore(file, progress, nodes, growth, utcNow);

            if (file.TryReadCurrent(out ProgressSaveData current) && store.TryUse(current, "본 저장", nodes, growth))
                return store.Loaded();

            LogBackup(file.MoveCurrentToBackup());

            if (file.TryReadPrevious(out ProgressSaveData previous) && store.TryUse(previous, "직전 저장", nodes, growth))
                return store.Loaded();

            LogBackup(file.MovePreviousToBackup());
            return store.Loaded();
        }

        // 계속. 이번 실행에서 이미 진행 중이면 메모리의 진행 상태가 가장 새것이라 그대로 둔다.
        public void Continue(ProgressState progress)
        {
            if (_started)
                return;

            ProgressSave.Restore(progress, Saved);
            _basis = Saved;
            _started = true;
        }

        // 새 게임. 진행 상태를 처음으로 되돌리고 새 게임 로그(makeLog)를 더하며 바로 저장한다. 이전 저장은 직전 저장(.prev)으로 한 번 남는다.
        public void StartNew(ProgressState progress, Func<long, PendingLog> makeLog)
        {
            ProgressSave.StartNew(progress);
            _basis = null;
            _started = true;
            Commit(makeLog);
        }

        // 진행 상태를 바꾼 직후에 부른다(노드 구매, 판 시작·결산, 새 게임). 로그를 장부에 더하고 진행도와 함께 한 번에 저장한 뒤 LogAppended를 올린다.
        // 테스트 세션이면 로그를 만들지 않는다(makeLog를 부르지 않는다).
        public void Commit(Func<long, PendingLog> makeLog)
        {
            if (makeLog == null)
                throw new ArgumentNullException(nameof(makeLog));

            if (!_started || _testSession)
                return;

            _ledger.Append(makeLog);
            Save(_progress);
            LogAppended?.Invoke();
        }

        // 테스트 도구가 진행 상태를 바꾸기 직전에 부른다. 이후 이번 실행은 진행 중으로 보고(계속은 메모리 상태 그대로) 저장하지 않는다.
        // 되돌리는 길은 없다. 앱(장면)을 다시 시작하면 저장 파일에서 다시 불러온다.
        public void EnterTestSession()
        {
            _started = true;
            _testSession = true;
        }

        public void Save(ProgressState progress)
        {
            if (!_started || _testSession)
                return;

            Write(ProgressSave.Capture(progress, _basis, _utcNow()));
        }

        // 서버 응답 하나를 반영한다(계약 5.7). 응답마다 파일에 쓴다 — 새 게임·계속을 고르기 전(타이틀)에도 쓴다. 대기 로그와 다음 seq를 잃지 않기 위해서다.
        public SyncApplyOutcome ApplyResponse(SyncResponseDto response)
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));

            ProgressDto server = response.progress ?? new ProgressDto();
            SyncApplication plan = _ledger.Apply(response, LocalMatches(server), _utcNow());

            if (plan.OverwriteProgress)
            {
                if (plan.Outcome == SyncApplyOutcome.AppliedWithDrift)
                    Debug.LogWarning("[동기화] 거부가 없는데 서버 진행도와 로컬 진행도가 다르다. 서버 값을 따른다(규칙 이식 버그 신호).");

                Overwrite(server);
            }

            WriteState();
            return plan.Outcome;
        }

        public void MarkLastRejectionShown()
        {
            _ledger.MarkLastRejectionShown();
            WriteState();
        }

        private ProgressStore Loaded()
        {
            _ledger = new SyncLedger(Saved?.Sync);
            return this;
        }

        // 로컬 진행도가 서버와 같은가. 고르기 전에는 진행 상태가 아직 저장에서 이어지지 않았으므로 불러온 저장과 비교한다.
        private bool LocalMatches(ProgressDto server)
        {
            if (_started)
                return ServerProgress.Matches(_progress, server);

            return Saved == null
                ? ServerProgress.Matches(0, HqGrowthDefinition.StartStage, Array.Empty<(string NodeId, int Rank)>(), server)
                : ServerProgress.Matches(Saved.Gold, Saved.GrowthStage, Saved.Ranks, server);
        }

        // 로컬 진행도를 서버 진행도로 덮어쓴다. 진행 저장의 "큰 값 유지"(Capture의 basis)를 거치지 않는다.
        private void Overwrite(ProgressDto server)
        {
            ProgressLoadResult result = ProgressSave.Load(ServerProgress.ToSaveData(server, _utcNow(), null), _nodes, _growth);

            if (!result.Succeeded)
            {
                Debug.LogError(Lines("[동기화] 서버 진행도를 로컬에 쓰지 못했다. 로컬 값을 그대로 둔다.", result.Diagnostics));
                return;
            }

            if (_started)
            {
                ProgressSave.Restore(_progress, result.Progress);
                _basis = null;
            }
            else if (Saved != null || !ServerProgress.IsEmpty(server))
            {
                // 계속으로 이어 할 저장이 된다. 서버에 기록이 없고 로컬 저장도 없으면(첫 실행) 만들지 않는다.
                Saved = result.Progress;
            }
        }

        // 응답·거부 확인처럼 진행도가 바뀌지 않은 변경을 저장한다. 고르기 전에는 불러온 저장의 진행도에 장부만 바꿔 쓴다.
        private void WriteState()
        {
            if (_testSession)
                return;

            if (_started)
                Write(ProgressSave.Capture(_progress, _basis, _utcNow()));
            else if (Saved != null)
                Write(ProgressSave.WithSync(Saved, null));
        }

        private void Write(ProgressSaveData data)
        {
            data.Sync = _ledger.ToSave();

            try
            {
                _file.Write(data);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                // 저장 공간 부족 등. 게임은 그대로 이어 가고, 다음 저장 때 다시 쓴다(장부는 메모리에 있다).
                Debug.LogError($"[저장] 저장하지 못했다: {error.Message}");
            }
        }

        private bool TryUse(ProgressSaveData data, string which, NodeContent nodes, HqGrowthDefinition growth)
        {
            ProgressLoadResult result = ProgressSave.Load(data, nodes, growth);

            if (result.Diagnostics.Count > 0)
                Debug.LogError(Lines($"[저장] {which}이 깨졌다.", result.Diagnostics));

            if (result.Adjustments.Count > 0)
                Debug.LogWarning(Lines($"[저장] {which}을 지금 콘텐츠에 맞춰 썼다. 파일에는 원래 값이 남는다.", result.Adjustments));

            Saved = result.Progress;
            return result.Succeeded;
        }

        private static string Lines(string title, IReadOnlyList<ContentDiagnostic> diagnostics) =>
            title + "\n  " + string.Join("\n  ", diagnostics);

        private static void LogBackup(string backup)
        {
            if (backup != null)
                Debug.LogWarning($"[저장] 읽지 못한 저장 파일을 보관했다: {backup}");
        }
    }
}
