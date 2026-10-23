namespace EnterpriseSystem.Application.Common.Models;

public class UserProfileDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public bool LockoutEnabled { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public int AccessFailedCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

public class RoleSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int UserCount { get; set; }
}

public class IdentityOperationResult
{
    public bool Succeeded { get; set; }
    public string? Message { get; set; }
    public string? ResetToken { get; set; }
    public IEnumerable<string> Errors { get; set; } = Array.Empty<string>();

    public static IdentityOperationResult Success(string? message = null, string? resetToken = null)
    {
        return new IdentityOperationResult
        {
            Succeeded = true,
            Message = message,
            ResetToken = resetToken
        };
    }

    public static IdentityOperationResult Failure(IEnumerable<string> errors)
    {
        return new IdentityOperationResult
        {
            Succeeded = false,
            Errors = errors
        };
    }

    public static IdentityOperationResult Failure(string error)
    {
        return new IdentityOperationResult
        {
            Succeeded = false,
            Errors = new[] { error }
        };
    }
}
