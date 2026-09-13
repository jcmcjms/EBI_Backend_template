using EBI.Template.Application.Features.Auth.DTOs;
using MediatR;

namespace EBI.Template.Application.Features.Auth.Commands.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : IRequest<AuthResult>;
