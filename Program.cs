using LoadBalancer.Api.Config;
using LoadBalancer.Api.Core;
using LoadBalancer.Api.Hosted;
using LoadBalancer.Api.Infrastructure;
using LoadBalancer.Api.Middleware;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<LoadBalancerOptions>()
    .Bind(builder.Configuration.GetSection("LoadBalancer"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient(nameof(ProxyClient))
    .ConfigureHttpClient((sp, client) =>
    {
        var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LoadBalancerOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(opts.UpstreamRequestTimeoutSeconds);
    });

builder.Services.AddSingleton<IServerRegistry, ServerRegistry>();
builder.Services.AddSingleton<IServerSelector, CompositeLoadSelector>();
builder.Services.AddSingleton<IHealthProbe, HttpHealthProbe>();
builder.Services.AddSingleton<IProxyClient, ProxyClient>();
builder.Services.AddSingleton<IRequestTracker, RequestTracker>();

builder.Services.AddHostedService<ActiveHealthMonitor>();

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<LoadBalancingMiddleware>();

app.MapGet("/lb/servers", (IServerRegistry reg) => Results.Json(reg.Snapshot()));
app.MapGet("/lb/options", (Microsoft.Extensions.Options.IOptions<LoadBalancerOptions> o) => o.Value);

app.Run();
