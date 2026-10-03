using EbiBackend.Features.Auth.Contracts;
using EbiBackend.Features.Users.Contracts;
using EbiBackend.Features.Users.Services;

namespace EbiBackend.Features.Users.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users").WithTags("Users").RequireAuthorization();

        group.MapGet("/", async (
            [AsParameters] UserFilterRequest filter,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var result = await userService.GetUsersAsync(filter, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(new { Error = result.Error });
        })
        .WithName("GetUsers")
        .Produces<List<UserProfileResponse>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (
            Guid id,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var result = await userService.GetUserByIdAsync(id, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { Error = result.Error });
        })
        .WithName("GetUserById")
        .Produces<UserProfileResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdateUserRequest request,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var result = await userService.UpdateUserAsync(id, request, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { Error = result.Error });
        })
        .WithName("UpdateUser")
        .Produces<UserProfileResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            IUserService userService,
            CancellationToken cancellationToken) =>
        {
            var result = await userService.DeleteUserAsync(id, cancellationToken);

            return result.IsSuccess
                ? Results.NoContent()
                : Results.NotFound(new { Error = result.Error });
        })
        .WithName("DeleteUser")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }
}