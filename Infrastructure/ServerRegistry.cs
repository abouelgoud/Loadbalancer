using System.Collections.Concurrent;
using LoadBalancer.Api.Config;
using LoadBalancer.Api.Core;
using LoadBalancer.Api.Domain;
using Microsoft.Extensions.Options;
using System.Linq;

namespace LoadBalancer.Api.Infrastructure
{
    public sealed class ServerRegistry : IServerRegistry
    {
        private readonly ConcurrentDictionary<string, BackendServer> _servers = new();

        public ServerRegistry(IOptions<LoadBalancerOptions> options)
        {
            foreach (var s in options.Value.Servers)
            {
                var server = new BackendServer(s.Id, new Uri(s.BaseAddress), s.MaxActiveRequests);
                _servers[s.Id] = server;
            }
        }

        public IReadOnlyList<BackendServer> Snapshot() => _servers.Values.OrderBy(s => s.Id).ToList();
        public IEnumerable<BackendServer> HealthyServers() => _servers.Values.Where(s => s.Health == ServerHealth.Healthy);
        public BackendServer? GetById(string id) => _servers.TryGetValue(id, out var s) ? s : null;
    }
}
