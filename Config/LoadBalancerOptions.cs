using System.ComponentModel.DataAnnotations;

namespace LoadBalancer.Api.Config
{
    public sealed class LoadBalancerOptions
    {
        [Required]
        public List<BackendServerOptions> Servers { get; set; } = new();

        [Range(1, 300)]
        public int HealthProbeIntervalSeconds { get; set; } = 5;

        [Range(1, 60)]
        public int HealthProbeTimeoutSeconds { get; set; } = 2;

        public string HealthProbePath { get; set; } = "/health";

        public string MetricsProbePath { get; set; } = "/metrics";

        [Range(1, 10)]
        public int ProbeFailureThreshold { get; set; } = 3;

        [Range(0, 10)]
        public int RetryOnUpstreamFailure { get; set; } = 1;

        [Range(1, 120)]
        public int UpstreamRequestTimeoutSeconds { get; set; } = 30;

        public int GlobalPendingHardLimit { get; set; } = -1;

        public WeightsOptions Weights { get; set; } = new();
    }

    public sealed class BackendServerOptions
    {
        [Required] public required string Id { get; set; }
        [Required] public required string BaseAddress { get; set; }
        [Range(1, 10000)] public int MaxActiveRequests { get; set; } = 200;
    }

    public sealed class WeightsOptions
    {
        [Range(0, 1)] public double Active { get; set; } = 0.5;
        [Range(0, 1)] public double Cpu { get; set; } = 0.3;
        [Range(0, 1)] public double Mem { get; set; } = 0.2;
    }
}
