using LoadBalancer.Api.Core;
using LoadBalancer.Api.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LoadBalancer.Api.Infrastructure
{
    public sealed class ProxyClient : IProxyClient
    {
        private readonly IHttpClientFactory _factory;
        private static readonly HashSet<string> HopByHop = new(StringComparer.OrdinalIgnoreCase)
        {
            "Connection","Keep-Alive","Proxy-Authenticate","Proxy-Authorization","TE","Trailers","Transfer-Encoding","Upgrade"
        };

        public ProxyClient(IHttpClientFactory factory) { _factory = factory; }

        public async Task ProxyAsync(HttpContext context, BackendServer server, CancellationToken ct)
        {
            var client = _factory.CreateClient(nameof(ProxyClient));
            var upstream = new UriBuilder(server.BaseAddress)
            {
                Path = context.Request.Path,
                Query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty
            }.Uri;

            using var upstreamRequest = new HttpRequestMessage(new HttpMethod(context.Request.Method), upstream);

            foreach (var header in context.Request.Headers)
            {
                if (!HopByHop.Contains(header.Key))
                {
                    if (!upstreamRequest.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                    {
                        upstreamRequest.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                    }
                }
            }

            if (context.Request.ContentLength is > 0 || context.Request.Body.CanRead)
            {
                upstreamRequest.Content = new StreamContent(context.Request.Body);
            }

            using var upstreamResponse = await client.SendAsync(upstreamRequest, HttpCompletionOption.ResponseHeadersRead, ct);

            context.Response.StatusCode = (int)upstreamResponse.StatusCode;
            foreach (var header in upstreamResponse.Headers)
                context.Response.Headers[header.Key] = header.Value.ToArray();
            foreach (var header in upstreamResponse.Content.Headers)
                context.Response.Headers[header.Key] = header.Value.ToArray();

            context.Response.Headers.Remove("transfer-encoding");

            await upstreamResponse.Content.CopyToAsync(context.Response.Body, ct);
        }
    }
}
