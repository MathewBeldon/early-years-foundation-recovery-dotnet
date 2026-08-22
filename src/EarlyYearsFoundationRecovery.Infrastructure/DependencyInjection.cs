using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Infrastructure.Auth;
using EarlyYearsFoundationRecovery.Infrastructure.Contentful;
using EarlyYearsFoundationRecovery.Infrastructure.Feedback;
using EarlyYearsFoundationRecovery.Infrastructure.Jobs;
using EarlyYearsFoundationRecovery.Infrastructure.Notes;
using EarlyYearsFoundationRecovery.Infrastructure.StaticContent;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Infrastructure.ReferenceData;
using EarlyYearsFoundationRecovery.Infrastructure.Services;
using EarlyYearsFoundationRecovery.Infrastructure.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<InfrastructureOptions>()
            .Bind(configuration.GetSection(InfrastructureOptions.SectionName))
            .Validate(InfrastructureOptions.IsValid, "Infrastructure:PublicBaseUrl must be an absolute HTTP or HTTPS URL.")
            .ValidateOnStart();

        services.AddOptions<GovOneOptions>()
            .Bind(configuration.GetSection(GovOneOptions.SectionName))
            .PostConfigure<IHostEnvironment>((options, environment) =>
            {
                if (string.IsNullOrWhiteSpace(options.PrivateKeyPath))
                {
                    var defaultKeyPath = Path.GetFullPath(Path.Combine(
                        environment.ContentRootPath,
                        "..",
                        "..",
                        "keys",
                        "gov-one-simulator-private-key.pem"));
                    if (File.Exists(defaultKeyPath))
                    {
                        options.PrivateKeyPath = defaultKeyPath;
                    }
                }
            });

        services.AddOptions<ContentfulOptions>()
            .Bind(configuration.GetSection(ContentfulOptions.SectionName))
            .PostConfigure(options =>
            {
                options.SpaceId = options.SpaceId.Trim();
                options.Environment = options.Environment.Trim();
                options.DeliveryApiKey = options.DeliveryApiKey.Trim();
            });
        services.AddOptions<NotifyOptions>()
            .Bind(configuration.GetSection(NotifyOptions.SectionName));
        services.AddOptions<BackgroundJobOptions>()
            .Bind(configuration.GetSection(BackgroundJobOptions.SectionName))
            .Validate(BackgroundJobOptions.IsValid,
                "BackgroundJobs intervals must be positive; heartbeat must be less than half the lease, and recovery less than the lease.")
            .ValidateOnStart();

        services.AddMemoryCache();
        services.AddHttpClient(nameof(GovOneAuthService));
        services.AddHttpClient(nameof(ContentfulClientFactory));
        services.AddHttpClient<HttpNotifyService>((provider, client) =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<NotifyOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
            }
        });

        RegisterContentProviders(services, configuration);
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICloseAccountService, CloseAccountService>();
        services.AddScoped<ICourseFeedbackRepository, CourseFeedbackRepository>();
        services.AddScoped<IUserModuleProgressRepository, UserModuleProgressRepository>();
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<ITrainingAssessmentRepository, TrainingAssessmentRepository>();
        services.AddScoped<IGovOneAuthService, GovOneAuthService>();
        services.AddScoped<INotifyService, HttpNotifyService>();
        services.AddScoped<INotifyCallbackHandler, NotifyCallbackHandler>();
        services.AddScoped<IBackgroundJobService, PostgresBackgroundJobService>();
        services.AddScoped<IAnalyticsExportService, LocalFileAnalyticsExportService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddSingleton<IPdfGenerator, ChromiumPdfGenerator>();

        services.AddScoped<DashboardJob>();
        services.AddScoped<ContentCheckJob>();
        services.AddScoped<NewModuleReleaseJob>();
        services.AddScoped<NewModuleNotificationDeliveryJob>();
        services.AddHostedService<BackgroundJobWorker>();
        services.AddHostedService<DashboardExportScheduler>();

        return services;
    }

    private static void RegisterContentProviders(IServiceCollection services, IConfiguration configuration)
    {
        var contentful = configuration.GetSection(ContentfulOptions.SectionName).Get<ContentfulOptions>();
        if (contentful?.IsConfigured == true)
        {
            services.AddSingleton<ContentfulClientFactory>();
            services.AddSingleton<IContentfulContentCache, ContentfulContentCache>();
            services.AddSingleton<IReferenceDataProvider, ContentfulReferenceDataProvider>();
            services.AddSingleton<ITrainingContentProvider, ContentfulTrainingContentProvider>();
            services.AddSingleton<IContentfulModuleIntegrityCheck, ContentfulModuleIntegrityCheck>();
            services.AddSingleton<IFeedbackContentProvider, ContentfulFeedbackContentProvider>();
            services.AddSingleton<IStaticContentProvider, ContentfulStaticContentProvider>();
            return;
        }

        services.AddSingleton<IContentfulContentCache, NoOpContentfulContentCache>();
        services.AddSingleton<IReferenceDataProvider, JsonReferenceDataProvider>();
        services.AddSingleton<ITrainingContentProvider, JsonTrainingContentProvider>();
        services.AddSingleton<IContentfulModuleIntegrityCheck, UnavailableContentfulModuleIntegrityCheck>();
        services.AddSingleton<IFeedbackContentProvider, JsonFeedbackContentProvider>();
        services.AddSingleton<IStaticContentProvider, JsonStaticContentProvider>();
    }

    public static IServiceCollection AddPostgreSqlPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<NoteEncryptionOptions>()
            .Bind(configuration.GetSection(NoteEncryptionOptions.SectionName))
            .Services.AddSingleton<INoteBodyProtector>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<NoteEncryptionOptions>>().Value;
                var validation = NoteEncryptionOptions.Validate(options);
                if (!validation.IsValid)
                {
                    throw new InvalidOperationException(validation.Message);
                }

                return new RailsNoteBodyProtector(
                    options.PrimaryKey!,
                    options.KeyDerivationSalt!,
                    options.PreviousPrimaryKeys);
            });

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))
                .UseSnakeCaseNamingConvention());

        return services;
    }

    public static IServiceCollection AddInMemoryPersistence(
        this IServiceCollection services,
        string databaseName)
    {
        // The demo/test provider is the only supported passthrough boundary.
        // PostgreSQL registration above always supplies Rails encryption.
        services.AddSingleton<INoteBodyProtector, InMemoryNoteBodyProtector>();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));

        return services;
    }
}
