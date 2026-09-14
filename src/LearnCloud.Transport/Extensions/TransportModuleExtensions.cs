using LearnCloud.Transport.Services;

namespace LearnCloud.Transport.Extensions;

public static class TransportModuleExtensions
{
    public static IServiceCollection AddLearnCloudTransport(this IServiceCollection services)
    {
        services.AddScoped<ITransportService, TransportService>();
        return services;
    }
}
