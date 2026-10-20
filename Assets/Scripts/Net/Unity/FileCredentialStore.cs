using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace BlackHole.Net.Unity
{
    // 계정 정보 파일(계약 2.5). 진행 저장과 같은 폴더의 account.json — 진행 저장과 계정이 늘 짝을 이룬다.
    // 쓰기: 임시 파일에 다 쓴 뒤 바꿔치기한다. 직전 파일은 남기지 않는다(옛 refresh 토큰은 이미 폐기됐다).
    // 읽지 못하는 파일(깨진 JSON, guestId·guestSecret 누락)은 시각을 붙여 보관하고 "계정 정보 없음"으로 본다.
    public sealed class FileCredentialStore : ICredentialStore
    {
        private const string FileName = "account.json";
        private const int FormatVersion = 1;
        private static readonly UTF8Encoding _utf8NoBom = new(false);

        private readonly string _path;
        private readonly Func<DateTime> _now;

        // directory: 진행 저장 폴더. now는 테스트가 savedAtUtc와 보관 이름의 시각을 정할 때 넣는다.
        public FileCredentialStore(string directory, Func<DateTime> now = null)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("저장 폴더가 필요하다.", nameof(directory));

            _path = Path.Combine(directory, FileName);
            _now = now ?? (() => DateTime.UtcNow);
        }

        public GuestCredentials Load()
        {
            if (!File.Exists(_path))
                return null;

            // 파일을 읽는 것 자체가 실패하면(잠김 등) 예외를 그대로 낸다. null로 보면 새 계정을 등록해 이 파일을 덮어쓴다.
            AccountFileData data = Parse(File.ReadAllText(_path, Encoding.UTF8));

            if (data != null)
                return new GuestCredentials(data.guestId, data.guestSecret, data.refreshToken);

            string backup = MoveToBackup();
            Debug.LogWarning($"[인증] 읽지 못한 계정 정보 파일을 보관했다: {backup ?? _path}");
            return null;
        }

        public void Save(GuestCredentials credentials)
        {
            if (credentials == null)
                throw new ArgumentNullException(nameof(credentials));

            var data = new AccountFileData
            {
                formatVersion = FormatVersion,
                guestId = credentials.GuestId,
                guestSecret = credentials.GuestSecret,
                refreshToken = credentials.RefreshToken ?? "",
                savedAtUtc = _now().ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            };

            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            string tmp = _path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true), _utf8NoBom);

            if (File.Exists(_path))
                File.Replace(tmp, _path, null);
            else
                File.Move(tmp, _path);
        }

        // 계정 정보 파일을 지운다. 테스트 도구의 "계정·진행 초기화"가 부른다. 보관한 .bak은 남긴다.
        public void Delete()
        {
            foreach (string path in new[] { _path, _path + ".tmp" })
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static AccountFileData Parse(string json)
        {
            try
            {
                AccountFileData data = JsonUtility.FromJson<AccountFileData>(json);
                return data != null && !string.IsNullOrEmpty(data.guestId) && !string.IsNullOrEmpty(data.guestSecret) ? data : null;
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        // 보관 이름에 시각(같은 초에 또 보관하면 번호까지)을 붙인다(ProgressSaveFile과 같은 규칙). 옮기지 못하면 null.
        // 옮기지 못한 파일은 다음 등록이 덮어쓴다 — 어차피 읽지 못하는 파일이다.
        private string MoveToBackup()
        {
            try
            {
                string stamp = _now().ToUniversalTime().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                string backup = $"{_path}.{stamp}.bak";

                for (int n = 1; File.Exists(backup); n++)
                    backup = $"{_path}.{stamp}-{n}.bak";

                File.Move(_path, backup);
                return backup;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                return null;
            }
        }

        // 필드 이름이 JSON 키다(계약 2.5).
        [Serializable]
        private sealed class AccountFileData
        {
            public int formatVersion;
            public string guestId;
            public string guestSecret;
            public string refreshToken;
            public string savedAtUtc;
        }
    }
}
