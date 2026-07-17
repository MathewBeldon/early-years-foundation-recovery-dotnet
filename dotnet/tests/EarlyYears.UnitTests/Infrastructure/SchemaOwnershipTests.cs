using EarlyYears.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EarlyYears.UnitTests.Infrastructure;

public sealed class SchemaOwnershipTests
{
    [Fact]
    public void InfrastructureRejectsNonRailsSchemaOwner()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Database:SchemaOwner"] = "DotNet",
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test",
        });

        var action = () => new ServiceCollection()
            .AddEarlyYearsInfrastructure(configuration);

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Contains("Rails must remain the database schema owner", exception.Message);
    }

    [Fact]
    public void InfrastructureRegistersPostgresConnectivityWithoutMigrationServices()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Database:SchemaOwner"] = "Rails",
            ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=postgres;Password=password",
        });

        var services = new ServiceCollection()
            .AddLogging()
            .AddEarlyYearsInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<NpgsqlDataSource>());
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType.FullName?.Contains("Migrations", StringComparison.Ordinal) == true);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
