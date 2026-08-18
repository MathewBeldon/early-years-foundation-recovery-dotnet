using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

internal static class IntegrationTestHost
{
    public static void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
        });

        builder.ConfigureServices(services =>
            services.AddDataProtection()
                .UseEphemeralDataProtectionProvider());
    }
}
