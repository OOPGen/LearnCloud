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

        // FeesController names these policies; they were never registered, so every endpoint
        // using them failed with 500 ("AuthorizationPolicy ... was not found").
        services.AddAuthorization(options =>
        {
            options.AddPolicy("RequireFeesStructuresRead", p => p.RequireRole("BURSAR", "SCHOOL_ADMIN", "HEAD_TEACHER"));
            options.AddPolicy("RequireFeesInvoicesRead", p => p.RequireRole("BURSAR", "SCHOOL_ADMIN", "HEAD_TEACHER"));
            options.AddPolicy("RequireFeesReportsRead", p => p.RequireRole("BURSAR", "SCHOOL_ADMIN", "HEAD_TEACHER"));
        });
        return services;
    }
}
