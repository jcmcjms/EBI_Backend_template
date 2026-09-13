namespace EBI.Template.Application.Features.Auth.DTOs;

public class AccessTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}
