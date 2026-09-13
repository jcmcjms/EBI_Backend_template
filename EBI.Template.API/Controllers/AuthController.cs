using EBI.Template.Application.Features.Auth.Commands.Login;
using EBI.Template.Application.Features.Auth.Commands.RefreshToken;
using EBI.Template.Application.Features.Auth.Commands.Register;
using EBI.Template.Application.Features.Auth.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EBI.Template.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator) => _mediator = mediator;

    [HttpPost("login")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<ActionResult<AccessTokenResponse>> Login([FromBody] LoginCommand command)
    {
        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
            return Unauthorized(new { error = result.Error, code = result.ErrorCode });

        SetRefreshTokenCookie(result.RefreshToken!);
        return Ok(new AccessTokenResponse { AccessToken = result.AccessToken! });
    }

    [HttpPost("register")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<ActionResult<AccessTokenResponse>> Register([FromBody] RegisterCommand command)
    {
        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, code = result.ErrorCode });

        SetRefreshTokenCookie(result.RefreshToken!);
        return CreatedAtAction(nameof(Register), new AccessTokenResponse { AccessToken = result.AccessToken! });
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AccessTokenResponse>> Refresh()
    {
        if (!Request.Cookies.TryGetValue("refresh_token", out var refreshToken))
            return Unauthorized();

        var command = new RefreshTokenCommand(refreshToken);
        var result = await _mediator.Send(command);

        if (!result.IsSuccess)
            return Unauthorized(new { error = result.Error, code = result.ErrorCode });

        SetRefreshTokenCookie(result.RefreshToken!);
        return Ok(new AccessTokenResponse { AccessToken = result.AccessToken! });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("refresh_token", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict
        });
        return NoContent();
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(7)
        };
        Response.Cookies.Append("refresh_token", refreshToken, cookieOptions);
    }
}
