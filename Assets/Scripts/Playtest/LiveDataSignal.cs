#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;

namespace BlackHole.Unity
{
    // 수치 파일이 바뀐 소식. 에디터의 파일 감시(LiveDataWatcher)가 새 값을 게임과 같은 로더로 검사한 뒤 낸다.
    // 듣는 쪽: 테스트 세팅 창(확정 정보 다시 계산), 플레이 중인 개발 패널(지금 세팅으로 다시 시작).
    // 에디터 전용이다. 개발 빌드(폰)는 프로필 JSON을 넣고 패널에서 다시 읽는다.
    internal static class LiveDataSignal
    {
        public static event Action<LiveDataChange> Changed;

        public static void Raise(LiveDataChange change) => Changed?.Invoke(change);
    }

    internal sealed class LiveDataChange
    {
        public LiveDataChange(
            IReadOnlyList<string> paths,
            bool contentChanged,
            bool valid,
            IReadOnlyList<string> errors,
            string fingerprint,
            string profile)
        {
            Paths = paths;
            ContentChanged = contentChanged;
            Valid = valid;
            Errors = errors;
            Fingerprint = fingerprint;
            Profile = profile;
        }

        // 바뀐 에셋 경로(Assets/…).
        public IReadOnlyList<string> Paths { get; }

        // 판 수치(게임 콘텐츠 세트가 가리키는 에셋·CSV, 밸런스 프로필)가 바뀌었나. 시나리오 파일만 바뀌었으면 false.
        public bool ContentChanged { get; }

        // 새 값이 게임 로더 검사를 통과했나(고른 프로필 적용 포함).
        public bool Valid { get; }

        public IReadOnlyList<string> Errors { get; }

        // 새 값의 수치 지문. 통과하지 못했으면 null.
        public string Fingerprint { get; }

        // 적용한 프로필 이름. 원본이면 "".
        public string Profile { get; }

        public string Files
        {
            get
            {
                var names = new List<string>(Paths.Count);
                foreach (string path in Paths)
                    names.Add(Path.GetFileName(path));

                return string.Join(", ", names);
            }
        }
    }
}
#endif
