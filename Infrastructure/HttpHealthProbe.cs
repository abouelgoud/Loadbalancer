using System.Net.Http.Json;
using LoadBalancer.Api.Config;
using LoadBalancer.Api.Core;
using LoadBalancer.Api.Domain;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LoadBalancer.Api.Infrastructure
{
    public sealed class HttpHealthProbe : IHealthProbe
    {
        private readonly IHttpClientFactory _factory;
        private readonly LoadBalancerOptions _opts;

        public HttpHealthProbe(IHttpClientFactory factory, IOptions<LoadBalancerOptions> opts)
        {
            _factory = factory; _opts = opts.Value;
        }

        public async Task<(bool ok, double? cpu, double? mem)> ProbeAsync(BackendServer server, CancellationToken ct)
        {
            var client = _factory.CreateClient(nameof(ProxyClient));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_opts.HealthProbeTimeoutSeconds));

            try
            {
                var probeUri = new Uri(server.BaseAddress, _opts.HealthProbePath);
                using var req = new HttpRequestMessage(HttpMethod.Get, probeUri);
                using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (!resp.IsSuccessStatusCode) return (false, null, null);

                try
                {
                    var metricsUri = new Uri(server.BaseAddress, _opts.MetricsProbePath);
                    var dict = await client.GetFromJsonAsync<Dictionary<string, double>>(metricsUri, cts.Token);
                    if (dict is not null &&
                        dict.TryGetValue("cpu", out var cpu) &&
                        dict.TryGetValue("mem", out var mem))
                    {
                        return (true, cpu, mem);
                    }
                }
                catch { }

                return (true, null, null);
            }
            catch
            {
                return (false, null, null);
            }
        }
    }
}
