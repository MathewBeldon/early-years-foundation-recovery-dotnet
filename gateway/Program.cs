using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MigrationGateway;

public partial class Program
{
    public static void Main(string[] args)
    {
        BuildApplication(args).Run();
    }

    public static WebApplication BuildApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options => options.IncludeScopes = true);
        builder.Services
            .AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
        builder.Services.AddHealthChecks();

        var telemetry = builder.Services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("early-years-migration-gateway"))
            .WithTracing(traces => traces.AddAspNetCoreInstrumentation(options =>
                options.Filter = context => !context.Request.Path.StartsWithSegments("/gateway-health")));

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.WithTracing(traces => traces.AddOtlpExporter());
        }

        var app = builder.Build();
        app.MapHealthChecks("/gateway-health");
        app.MapReverseProxy();
        return app;
    }
}
