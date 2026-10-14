using System;
using System.Globalization;
using System.IO;
using System.Text;
using BlackHole.Core;
using UnityEngine;

namespace BlackHole.Unity
{
    // 진행 저장 파일(JSON)을 읽고 쓴다. 내용 검사는 하지 않는다.
    // 쓰기: 임시 파일에 다 쓴 뒤 바꿔치기한다. 바뀌기 전 파일은 직전 저장(.prev)으로 남는다 — 본 파일이 깨지면 그것으로 이어 한다.
    // 깨진 파일은 덮어쓰지 않고 .bak으로 옮겨 보관한다.
    // 웹은 쓰기·옮기기·지우기 뒤 브라우저 저장소에 동기화해야 남는다(SaveLocation.Flush).
    public sealed class ProgressSaveFile
    {
        private const string FileName = "progress.json";
        private static readonly UTF8Encoding Utf8NoBom = new(false);

        private readonly string _path;
        private readonly string _previousPath;

        public ProgressSaveFile(string directory)
        {
            _path = Path.Combine(directory, FileName);
            _previousPath = _path + ".prev";
        }

        public void Write(ProgressSaveData data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true), Utf8NoBom);

            if (File.Exists(_path))
                File.Replace(tmp, _path, _previousPath);
            else
                File.Move(tmp, _path);

            SaveLocation.Flush();
        }

        // 본 파일. 없거나 JSON으로 읽지 못하면 false.
        public bool TryReadCurrent(out ProgressSaveData data) => TryRead(_path, out data);

        // 직전 저장. 없거나 JSON으로 읽지 못하면 false.
        public bool TryReadPrevious(out ProgressSaveData data) => TryRead(_previousPath, out data);

        // 보관한 경로. 파일이 없었으면 null.
        public string MoveCurrentToBackup() => MoveToBackup(_path);

        public string MovePreviousToBackup() => MoveToBackup(_previousPath);

        // 본 파일과 직전 저장을 지운다. 테스트 도구의 "계정·진행 초기화"가 부른다. 보관한 .bak은 남긴다.
        public void Delete()
        {
            foreach (string path in new[] { _path, _previousPath, _path + ".tmp" })
            {
                if (File.Exists(path))
                    File.Delete(path);
            }

            SaveLocation.Flush();
        }

        private static bool TryRead(string path, out ProgressSaveData data)
        {
            data = null;

            if (!File.Exists(path))
                return false;

            try
            {
                data = JsonUtility.FromJson<ProgressSaveData>(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (ArgumentException)
            {
                return false;
            }

            return data != null;
        }

        // 보관 이름에 시각(같은 초에 또 보관하면 번호까지)을 붙여 앞서 보관한 파일을 덮지 않는다.
        private static string MoveToBackup(string path)
        {
            if (!File.Exists(path))
                return null;

            string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            string backup = $"{path}.{stamp}.bak";

            for (int n = 1; File.Exists(backup); n++)
                backup = $"{path}.{stamp}-{n}.bak";

            File.Move(path, backup);
            SaveLocation.Flush();
            return backup;
        }
    }
}
