using System;
using System.Collections.Generic;
using BlackHole.Core;
using NUnit.Framework;

namespace BlackHole.ContentExport.Tests
{
    // 전투 규칙 기대값 표(계약 1.7a)의 확인 지점 선택과 글자 형식.
    // 표 전체는 실제 콘텐츠가 있어야 만들 수 있어(에디터 메뉴) 여기서는 콘텐츠 없이 확인할 수 있는 규칙만 본다.
    // 전체의 일치는 서버 테스트(W4-1b)가 확인한다.
    public sealed class RulesTableBuilderTests
    {
        // Level 1, 2, 3에 닿는 누적 EXP가 10, 25, 45. 성장도 1의 이정표는 Level 2.
        private static HqGrowthDefinition Growth() => new(
            new long[] { 10, 25, 45 },
            new[] { new HqMilestone(2, 100, 1f) });

        [TestCase(335, 10, 33)]
        [TestCase(335, 25, 83)]
        [TestCase(335, 50, 167)]
        [TestCase(335, 75, 251)]
        [TestCase(0, 50, 0)]
        [TestCase(7, 100, 7)]
        [TestCase(7, 0, 0)]
        public void PartialCountRoundsDown(int total, int percent, int expected)
        {
            Assert.AreEqual(expected, RulesTableBuilder.PartialCount(total, percent));
        }

        [TestCase(10, -1)]
        [TestCase(10, 101)]
        [TestCase(-1, 50)]
        public void PartialCountRejectsOutOfRangeInput(int total, int percent)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RulesTableBuilder.PartialCount(total, percent));
        }

        [Test]
        public void ProbeExpsAroundEveryThresholdFromTheStartLevel()
        {
            Assert.AreEqual(
                new long[] { 0, 9, 10, 11, 24, 25, 26, 44, 45, 46 },
                RulesTableBuilder.ProbeExps(Growth(), 0));
        }

        [Test]
        public void ProbeExpsStartOneBelowTheStartExpAndSkipLevelsAtOrBelowTheStartLevel()
        {
            // 성장도 1은 Level 2에서 시작한다(시작 EXP 25). Level 3의 임계값 45 근처만 더한다.
            Assert.AreEqual(
                new long[] { 24, 25, 44, 45, 46 },
                RulesTableBuilder.ProbeExps(Growth(), 1));
        }

        [Test]
        public void ProbeGoldsCoverTheTargetBoundariesAndDouble()
        {
            Assert.AreEqual(new long[] { 0, 1, 99, 100, 101, 200 }, RulesTableBuilder.ProbeGolds(100));
        }

        [Test]
        public void ProbeGoldsDropDuplicatesForTinyTargets()
        {
            Assert.AreEqual(new long[] { 0, 1, 2 }, RulesTableBuilder.ProbeGolds(1));
        }

        [Test]
        public void ProbeGoldsDoNotOverflowForHugeTargets()
        {
            IReadOnlyList<long> golds = RulesTableBuilder.ProbeGolds(long.MaxValue - 1);

            // 2 × target은 long을 넘으므로 넣지 않는다.
            Assert.AreEqual(new long[] { 0, 1, long.MaxValue - 2, long.MaxValue - 1, long.MaxValue }, golds);
        }

        [Test]
        public void ArithmeticCoversEveryValueAndFactorOnce()
        {
            JsonObject arithmetic = RulesTableBuilder.ArithmeticJson();
            int expected = RulesTableBuilder.ArithmeticValues.Length * RulesTableBuilder.ArithmeticFactors.Length;

            Assert.AreEqual(expected, Cases(arithmetic, "multiply").Count);
            Assert.AreEqual(expected, Cases(arithmetic, "critBonus").Count);
        }

        // 정확히 .5가 되는 곱은 0에서 먼 쪽(올림)이다. 서버가 HALF_EVEN으로 틀리게 구현하면 이 값에서 어긋난다.
        [Test]
        public void HalvesRoundAwayFromZero()
        {
            JsonObject arithmetic = RulesTableBuilder.ArithmeticJson();

            Assert.AreEqual(3L, ResultOf(arithmetic, "multiply", 5L, 0.5f));
            Assert.AreEqual(3L, ResultOf(arithmetic, "critBonus", 5L, 0.5f));
            Assert.AreEqual(2L, ResultOf(arithmetic, "multiply", 3L, 0.5f));
        }

        // 곱셈이 double이면 값이 보존되고, float이면 2^24를 넘는 값이 깎인다. 두 식이 갈리는 지점이다(계약 C28).
        [Test]
        public void MultiplyKeepsLargeValuesButCritBonusMultipliesAsFloat()
        {
            JsonObject arithmetic = RulesTableBuilder.ArithmeticJson();

            Assert.AreEqual(16777217L, ResultOf(arithmetic, "multiply", 16777217L, 1f));
            Assert.AreEqual(16777216L, ResultOf(arithmetic, "critBonus", 16777217L, 1f));
        }

        private static List<List<object>> Cases(JsonObject arithmetic, string name)
        {
            foreach (KeyValuePair<string, object> entry in arithmetic)
            {
                if (entry.Key != name)
                    continue;

                var cases = new List<List<object>>();

                foreach (object item in (List<object>)entry.Value)
                    cases.Add((List<object>)((JsonInline)item).Value);

                return cases;
            }

            throw new AssertionException($"'{name}' 칸이 없다.");
        }

        private static long ResultOf(JsonObject arithmetic, string name, long value, float factor)
        {
            foreach (List<object> triple in Cases(arithmetic, name))
            {
                if ((long)triple[0] == value && (float)triple[1] == factor)
                    return (long)triple[2];
            }

            throw new AssertionException($"{name}에 ({value}, {factor}) 경우가 없다.");
        }

        [Test]
        public void InlineValueStaysOnOneLineInsideAMultiLineDocument()
        {
            string json = PyJson.Write(new JsonObject
            {
                { "a", new List<object> { new JsonInline(new List<object> { 1, 2L }), new JsonInline(new List<object> { 3, 4L }) } },
                { "b", new JsonInline(new JsonObject { { "x", 0.5f }, { "y", null } }) },
            });

            Assert.AreEqual(
                "{\n  \"a\": [\n    [1, 2],\n    [3, 4]\n  ],\n  \"b\": {\"x\": 0.5, \"y\": null}\n}",
                json);
        }
    }
}
