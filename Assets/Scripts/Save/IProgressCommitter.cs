using System;
using BlackHole.Core;
using BlackHole.Net;

namespace BlackHole.Unity
{
    // 진행 상태의 변화를 로그와 함께 저장하는 쪽. ProgressStore가 구현한다.
    // ProgressRecorder가 이것만 보므로, 파일 없이(장부만으로) 로그 순서를 검사하는 테스트를 만들 수 있다.
    public interface IProgressCommitter
    {
        // 새 게임. 진행 상태를 처음으로 되돌리고 새 게임 로그(makeLog)를 더하며 바로 저장한다.
        void StartNew(ProgressState progress, Func<long, PendingLog> makeLog);

        // 진행 상태를 바꾼 직후. 로그를 장부에 더하고 진행도와 함께 저장한다. 테스트 세션이면 아무것도 하지 않는다.
        void Commit(Func<long, PendingLog> makeLog);

        // 이번 실행은 더 저장하지 않고 로그도 만들지 않는다(테스트 세션). 되돌리는 길은 없다.
        void EnterTestSession();
    }
}
