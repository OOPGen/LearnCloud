using LearnCloud.Fees.Services;

namespace LearnCloud.Fees.Extensions;

public static class FeesModuleExtensions
{
    public static IServiceCollection AddLearnCloudFees(this IServiceCollection services)
    {
        services.AddScoped<FeeCalculationService>();
        services.AddScoped<IArrearsService, ArrearsService>();
        services.AddScoped<IInvoiceGenerationService, InvoiceGenerationService>();
        services.AddScoped<IPaymentService, PaymentService>();
        return services;
    }
}
