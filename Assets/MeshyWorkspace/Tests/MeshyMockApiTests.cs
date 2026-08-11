using NUnit.Framework;

namespace MeshyWorkspace.Tests
{
    public class MeshyMockApiTests
    {
        [Test]
        public void MockTaskReachesSucceededWithImages()
        {
            var api = new MeshyMockApi();
            var created = api.CreateTextToImageAsync(new TextToImageRequest { Prompt = "cat" }).GetAwaiter().GetResult();

            MeshyTaskBase last = null;
            for (var i = 0; i < 10; i++)
            {
                var task = api.GetTaskAsync<TextToImageTask>(created.Result, "text-to-image").GetAwaiter().GetResult();
                last = task;
                if (task.Status == MeshyTaskStatus.Succeeded)
                {
                    break;
                }
            }

            Assert.That(last.Status, Is.EqualTo(MeshyTaskStatus.Succeeded));
            var imageTask = (TextToImageTask)last;
            Assert.That(imageTask.ImageUrls, Has.Count.EqualTo(2));
        }
    }
}
