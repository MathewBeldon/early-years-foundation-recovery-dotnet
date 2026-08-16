using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence.Migrations;

/// <summary>
/// Records the corrected EF mapping for Rails' current setting_type_id column.
/// Both setting_type and setting_type_id are Rails-owned and already exist, so
/// applying or rolling back this migration must not alter either column.
/// </summary>
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260816120000_MapRailsSettingTypeId")]
public sealed class MapRailsSettingTypeId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
