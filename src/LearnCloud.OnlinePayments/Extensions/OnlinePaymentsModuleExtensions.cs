using LearnCloud.OnlinePayments.Services;
using LearnCloud.OnlinePayments.Services.Gateways;

namespace LearnCloud.OnlinePayments.Extensions;

public static class OnlinePaymentsModuleExtensions
{
    public static IServiceCollection AddLearnCloudOnlinePayments(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<PayNowOptions>(config.GetSection("Payments:PayNow"));
        services.AddHttpClient<PayNowGateway>();
        services.AddScoped<IOnlinePaymentService, OnlinePaymentService>();
        services.AddScoped<IPaymentGatewayFactory, PaymentGatewayFactory>();
        return services;
    }
}
