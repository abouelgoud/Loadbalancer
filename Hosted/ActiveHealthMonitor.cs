using LoadBalancer.Api.Config;
using LoadBalancer.Api.Core;
using LoadBalancer.Api.Domain;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LoadBalancer.Api.Hosted
{
    public sealed class ActiveHealthMonitor : BackgroundService
    {
        private readonly IServerRegistry _registry;
        private readonly IHealthProbe _probe;
        private readonly LoadBalancerOptions _opts;
        private readonly ILogger<ActiveHealthMonitor> _logger;

        public ActiveHealthMonitor(IServerRegistry registry, IHealthProbe probe, IOptions<LoadBalancerOptions> opts, ILogger<ActiveHealthMonitor> logger)
        { _registry = registry; _probe = probe; _opts = opts.Value; _logger = logger; }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var snapshot = _registry.Snapshot();
                var tasks = snapshot.Select(async server =>
                {
                    var (ok, cpu, mem) = await _probe.ProbeAsync(server, stoppingToken);
                    server.MarkProbeResult(ok, cpu, mem);

                    if (!ok && server.ConsecutiveProbeFailures >= _opts.ProbeFailureThreshold)
                    {
                        if (server.Health != ServerHealth.Unhealthy)
                            server.ForceUnhealthy();
                        _logger.LogWarning("{Id} marked Unhealthy after {Fail} failures", server.Id, server.ConsecutiveProbeFailures);
                    }
                    else if (ok && server.Health == ServerHealth.Healthy)
                    {
                        _logger.LogInformation("{Id} Healthy (cpu={Cpu:0.00}, mem={Mem:0.00})", server.Id, server.Cpu, server.Mem);
                    }
                });

                try { await Task.WhenAll(tasks); }
                catch (Exception ex) { _logger.LogError(ex, "Health monitor iteration failed"); }

                await Task.Delay(TimeSpan.FromSeconds(_opts.HealthProbeIntervalSeconds), stoppingToken);
            }
        }
    }
}
