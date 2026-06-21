using EarlyYearsFoundationRecovery.Application;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Observability;
using EarlyYearsFoundationRecovery.Infrastructure;
using EarlyYearsFoundationRecovery.Infrastructure.Contentful;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Web.Authentication;
using EarlyYearsFoundationRecovery.Web.Filters;
using EarlyYearsFoundationRecovery.Web.Services;
using GovUk.Frontend.AspNetCore;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var openTelemetryEnabled = builder.Configuration.GetValue("OpenTelemetry:Enabled", false);
if (openTelemetryEnabled)
{
    var serviceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? builder.Environment.ApplicationName;
    var serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString();
    var serviceAttributes = new[]
    {
        new KeyValuePair<string, object>("deployment.environment", builder.Environment.EnvironmentName)
    };
    var resourceBuilder = ResourceBuilder.CreateDefault()
        .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
        .AddAttributes(serviceAttributes);
    var otlpEndpoint = builder.Configuration["OpenTelemetry:Otlp:Endpoint"];

    builder.Logging.AddOpenTelemetry(logging =>
    {
        logging.IncludeFormattedMessage = true;
        logging.IncludeScopes = true;
        logging.SetResourceBuilder(resourceBuilder);

        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            logging.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    });

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
            .AddAttributes(serviceAttributes))
        .WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSource(ApplicationTelemetry.ActivitySourceName)
                .AddSource(ContentfulTelemetry.ActivitySourceName);

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
            }
        })
        .WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(ApplicationTelemetry.MeterName);

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
            }
        });
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddInMemoryPersistence("integration-tests");
}
else
{
    builder.Services.AddPostgreSqlPersistence(builder.Configuration);
}

builder.Services.AddGovUkFrontend();
builder.Services.AddAppCookieAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromHours(8);
});
builder.Services.AddScoped<RequireRegistrationIncompleteFilter>();
builder.Services.AddScoped<RequireRegistrationCompleteFilter>();
builder.Services.AddSingleton<GovUkMarkdownRenderer>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

var contentful = app.Configuration.GetSection(ContentfulOptions.SectionName).Get<ContentfulOptions>();
if (contentful?.IsConfigured == true)
{
    app.Logger.LogInformation("Content source: Contentful ({Environment}); registration reference data source: Contentful.", contentful.Environment);
}
else
{
    app.Logger.LogInformation("Content source: local JSON files in dotnet/data/; registration reference data source: local JSON.");
}

if (app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
    await VisitedPagesBackfill.RunAsync(
        dbContext,
        scope.ServiceProvider.GetRequiredService<ITrainingContentProvider>(),
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("VisitedPagesBackfill"));
}
else if (app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseExceptionHandler("/errors/internal-server-error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/errors/{0}");

app.UseHttpsRedirection();
app.UseGovUkFrontend();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
