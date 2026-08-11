using Newtonsoft.Json;
using NUnit.Framework;

namespace MeshyWorkspace.Tests
{
    public class MeshyModelsTests
    {
        [Test]
        public void DeserializeTextToImageTask()
        {
            var json = "{\"id\":\"t1\",\"type\":\"text-to-image\",\"status\":\"SUCCEEDED\",\"progress\":100,\"image_urls\":[\"https://x/1.png\"]}";

            var task = JsonConvert.DeserializeObject<TextToImageTask>(json);

            Assert.That(task.Id, Is.EqualTo("t1"));
            Assert.That(task.Status, Is.EqualTo(MeshyTaskStatus.Succeeded));
            Assert.That(task.ImageUrls, Has.Count.EqualTo(1));
        }

        [Test]
        public void SerializeTextToImageRequestUsesSnakeCase()
        {
            var request = new TextToImageRequest
            {
                AiModel = "nano-banana",
                Prompt = "cat",
                AspectRatio = "1:1"
            };

            var json = JsonConvert.SerializeObject(request);

            Assert.That(json, Does.Contain("\"ai_model\""));
            Assert.That(json, Does.Contain("\"aspect_ratio\""));
            Assert.That(json, Does.Contain("\"prompt\""));
        }
    }
}
