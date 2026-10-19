using Template.Api.Features.Auth;
using Template.Api.Features.Roles;
using Template.Api.Features.Users;
using Template.Api.Infrastructure.Http;

namespace Template.Api.Composition;

/// <summary>
/// Registers application services: shared infrastructure, error/validation/OpenAPI plumbing,
/// and every feature use case. Registration is explicit by design — no assembly scanning.
/// </summary>
public static class ApplicationComposition
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);

        services.AddExceptionHandler<UnhandledExceptionHandler>();
        services.AddProblemDetails();

        // .NET 10 built-in validation: DataAnnotations on request DTOs are enforced
        // before handlers run and produce 400 ValidationProblemDetails responses.
        services.AddValidation();

        services.AddOpenApi();

        // Auth feature
        services.AddScoped<LoginUseCase>();
        services.AddScoped<RefreshSessionUseCase>();
        services.AddScoped<LogoutUseCase>();

        // Users feature
        services.AddScoped<GetCurrentUserUseCase>();
        services.AddScoped<ListUsersUseCase>();
        services.AddScoped<GetUserUseCase>();
        services.AddScoped<CreateUserUseCase>();
        services.AddScoped<UpdateUserUseCase>();
        services.AddScoped<UpdateUserProfileUseCase>();
        services.AddScoped<SetUserRolesUseCase>();
        services.AddScoped<ResetUserPasswordUseCase>();
        services.AddScoped<ChangeOwnPasswordUseCase>();

        // Roles feature
        services.AddScoped<ListRolesUseCase>();
        services.AddScoped<CreateRoleUseCase>();
        services.AddScoped<UpdateRoleUseCase>();
        services.AddScoped<DeleteRoleUseCase>();

        return services;
    }
}
