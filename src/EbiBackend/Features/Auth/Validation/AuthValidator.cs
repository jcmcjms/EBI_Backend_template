using EbiBackend.Features.Auth.Contracts;

namespace EbiBackend.Features.Auth.Validation;

public static class AuthValidator
{
    public static string[] ValidateRegisterRequest(RegisterRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Email))
            errors.Add("Email is required.");
        else if (!request.Email.Contains('@'))
            errors.Add("Email is not valid.");

        if (string.IsNullOrWhiteSpace(request.Password))
            errors.Add("Password is required.");
        else if (request.Password.Length < 8)
            errors.Add("Password must be at least 8 characters.");

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            errors.Add("Display name is required.");

        return [.. errors];
    }

    public static string[] ValidateLoginRequest(LoginRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Email))
            errors.Add("Email is required.");

        if (string.IsNullOrWhiteSpace(request.Password))
            errors.Add("Password is required.");

        return [.. errors];
    }

    public static string[] ValidateRefreshRequest(RefreshRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            errors.Add("Refresh token is required.");

        return [.. errors];
    }
}