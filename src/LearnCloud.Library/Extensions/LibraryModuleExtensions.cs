using LearnCloud.Library.Services;

namespace LearnCloud.Library.Extensions;

public static class LibraryModuleExtensions
{
    public static IServiceCollection AddLearnCloudLibrary(this IServiceCollection services)
    {
        services.AddScoped<ILibraryService, LibraryService>();
        return services;
    }
}
