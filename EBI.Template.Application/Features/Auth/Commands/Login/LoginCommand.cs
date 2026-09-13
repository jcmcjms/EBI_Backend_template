using EBI.Template.Application.Features.Auth.DTOs;
using MediatR;

namespace EBI.Template.Application.Features.Auth.Commands.Login;

public record LoginCommand : IRequest<AuthResult>
{
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
