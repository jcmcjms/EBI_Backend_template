namespace EBI.Template.Application.Features.Auth.DTOs;

public class AuthResult
{
    public bool IsSuccess { get; private set; }
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public string? Error { get; private set; }
    public string? ErrorCode { get; private set; }

    private AuthResult() { }

    public static AuthResult Success(AccessTokenResponse response) => new()
    {
        IsSuccess = true,
        AccessToken = response.AccessToken,
        RefreshToken = response.RefreshToken
    };

    public static AuthResult Failure(string error, string errorCode) => new()
    {
        IsSuccess = false,
        Error = error,
        ErrorCode = errorCode
    };
}
