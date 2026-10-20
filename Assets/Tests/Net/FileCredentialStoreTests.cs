using System;
using System.IO;
using BlackHole.Net.Unity;
using NUnit.Framework;

namespace BlackHole.Net.Tests
{
    // 계정 정보 파일(계약 2.5): 형식, 바꿔치기 쓰기, 읽지 못하는 파일의 보관.
    public sealed class FileCredentialStoreTests
    {
        private static readonly DateTime SavedAt = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);

        private string _directory;
        private string AccountPath => Path.Combine(_directory, "account.json");

        [SetUp]
        public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "BlackHole.Net.Tests", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        private FileCredentialStore Store() => new(_directory, () => SavedAt);

        [Test]
        public void MissingFileIsNull()
        {
            Assert.IsNull(Store().Load());
        }

        [Test]
        public void SaveWritesTheContractFormat()
        {
            Store().Save(new GuestCredentials("id-1", "secret-1"));

            byte[] bytes = File.ReadAllBytes(AccountPath);
            string json = File.ReadAllText(AccountPath);

            Assert.AreEqual((byte)'{', bytes[0], "BOM이 없다.");
            StringAssert.Contains("\"formatVersion\": 1", json);
            StringAssert.Contains("\"guestId\": \"id-1\"", json);
            StringAssert.Contains("\"guestSecret\": \"secret-1\"", json);
            StringAssert.Contains("\"refreshToken\": \"\"", json);
            StringAssert.Contains("\"savedAtUtc\": \"2026-10-12T10:00:00Z\"", json);
            StringAssert.DoesNotContain("accessToken", json);
        }

        [Test]
        public void SaveReplacesAndLeavesNoTemporaryFile()
        {
            Store().Save(new GuestCredentials("id-1", "secret-1", "r1"));
            Store().Save(new GuestCredentials("id-1", "secret-1", "r2"));

            GuestCredentials loaded = Store().Load();

            Assert.AreEqual("id-1", loaded.GuestId);
            Assert.AreEqual("secret-1", loaded.GuestSecret);
            Assert.AreEqual("r2", loaded.RefreshToken);
            CollectionAssert.AreEqual(new[] { "account.json" }, FileNames(), "직전 파일(.prev)도 임시 파일도 남지 않는다.");
        }

        [Test]
        public void EmptyRefreshTokenLoadsAsNull()
        {
            Store().Save(new GuestCredentials("id-1", "secret-1"));

            Assert.IsNull(Store().Load().RefreshToken);
        }

        [TestCase("{\"guestId\":", TestName = "UnreadableFileIsMovedAside(BrokenJson)")]
        [TestCase("{\"formatVersion\":1,\"guestId\":\"id-1\"}", TestName = "UnreadableFileIsMovedAside(MissingSecret)")]
        [TestCase("", TestName = "UnreadableFileIsMovedAside(Empty)")]
        public void UnreadableFileIsMovedAside(string content)
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(AccountPath, content);

            Assert.IsNull(Store().Load());

            Assert.IsFalse(File.Exists(AccountPath));
            string backup = Path.Combine(_directory, "account.json.20261012100000.bak");
            Assert.IsTrue(File.Exists(backup), string.Join(", ", FileNames()));
            Assert.AreEqual(content, File.ReadAllText(backup), "내용은 그대로 보관한다.");
        }

        [Test]
        public void BackupsInTheSameSecondDoNotOverwrite()
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(AccountPath, "broken-1");
            Store().Load();
            File.WriteAllText(AccountPath, "broken-2");
            Store().Load();

            Assert.AreEqual("broken-1", File.ReadAllText(Path.Combine(_directory, "account.json.20261012100000.bak")));
            Assert.AreEqual("broken-2", File.ReadAllText(Path.Combine(_directory, "account.json.20261012100000-1.bak")));
        }

        [Test]
        public void DeleteRemovesTheFile()
        {
            Store().Save(new GuestCredentials("id-1", "secret-1", "r1"));

            Store().Delete();

            Assert.IsNull(Store().Load());
            Assert.DoesNotThrow(() => Store().Delete(), "없어도 된다.");
        }

        private string[] FileNames()
        {
            string[] paths = Directory.GetFiles(_directory);
            for (int i = 0; i < paths.Length; i++)
                paths[i] = Path.GetFileName(paths[i]);
            Array.Sort(paths, StringComparer.Ordinal);
            return paths;
        }
    }
}
