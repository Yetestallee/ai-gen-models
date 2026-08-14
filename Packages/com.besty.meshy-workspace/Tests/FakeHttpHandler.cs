using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MeshyWorkspace.Tests
{
    public sealed class FakeHttpHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = Responder == null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : Responder(request);
            response.RequestMessage = request;
            return Task.FromResult(response);
        }
    }
}
