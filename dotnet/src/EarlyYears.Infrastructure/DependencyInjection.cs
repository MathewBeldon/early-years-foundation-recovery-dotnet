using EarlyYears.Infrastructure.Health;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EarlyYears.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEarlyYearsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var schemaOwner = configuration["Database:SchemaOwner"] ?? "Rails";
        if (!string.Equals(schemaOwner, "Rails", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Rails must remain the database schema owner during coexistence. " +
                "The .NET application never creates or applies migrations.");
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection must be configured.");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        services.AddSingleton(dataSourceBuilder.Build());
        services.AddHealthChecks()
            .AddCheck<PostgresConnectionHealthCheck>("postgres", tags: ["ready"]);

        return services;
    }
}
