using System.Reflection;
using EarlyYearsFoundationRecovery.Application.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace EarlyYearsFoundationRecovery.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<Training.ModuleProgressService>();
        services.AddScoped<Training.AssessmentProgressService>();
        services.AddScoped<Training.QuestionAnswerService>();
        services.AddScoped<Feedback.CourseFeedbackService>();

        return services;
    }
}
