using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;

namespace BlackHole.Net.Tests
{
    internal static class TestTasks
    {
        // [UnityTest]에서 작업이 끝날 때까지 프레임을 넘긴다. 실제 요청(UnityWebRequest)은 에디터 업데이트에서 진행된다.
        public static IEnumerator Wait(Task task, double seconds)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (!task.IsCompleted)
            {
                if (DateTime.UtcNow > deadline)
                    Assert.Fail($"{seconds}초 안에 끝나지 않았다.");

                yield return null;
            }
        }

        // 가짜 전송만 쓰는 흐름은 곧바로 끝난다. 끝나지 않았으면 테스트가 잘못 짠 것이다.
        public static T Done<T>(Task<T> task)
        {
            Assert.IsTrue(task.IsCompleted, "작업이 곧바로 끝나지 않았다.");
            return task.GetAwaiter().GetResult();
        }

        public static void Done(Task task)
        {
            Assert.IsTrue(task.IsCompleted, "작업이 곧바로 끝나지 않았다.");
            task.GetAwaiter().GetResult();
        }
    }
}
