using LoadBalancer.Api.Config;
using LoadBalancer.Api.Core;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LoadBalancer.Api.Middleware
{
    public sealed class LoadBalancingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IServerRegistry _registry;
        private readonly IServerSelector _selector;
        private readonly IProxyClient _proxy;
        private readonly IRequestTracker _tracker;
        private readonly LoadBalancerOptions _opts;
        private readonly ILogger<LoadBalancingMiddleware> _logger;

        public LoadBalancingMiddleware(
            RequestDelegate next,
            IServerRegistry registry,
            IServerSelector selector,
            IProxyClient proxy,
            IRequestTracker tracker,
            IOptions<LoadBalancerOptions> opts,
            ILogger<LoadBalancingMiddleware> logger)
        {
            _next = next; _registry = registry; _selector = selector; _proxy = proxy; _tracker = tracker; _opts = opts.Value; _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var attempts = 0;
            var tried = new HashSet<string>();

            while (true)
            {
                attempts++;
                var candidates = _registry.HealthyServers().Where(s => !tried.Contains(s.Id)).ToList();

                if (candidates.Count == 0)
                {
                    _logger.LogError("No healthy servers available");
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    await context.Response.WriteAsync("No backend servers available");
                    return;
                }

                if (_opts.GlobalPendingHardLimit > -1)
                {
                    var totalPending = candidates.Sum(s => s.ActiveRequests);
                    if (totalPending >= _opts.GlobalPendingHardLimit)
                    {
                        _logger.LogWarning("Global pending limit {Limit} reached ({Pending})", _opts.GlobalPendingHardLimit, totalPending);
                        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                        await context.Response.WriteAsync("Server busy. Try again later.");
                        return;
                    }
                }

                var chosen = _selector.ChooseServer(context, candidates);
                if (chosen is null)
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                    await context.Response.WriteAsync("No backend servers available");
                    return;
                }

                _logger.LogInformation("Routing {Method} {Path} to {Server} (active {Active}/{Max})",
                    context.Request.Method, context.Request.Path, chosen.Id, chosen.ActiveRequests, chosen.MaxActiveRequests);

                tried.Add(chosen.Id);
                _tracker.OnBegin(chosen);

                try
                {
                    await _proxy.ProxyAsync(context, chosen, context.RequestAborted);
                    return;
                }
                catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                {
                    _logger.LogWarning("Client aborted for {Path}", context.Request.Path);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Proxy to {Server} failed on attempt {Attempt}", chosen.Id, attempts);
                    chosen.MarkProbeResult(false);
                    if (attempts > _opts.RetryOnUpstreamFailure)
                    {
                        context.Response.StatusCode = StatusCodes.Status502BadGateway;
                        await context.Response.WriteAsync("Upstream error");
                        return;
                    }
                }
                finally
                {
                    _tracker.OnEnd(chosen);
                }
            }
        }
    }
}
