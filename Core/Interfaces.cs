using LoadBalancer.Api.Domain;

namespace LoadBalancer.Api.Core
{
    public interface IServerRegistry
    {
        IReadOnlyList<BackendServer> Snapshot();
        IEnumerable<BackendServer> HealthyServers();
        BackendServer? GetById(string id);
    }

    public interface IServerSelector
    {
        BackendServer? ChooseServer(HttpContext context, IReadOnlyList<BackendServer> candidates);
    }

    public interface IHealthProbe
    {
        Task<(bool ok, double? cpu, double? mem)> ProbeAsync(BackendServer server, CancellationToken ct);
    }

    public interface IProxyClient
    {
        Task ProxyAsync(HttpContext context, BackendServer server, CancellationToken ct);
    }

    public interface IRequestTracker
    {
        void OnBegin(BackendServer server);
        void OnEnd(BackendServer server);
    }
}
