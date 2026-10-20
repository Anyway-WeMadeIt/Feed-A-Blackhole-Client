using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace BlackHole.ContentExport
{
    // 서버 콘텐츠 묶음 파일 하나: content/ 기준 상대 경로(구분자 /)와 바이트.
    public sealed class BundleFile
    {
        public string Path { get; }
        public byte[] Bytes { get; }

        public BundleFile(string path, byte[] bytes)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        }

        public string Text => Encoding.UTF8.GetString(Bytes);
    }

    // 서버 콘텐츠 묶음(계약 1.2~1.3): 목차에 적히는 파일들과 목차(manifest.json), 콘텐츠 버전.
    public sealed class ServerContentBundle
    {
        public const string ManifestPath = "manifest.json";
        public const int FormatVersion = 1;

        // 목차에 적히는 파일(버전 계산 순서).
        public IReadOnlyList<BundleFile> Files { get; }

        public BundleFile Manifest { get; }

        public string ContentVersion { get; }

        internal ServerContentBundle(IReadOnlyList<BundleFile> files)
        {
            Files = files;
            ContentVersion = VersionOf(files);
            Manifest = new BundleFile(ManifestPath, Encoding.UTF8.GetBytes(ManifestJson(ContentVersion, files)));
        }

        // 계약 1.3: 목차 순서대로 경로(UTF-8) + 0x00 + 파일 바이트 + 0x00을 이어 붙인 SHA-256의 소문자 16진수 앞 16자.
        // 서버의 ContentVersion(Java)과 같은 계산이다.
        public static string VersionOf(IReadOnlyList<BundleFile> files)
        {
            using SHA256 sha = SHA256.Create();
            var zero = new byte[] { 0 };

            foreach (BundleFile file in files)
            {
                byte[] path = Encoding.UTF8.GetBytes(file.Path);
                sha.TransformBlock(path, 0, path.Length, null, 0);
                sha.TransformBlock(zero, 0, 1, null, 0);
                sha.TransformBlock(file.Bytes, 0, file.Bytes.Length, null, 0);
                sha.TransformBlock(zero, 0, 1, null, 0);
            }

            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            var hex = new StringBuilder(64);

            foreach (byte b in sha.Hash)
                hex.Append(b.ToString("x2"));

            return hex.ToString(0, 16);
        }

        private static string ManifestJson(string version, IReadOnlyList<BundleFile> files)
        {
            var paths = new List<object>();

            foreach (BundleFile file in files)
                paths.Add(file.Path);

            return PyJson.Write(new JsonObject
            {
                { "formatVersion", FormatVersion },
                { "contentVersion", version },
                { "files", paths },
            }) + "\n";
        }
    }
}
