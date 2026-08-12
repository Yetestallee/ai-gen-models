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

        [Test]
        public void SerializeImageTo3DSmartTopologyRequestUsesSnakeCase()
        {
            var request = new ImageTo3DRequest
            {
                ImageUrl = "https://x/1.png",
                ShouldTexture = true,
                EnablePbr = true,
                AiModel = "meshy T1",
                ShouldRemesh = true,
                Topology = "triangle",
                TargetPolycount = 150000
            };

            var json = JsonConvert.SerializeObject(request);

            Assert.That(json, Does.Contain("\"should_remesh\":true"));
            Assert.That(json, Does.Contain("\"topology\":\"triangle\""));
            Assert.That(json, Does.Contain("\"target_polycount\":150000"));
            Assert.That(json, Does.Contain("\"ai_model\":\"meshy T1\""));
        }
    }
}
