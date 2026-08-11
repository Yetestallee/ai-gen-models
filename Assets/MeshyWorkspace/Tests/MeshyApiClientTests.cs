using System.Net;
using System.Net.Http;
using NUnit.Framework;

namespace MeshyWorkspace.Tests
{
    public class MeshyApiClientTests
    {
        [Test]
        public void GetBalanceParsesResponse()
        {
            var handler = new FakeHttpHandler
            {
                Responder = request => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"balance\":3115}")
                }
            };

            using (var client = new MeshyApiClient(new MeshyApiConfig("key"), handler))
            {
                var result = client.GetBalanceAsync().GetAwaiter().GetResult();
                Assert.That(result.Balance, Is.EqualTo(3115));
            }
        }

        [Test]
        public void UnauthorizedThrowsFriendlyError()
        {
            var handler = new FakeHttpHandler
            {
                Responder = request => new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"message\":\"bad key\"}")
                }
            };

            using (var client = new MeshyApiClient(new MeshyApiConfig("key"), handler))
            {
                var ex = TestAssert.ThrowsAsync<MeshyApiException>(
                    () => client.GetBalanceAsync()).GetAwaiter().GetResult();
                Assert.That(ex.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
                Assert.That(ex.Message, Does.Contain("API Key"));
            }
        }
    }
}
