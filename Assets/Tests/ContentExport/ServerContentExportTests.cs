using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace BlackHole.ContentExport.Tests
{
    // 서버 콘텐츠 묶음의 글자 형식과 버전 계산(계약 1.2~1.4). 서버(Java)와 같은 값을 내야 한다.
    // 실제 콘텐츠로 내보낸 묶음이 서버 저장소의 샘플 묶음과 바이트까지 같은지는 내보내기 도구로 확인했다(2026-10-12, 버전 4d6c398ad1f5e49c).
    public sealed class ServerContentExportTests
    {
        // 기대값은 계약 1.3의 계산법을 Python으로 따로 구현해 구했다.
        [Test]
        public void VersionMatchesTheContractCalculation()
        {
            Assert.AreEqual("5e106e3d29cfc41b", ServerContentBundle.VersionOf(new[]
            {
                new BundleFile("a.json", new byte[] { 1, 2 }),
            }));

            Assert.AreEqual("ff2e0c8f287114ee", ServerContentBundle.VersionOf(new[]
            {
                new BundleFile("nodes/한.csv", Encoding.UTF8.GetBytes("x")),
                new BundleFile("b", new byte[0]),
            }));
        }

        [Test]
        public void JsonMatchesPythonIndentedFormat()
        {
            string json = PyJson.Write(new JsonObject
            {
                { "a", 1 },
                { "b", new List<object> { "x", 2L } },
                { "c", new JsonObject() },
                { "d", new List<object>() },
                { "e", null },
                { "f", true },
                { "g", 0.3f },
                { "h", "한\"\\\n" },
            });

            Assert.AreEqual(
                "{\n  \"a\": 1,\n  \"b\": [\n    \"x\",\n    2\n  ],\n  \"c\": {},\n  \"d\": [],\n  \"e\": null,\n" +
                "  \"f\": true,\n  \"g\": 0.3,\n  \"h\": \"한\\\"\\\\\\n\"\n}",
                json);
        }

        [Test]
        public void InlineJsonUsesCommaSpaceSeparators()
        {
            string json = PyJson.WriteInline(new JsonObject
            {
                { "Id", "timer-01" },
                { "Start", true },
                { "Links", new List<object> { "a", "b" } },
            });

            Assert.AreEqual("{\"Id\": \"timer-01\", \"Start\": true, \"Links\": [\"a\", \"b\"]}", json);
        }

        [TestCase(12f, "12")]
        [TestCase(0.3f, "0.3")]
        [TestCase(1.68f, "1.68")]
        [TestCase(0f, "0")]
        [TestCase(-2.5f, "-2.5")]
        public void FloatUsesTheShortestRoundTripText(float value, string expected)
        {
            Assert.AreEqual(expected, PyJson.Float(value));
        }

        [Test]
        public void SheetKeepsServerColumnsAndValuesAndDropsBlankRows()
        {
            string sheet = "﻿NodeId,Rank,Cost,분석\r\ntimer-01,1,\"1,500\",x\r\n,,,메모만\r\n";

            string server = NodeSheets.NodeCost(sheet);

            Assert.AreEqual("NodeId,Rank,Cost,Memo\ntimer-01,1,\"1,500\",\n", server);
        }
    }
}
