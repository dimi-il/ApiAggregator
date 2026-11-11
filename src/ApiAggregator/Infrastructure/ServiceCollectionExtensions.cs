using ApiAggregator.Clients;

namespace ApiAggregator.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers an external API client with a typed HttpClient and the standard resilience pipeline
    /// (retry with exponential backoff + jitter for transient errors, per-attempt and total timeouts,
    /// circuit breaker) and exposes it as IExternalApiClient so the aggregator discovers it.
    /// </summary>
    public static IServiceCollection AddExternalApiClient<TClient>(
        this IServiceCollection services, Action<HttpClient>? configure = null)
        where TClient : class, IExternalApiClient
    {
        var builder = configure is null ? services.AddHttpClient<TClient>() : services.AddHttpClient<TClient>(configure);

        builder.AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddTransient<IExternalApiClient>(sp => sp.GetRequiredService<TClient>());
        return services;
    }
}
