using System;
using System.Threading;
using NUnit.Framework;

namespace MeshyWorkspace.Tests
{
    public class MeshyTaskPollerTests
    {
        [Test]
        public void PollsUntilSucceeded()
        {
            var api = new FakeMeshyApi(
                new TextToImageTask { Id = "t", StatusRaw = "PENDING" },
                new TextToImageTask { Id = "t", StatusRaw = "IN_PROGRESS", Progress = 50 },
                new TextToImageTask { Id = "t", StatusRaw = "SUCCEEDED", Progress = 100 });
            var poller = new MeshyTaskPoller(api, TimeSpan.FromMilliseconds(1));

            var result = poller.WaitForTaskAsync<TextToImageTask>("t", "text-to-image").GetAwaiter().GetResult();

            Assert.That(result.Status, Is.EqualTo(MeshyTaskStatus.Succeeded));
            Assert.That(api.GetTaskCalls, Is.EqualTo(3));
        }

        [Test]
        public void FailedTaskThrowsFriendlyError()
        {
            var api = new FakeMeshyApi(new TextToImageTask
            {
                Id = "t",
                StatusRaw = "FAILED",
                TaskError = new MeshyTaskError { Code = "moderation_blocked" }
            });
            var poller = new MeshyTaskPoller(api, TimeSpan.FromMilliseconds(1));

            var ex = TestAssert.ThrowsAsync<MeshyApiException>(
                () => poller.WaitForTaskAsync<TextToImageTask>("t", "text-to-image")).GetAwaiter().GetResult();

            Assert.That(ex.Message, Does.Contain("安全审核"));
        }

        [Test]
        public void CancellationStopsPolling()
        {
            var api = new FakeMeshyApi(new TextToImageTask { Id = "t", StatusRaw = "PENDING" });
            var poller = new MeshyTaskPoller(api, TimeSpan.FromMilliseconds(1), 1000);

            using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50)))
            {
                TestAssert.ThrowsAsync<OperationCanceledException>(
                    () => poller.WaitForTaskAsync<TextToImageTask>("t", "text-to-image", null, cts.Token)).GetAwaiter().GetResult();
            }
        }
    }
}
