using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EnterpriseSystem.Application.Common.Interfaces;
using EnterpriseSystem.Application.Common.Models;
using EnterpriseSystem.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace EnterpriseSystem.Infrastructure.Identity;

public class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IConfiguration _configuration;

    public IdentityService(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IConfiguration configuration)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _configuration = configuration;
    }

    public async Task<AuthResult> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return AuthResult.Failure("No encontramos una cuenta registrada con ese correo o las credenciales son inválidas.");
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
            var remainingMinutes = lockoutEnd.HasValue
                ? Math.Max(1, (int)Math.Ceiling((lockoutEnd.Value - DateTimeOffset.UtcNow).TotalMinutes))
                : 15;

            return AuthResult.Failure(
                remainingMinutes > 60 * 24
                    ? "Esta cuenta ha sido bloqueada por un administrador. Contacta al administrador del sistema."
                    : $"Tu cuenta está bloqueada temporalmente por múltiples intentos fallidos. Intenta de nuevo en {remainingMinutes} minuto(s) o restablece tu contraseña.");
        }

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!isPasswordValid)
        {
            if (await _userManager.GetLockoutEnabledAsync(user))
            {
                await _userManager.AccessFailedAsync(user);
                if (await _userManager.IsLockedOutAsync(user))
                {
                    return AuthResult.Failure("Has superado los 5 intentos permitidos. Tu cuenta fue bloqueada por 15 minutos por seguridad.");
                }

                var failedCount = await _userManager.GetAccessFailedCountAsync(user);
                var remainingAttempts = Math.Max(1, 5 - failedCount);
                return AuthResult.Failure($"La contraseña ingresada es incorrecta. Te quedan {remainingAttempts} intento(s) antes del bloqueo temporal.");
            }

            return AuthResult.Failure("La contraseña ingresada es incorrecta. Verifica tus datos e inténtalo nuevamente.");
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiration) = GenerateJwtToken(user, roles);

        return AuthResult.Success(
            token,
            expiration,
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            roles.ToList());
    }

    public async Task<AuthResult> RegisterAsync(string email, string password, string fullName, string role, CancellationToken cancellationToken = default)
    {
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            return AuthResult.Failure($"Ya existe una cuenta registrada con el correo '{email}'.");
        }

        if (!Roles.All.Contains(role))
        {
            return AuthResult.Failure($"El rol '{role}' no es válido. Roles permitidos: {string.Join(", ", Roles.All)}.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = fullName.Trim(),
            EmailConfirmed = true,
            LockoutEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return AuthResult.Failure(FormatIdentityErrors(result.Errors));
        }

        if (!await _roleManager.RoleExistsAsync(role))
        {
            await _roleManager.CreateAsync(new ApplicationRole(role));
        }

        await _userManager.AddToRoleAsync(user, role);

        var roles = new List<string> { role };
        var (token, expiration) = GenerateJwtToken(user, roles);

        return AuthResult.Success(
            token,
            expiration,
            user.Id,
            user.Email ?? string.Empty,
            user.FullName,
            roles);
    }

    public async Task<bool> CheckRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return false;
        }

        return await _userManager.IsInRoleAsync(user, role);
    }

    public async Task<IReadOnlyList<string>> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Array.Empty<string>();
        }

        var roles = await _userManager.GetRolesAsync(user);
        return roles.ToList();
    }

    public async Task<string?> GetUserNameAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user?.FullName ?? user?.UserName;
    }

    public async Task<UserProfileDto?> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return null;
        }

        return await MapToUserProfileDtoAsync(user);
    }

    public async Task<IdentityOperationResult> UpdateProfileAsync(Guid userId, string fullName, string? phoneNumber, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return IdentityOperationResult.Failure("No se encontró el usuario.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            return IdentityOperationResult.Failure("El nombre completo es obligatorio.");
        }

        user.FullName = fullName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return IdentityOperationResult.Failure(FormatIdentityErrors(result.Errors));
        }

        return IdentityOperationResult.Success("Perfil actualizado correctamente.");
    }

    public async Task<IdentityOperationResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return IdentityOperationResult.Failure("No se encontró el usuario.");
        }

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            return IdentityOperationResult.Failure(FormatIdentityErrors(result.Errors));
        }

        return IdentityOperationResult.Success("Contraseña actualizada correctamente.");
    }

    public async Task<IdentityOperationResult> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return IdentityOperationResult.Failure("No se encontró ninguna cuenta asociada a ese correo electrónico.");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        return IdentityOperationResult.Success("Token de recuperación generado.", token);
    }

    public async Task<IdentityOperationResult> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            return IdentityOperationResult.Failure("No se encontró ninguna cuenta asociada a ese correo electrónico.");
        }

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            return IdentityOperationResult.Failure(FormatIdentityErrors(result.Errors));
        }

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        return IdentityOperationResult.Success("La contraseña ha sido restablecida y la cuenta desbloqueada.");
    }

    public async Task<IReadOnlyList<UserProfileDto>> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userManager.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(cancellationToken);

        var profiles = new List<UserProfileDto>(users.Count);
        foreach (var user in users)
        {
            profiles.Add(await MapToUserProfileDtoAsync(user));
        }

        return profiles;
    }

    public async Task<IdentityOperationResult> CreateUserByAdminAsync(string email, string password, string fullName, string? phoneNumber, string role, CancellationToken cancellationToken = default)
    {
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
        {
            return IdentityOperationResult.Failure($"Ya existe un usuario registrado con el correo '{email}'.");
        }

        if (!Roles.All.Contains(role))
        {
            return IdentityOperationResult.Failure($"El rol '{role}' no es válido. Roles permitidos: {string.Join(", ", Roles.All)}.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FullName = fullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
            EmailConfirmed = true,
            LockoutEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return IdentityOperationResult.Failure(FormatIdentityErrors(result.Errors));
        }

        if (!await _roleManager.RoleExistsAsync(role))
        {
            await _roleManager.CreateAsync(new ApplicationRole(role));
        }

        await _userManager.AddToRoleAsync(user, role);

        return IdentityOperationResult.Success($"Operador '{email}' creado con el rol '{role}'.");
    }

    public async Task<IdentityOperationResult> UpdateUserRoleAsync(Guid userId, string role, CancellationToken cancellationToken = default)
    {
        if (!Roles.All.Contains(role))
        {
            return IdentityOperationResult.Failure($"El rol '{role}' no es válido. Roles permitidos: {string.Join(", ", Roles.All)}.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return IdentityOperationResult.Failure("No se encontró el usuario.");
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        if (currentRoles.Any())
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!removeResult.Succeeded)
            {
                return IdentityOperationResult.Failure(FormatIdentityErrors(removeResult.Errors));
            }
        }

        if (!await _roleManager.RoleExistsAsync(role))
        {
            await _roleManager.CreateAsync(new ApplicationRole(role));
        }

        var addResult = await _userManager.AddToRoleAsync(user, role);
        if (!addResult.Succeeded)
        {
            return IdentityOperationResult.Failure(FormatIdentityErrors(addResult.Errors));
        }

        return IdentityOperationResult.Success($"Rol actualizado a '{role}'.");
    }

    public async Task<IdentityOperationResult> SetUserLockoutAsync(Guid userId, bool lockAccount, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return IdentityOperationResult.Failure("No se encontró el usuario.");
        }

        await _userManager.SetLockoutEnabledAsync(user, true);

        if (lockAccount)
        {
            var result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            if (!result.Succeeded)
            {
                return IdentityOperationResult.Failure(FormatIdentityErrors(result.Errors));
            }
            return IdentityOperationResult.Success("La cuenta de usuario ha sido bloqueada.");
        }
        else
        {
            var result = await _userManager.SetLockoutEndDateAsync(user, null);
            if (!result.Succeeded)
            {
                return IdentityOperationResult.Failure(FormatIdentityErrors(result.Errors));
            }
            await _userManager.ResetAccessFailedCountAsync(user);
            return IdentityOperationResult.Success("La cuenta de usuario ha sido desbloqueada.");
        }
    }

    public async Task<IdentityOperationResult> AdminResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return IdentityOperationResult.Failure("No se encontró el usuario.");
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            return IdentityOperationResult.Failure(FormatIdentityErrors(result.Errors));
        }

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        return IdentityOperationResult.Success("La contraseña del usuario ha sido restablecida por el administrador.");
    }

    public async Task<IdentityOperationResult> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return IdentityOperationResult.Failure("No se encontró el usuario.");
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return IdentityOperationResult.Failure(FormatIdentityErrors(result.Errors));
        }

        return IdentityOperationResult.Success("Cuenta de usuario eliminada.");
    }

    private static IEnumerable<string> FormatIdentityErrors(IEnumerable<IdentityError> errors)
    {
        return errors.Select(e => e.Code switch
        {
            "PasswordMismatch" => "La contraseña actual ingresada es incorrecta.",
            "InvalidToken" => "El token de seguridad es inválido o ha expirado. Solicita uno nuevo.",
            "DuplicateEmail" or "DuplicateUserName" => "Este correo electrónico ya se encuentra registrado en el sistema.",
            "InvalidEmail" => "El formato del correo electrónico no es válido.",
            "PasswordTooShort" => "La contraseña debe tener al menos 8 caracteres.",
            "PasswordRequiresNonAlphanumeric" => "La contraseña debe incluir al menos un carácter especial (ej. @, #, !, $).",
            "PasswordRequiresDigit" => "La contraseña debe incluir al menos un número (0-9).",
            "PasswordRequiresLower" => "La contraseña debe incluir al menos una letra minúscula (a-z).",
            "PasswordRequiresUpper" => "La contraseña debe incluir al menos una letra mayúscula (A-Z).",
            _ => e.Description
        });
    }

    public async Task<IReadOnlyList<RoleSummaryDto>> GetAllRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync(cancellationToken);
        var summaries = new List<RoleSummaryDto>(roles.Count);

        foreach (var role in roles)
        {
            var roleName = role.Name ?? string.Empty;
            var usersInRole = await _userManager.GetUsersInRoleAsync(roleName);
            summaries.Add(new RoleSummaryDto
            {
                Id = role.Id,
                Name = roleName,
                UserCount = usersInRole.Count
            });
        }

        return summaries;
    }

    private async Task<UserProfileDto> MapToUserProfileDtoAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var isLockedOut = await _userManager.IsLockedOutAsync(user);

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            EmailConfirmed = user.EmailConfirmed,
            TwoFactorEnabled = user.TwoFactorEnabled,
            LockoutEnabled = user.LockoutEnabled,
            IsLockedOut = isLockedOut,
            LockoutEnd = user.LockoutEnd,
            AccessFailedCount = user.AccessFailedCount,
            CreatedAt = user.CreatedAt,
            Roles = roles.ToList()
        };
    }

    private (string Token, DateTimeOffset Expiration) GenerateJwtToken(ApplicationUser user, IList<string> roles)
    {
        var secretKey = _configuration["Jwt:SecretKey"] ?? "EnterpriseSystem_SuperSecretSecureKey_2026_DotNet10_DefaultSigningKey!";
        var issuer = _configuration["Jwt:Issuer"] ?? "EnterpriseSystem";
        var audience = _configuration["Jwt:Audience"] ?? "EnterpriseSystemClient";
        var expiryMinutes = int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var minutes) ? minutes : 1440;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiration = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new(ClaimTypes.Name, user.FullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiration.UtcDateTime,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return (tokenHandler.WriteToken(token), expiration);
    }
}
