namespace EnterpriseSystem.Application.Common.Models;

public class AuthResult
{
    public bool Succeeded { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTimeOffset? Expiration { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IEnumerable<string> Errors { get; set; } = Array.Empty<string>();

    public static AuthResult Success(string token, DateTimeOffset expiration, Guid userId, string email, string fullName, IReadOnlyList<string> roles)
    {
        return new AuthResult
        {
            Succeeded = true,
            Token = token,
            Expiration = expiration,
            UserId = userId,
            Email = email,
            FullName = fullName,
            Roles = roles
        };
    }

    public static AuthResult Failure(IEnumerable<string> errors)
    {
        return new AuthResult
        {
            Succeeded = false,
            Errors = errors
        };
    }

    public static AuthResult Failure(string error)
    {
        return new AuthResult
        {
            Succeeded = false,
            Errors = new[] { error }
        };
    }
}
