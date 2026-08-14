using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260214120000_RailsBaseline")]
public sealed class RailsBaseline : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty. The Rails schema is the compatibility contract; the
        // required version is RailsSchemaCompatibility.RequiredRailsVersion, currently
        // 20260529104000. This migration's own id is fixed bookkeeping that marks
        // "Rails owns everything before .NET's additions" and is deliberately not
        // renamed when the contract advances, so already-recorded history stays valid.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Rails-owned tables must never be dropped by .NET.
    }
}
