using EnterpriseSystem.Application.Common.Interfaces;
using EnterpriseSystem.Application.Common.Models;
using EnterpriseSystem.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseSystem.WebApi.Controllers;

public record AdminCreateUserRequest(
    string Email,
    string Password,
    string FullName,
    string? PhoneNumber,
    string Role);

public record UpdateUserRoleRequest(string Role);
public record SetUserLockoutRequest(bool LockAccount);
public record AdminResetPasswordRequest(string NewPassword);

[Authorize(Roles = Roles.Administrator)]
public class UsersController : ApiControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly ICurrentUserService _currentUserService;

    public UsersController(IIdentityService identityService, ICurrentUserService currentUserService)
    {
        _identityService = identityService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UserProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserProfileDto>>> GetAllUsers(CancellationToken cancellationToken)
    {
        var users = await _identityService.GetAllUsersAsync(cancellationToken);
        return Ok(users);
    }

    [HttpGet("roles")]
    [ProducesResponseType(typeof(IReadOnlyList<RoleSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoleSummaryDto>>> GetAllRoles(CancellationToken cancellationToken)
    {
        var roles = await _identityService.GetAllRolesAsync(cancellationToken);
        return Ok(roles);
    }

    [HttpPost]
    [ProducesResponseType(typeof(IdentityOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IdentityOperationResult>> CreateUser([FromBody] AdminCreateUserRequest request, CancellationToken cancellationToken)
    {
        var result = await _identityService.CreateUserByAdminAsync(
            request.Email,
            request.Password,
            request.FullName,
            request.PhoneNumber,
            request.Role,
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "User Creation Failed",
                Detail = string.Join("; ", result.Errors)
            });
        }

        return Ok(result);
    }

    [HttpPut("{id:guid}/role")]
    [ProducesResponseType(typeof(IdentityOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IdentityOperationResult>> UpdateUserRole(Guid id, [FromBody] UpdateUserRoleRequest request, CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId == id && request.Role != Roles.Administrator)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Action Restricted",
                Detail = "You cannot downgrade your own Administrator role."
            });
        }

        var result = await _identityService.UpdateUserRoleAsync(id, request.Role, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Role Update Failed",
                Detail = string.Join("; ", result.Errors)
            });
        }

        return Ok(result);
    }

    [HttpPut("{id:guid}/lockout")]
    [ProducesResponseType(typeof(IdentityOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IdentityOperationResult>> SetUserLockout(Guid id, [FromBody] SetUserLockoutRequest request, CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId == id && request.LockAccount)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Action Restricted",
                Detail = "You cannot lock your own active administrator account."
            });
        }

        var result = await _identityService.SetUserLockoutAsync(id, request.LockAccount, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Lockout Update Failed",
                Detail = string.Join("; ", result.Errors)
            });
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/reset-password")]
    [ProducesResponseType(typeof(IdentityOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IdentityOperationResult>> AdminResetPassword(Guid id, [FromBody] AdminResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _identityService.AdminResetPasswordAsync(id, request.NewPassword, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Password Reset Failed",
                Detail = string.Join("; ", result.Errors)
            });
        }

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(typeof(IdentityOperationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IdentityOperationResult>> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        if (_currentUserService.UserId == id)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Action Restricted",
                Detail = "You cannot delete your own active administrator account."
            });
        }

        var result = await _identityService.DeleteUserAsync(id, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "User Deletion Failed",
                Detail = string.Join("; ", result.Errors)
            });
        }

        return Ok(result);
    }
}
