using Newtonsoft.Json;
using NUnit.Framework;

namespace MeshyWorkspace.Tests
{
    public class MeshyTextureUrlsTests
    {
        [Test]
        public void EmptyArrayParsesToEmptyList()
        {
            var task = JsonConvert.DeserializeObject<TextTo3DTask>(
                "{\"id\":\"t\",\"status\":\"SUCCEEDED\",\"texture_urls\":[]}");

            Assert.That(task.TextureUrls, Is.Not.Null);
            Assert.That(task.TextureUrls, Is.Empty);
        }

        [Test]
        public void ArrayOfObjectsParsesToUrlList()
        {
            var task = JsonConvert.DeserializeObject<TextTo3DTask>(
                "{\"id\":\"t\",\"status\":\"SUCCEEDED\",\"texture_urls\":[{\"base_color\":\"https://x/color.png\",\"normal\":\"https://x/normal.png\"}]}");

            Assert.That(task.TextureUrls, Has.Count.EqualTo(2));
            Assert.That(task.TextureUrls, Does.Contain("https://x/color.png"));
            Assert.That(task.TextureUrls, Does.Contain("https://x/normal.png"));
        }

        [Test]
        public void ObjectParsesToUrlList()
        {
            var task = JsonConvert.DeserializeObject<TextTo3DTask>(
                "{\"id\":\"t\",\"status\":\"SUCCEEDED\",\"texture_urls\":{\"base_color\":\"https://x/color.png\"}}");

            Assert.That(task.TextureUrls, Has.Count.EqualTo(1));
            Assert.That(task.TextureUrls[0], Is.EqualTo("https://x/color.png"));
        }
    }
}
