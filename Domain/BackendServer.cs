namespace LoadBalancer.Api.Domain
{
    public sealed class BackendServer
    {
        public string Id { get; }
        public Uri BaseAddress { get; }
        public int MaxActiveRequests { get; }

        private int _active;
        public int ActiveRequests => Volatile.Read(ref _active);

        public double Cpu { get; set; }
        public double Mem { get; set; }

        public DateTimeOffset LastHealthCheckUtc { get; private set; }
        public ServerHealth Health { get; private set; } = ServerHealth.Unhealthy;
        public int ConsecutiveProbeFailures { get; private set; }

        public BackendServer(string id, Uri baseAddress, int maxActive)
        {
            Id = id; BaseAddress = baseAddress; MaxActiveRequests = maxActive;
        }

        public void IncrementActive() => Interlocked.Increment(ref _active);
        public void DecrementActive() => Interlocked.Decrement(ref _active);

        public bool UnderLimit => ActiveRequests < MaxActiveRequests;
        public double LoadRatio => (double)ActiveRequests / Math.Max(1, MaxActiveRequests);

        public void MarkProbeResult(bool success, double? cpu = null, double? mem = null)
        {
            LastHealthCheckUtc = DateTimeOffset.UtcNow;
            if (success)
            {
                Health = ServerHealth.Healthy;
                ConsecutiveProbeFailures = 0;
                if (cpu.HasValue) Cpu = Math.Clamp(cpu.Value, 0, 1);
                if (mem.HasValue) Mem = Math.Clamp(mem.Value, 0, 1);
            }
            else
            {
                ConsecutiveProbeFailures++;
            }
        }

        public void ForceUnhealthy() => Health = ServerHealth.Unhealthy;
    }
}
