using LoadBalancer.Api.Core;
using LoadBalancer.Api.Domain;

namespace LoadBalancer.Api.Infrastructure
{
    public sealed class RequestTracker : IRequestTracker
    {
        public void OnBegin(BackendServer server) => server.IncrementActive();
        public void OnEnd(BackendServer server) => server.DecrementActive();
    }
}
