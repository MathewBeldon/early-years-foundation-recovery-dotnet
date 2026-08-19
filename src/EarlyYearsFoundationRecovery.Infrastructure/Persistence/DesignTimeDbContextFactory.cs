using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using EarlyYearsFoundationRecovery.Infrastructure.Notes;

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../EarlyYearsFoundationRecovery.Web"))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
            .UseSnakeCaseNamingConvention();

        var noteEncryption = configuration
            .GetSection(NoteEncryptionOptions.SectionName)
            .Get<NoteEncryptionOptions>()
            ?? new NoteEncryptionOptions();
        var validation = NoteEncryptionOptions.Validate(noteEncryption);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Message);
        }

        return new ApplicationDbContext(
            optionsBuilder.Options,
            new RailsNoteBodyProtector(
                noteEncryption.PrimaryKey!,
                noteEncryption.KeyDerivationSalt!,
                noteEncryption.PreviousPrimaryKeys));
    }
}
