using Microsoft.Extensions.DependencyInjection;

namespace RewindFix.AspNetCore;

public static class RewindFixServiceCollectionExtensions
{
    public static IServiceCollection AddRewindFix(
        this IServiceCollection services,
        Action<RewindFixOptions>? configure = null)
    {
        var options = new RewindFixOptions();

        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<CrashReportQueue>();

        services.AddHttpClient("RewindFix");

        services.AddHostedService<CrashReportSender>();

        return services;
    }
}