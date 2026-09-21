using Harmonia.API.Filters;

namespace Harmonia.API.Extensions;

public static class ControllersExtensions
{
    public static IServiceCollection AddApiControllers(this IServiceCollection services)
    {
        services
            .AddControllers(options =>
            {
                options.Filters.Add<ValidationFilter>();

                // Required-ness is decided by FluentValidation, which carries our error codes.
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                // ValidationFilter turns ModelState errors into ValidationException instead.
                options.SuppressModelStateInvalidFilter = true;
            });

        return services;
    }
}
