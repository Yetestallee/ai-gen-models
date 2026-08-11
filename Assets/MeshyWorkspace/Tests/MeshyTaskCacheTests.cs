using System;
using System.IO;
using NUnit.Framework;

namespace MeshyWorkspace.Tests
{
    public class MeshyTaskCacheTests
    {
        [Test]
        public void AddUpdateRemovePersists()
        {
            var path = Path.Combine(Path.GetTempPath(), "meshy-cache-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var cache = new MeshyTaskCache(path);
                cache.AddOrUpdate(new MeshyCachedTask
                {
                    TaskId = "t1",
                    TaskType = "text-to-image",
                    Status = "SUCCEEDED",
                    ImageUrls = new System.Collections.Generic.List<string> { "https://x/1.png" }
                });

                var reloaded = new MeshyTaskCache(path);
                Assert.That(reloaded.Entries, Has.Count.EqualTo(1));
                Assert.That(reloaded.Entries[0].ImageUrls, Has.Count.EqualTo(1));

                reloaded.Remove("t1");
                Assert.That(reloaded.Entries, Is.Empty);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
