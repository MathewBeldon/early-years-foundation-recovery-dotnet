using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260214120000_RailsBaseline")]
public sealed class RailsBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty. Rails schema 20260214120000 is the compatibility contract.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Rails-owned tables must never be dropped by .NET.
    }
}
