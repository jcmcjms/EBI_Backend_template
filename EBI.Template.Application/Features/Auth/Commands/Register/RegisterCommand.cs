using EBI.Template.Application.Features.Auth.DTOs;
using MediatR;

namespace EBI.Template.Application.Features.Auth.Commands.Register;

public record RegisterCommand : IRequest<AuthResult>
{
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string ConfirmPassword { get; init; } = string.Empty;
}
