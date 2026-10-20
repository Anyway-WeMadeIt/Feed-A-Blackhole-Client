using System.Collections.Generic;
using NUnit.Framework;

namespace BlackHole.Net.Tests
{
    // 거부 알림의 "종류" 글자 키(계약 5.9). 글자 자체는 UIText.csv에 있고 UITextTableCheck가 표를 본다.
    public sealed class RejectionKindsTests
    {
        [TestCase(LogTypes.RunStart, "sync.rejection.kind.runStart")]
        [TestCase(LogTypes.Settlement, "sync.rejection.kind.settlement")]
        [TestCase(LogTypes.NodePurchase, "sync.rejection.kind.nodePurchase")]
        [TestCase(LogTypes.Reset, "sync.rejection.kind.reset")]
        public void KnownTypes_MapToTheirKey(string type, string key)
        {
            Assert.AreEqual(key, RejectionKinds.TextKeyOf(type));
        }

        [TestCase("SOMETHING_NEW")]
        [TestCase("")]
        [TestCase(null)]
        public void UnknownTypes_MapToOther(string type)
        {
            Assert.AreEqual(RejectionKinds.OtherKey, RejectionKinds.TextKeyOf(type));
        }

        [Test]
        public void Keys_AreDistinct()
        {
            var keys = new HashSet<string>
            {
                RejectionKinds.TextKeyOf(LogTypes.RunStart),
                RejectionKinds.TextKeyOf(LogTypes.Settlement),
                RejectionKinds.TextKeyOf(LogTypes.NodePurchase),
                RejectionKinds.TextKeyOf(LogTypes.Reset),
                RejectionKinds.OtherKey,
            };

            Assert.AreEqual(5, keys.Count);
        }
    }
}
