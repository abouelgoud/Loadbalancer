using LoadBalancer.Api.Config;
using LoadBalancer.Api.Core;
using LoadBalancer.Api.Domain;
using Microsoft.Extensions.Options;
using System.Linq;

namespace LoadBalancer.Api.Infrastructure
{
    public sealed class CompositeLoadSelector : IServerSelector
    {
        private readonly WeightsOptions _weights;
        public CompositeLoadSelector(Microsoft.Extensions.Options.IOptions<LoadBalancer.Api.Config.LoadBalancerOptions> opts)
        {
            _weights = opts.Value.Weights;
        }

        public BackendServer? ChooseServer(HttpContext context, IReadOnlyList<BackendServer> candidates)
        {
            var under = candidates.Where(s => s.UnderLimit).ToList();
            var pool = under.Count > 0 ? under : candidates;

            return pool
                .OrderBy(Score)
                .ThenBy(s => s.ActiveRequests)
                .FirstOrDefault();
        }

        private double Score(BackendServer s)
        {
            var active = s.LoadRatio * _weights.Active;
            var cpu = s.Cpu * _weights.Cpu;
            var mem = s.Mem * _weights.Mem;
            return active + cpu + mem;
        }
    }
}
