using EnterpriseSystem.Application.Common.Models;

namespace EnterpriseSystem.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<AuthResult> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<AuthResult> RegisterAsync(string email, string password, string fullName, string role, CancellationToken cancellationToken = default);
    Task<bool> CheckRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<string?> GetUserNameAsync(Guid userId, CancellationToken cancellationToken = default);

    // Account & Profile Management
    Task<UserProfileDto?> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> UpdateProfileAsync(Guid userId, string fullName, string? phoneNumber, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default);

    // Admin Users & Roles Management
    Task<IReadOnlyList<UserProfileDto>> GetAllUsersAsync(CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> CreateUserByAdminAsync(string email, string password, string fullName, string? phoneNumber, string role, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> UpdateUserRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> SetUserLockoutAsync(Guid userId, bool lockAccount, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> AdminResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default);
    Task<IdentityOperationResult> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleSummaryDto>> GetAllRolesAsync(CancellationToken cancellationToken = default);
}
