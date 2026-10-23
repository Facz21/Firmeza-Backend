using EnterpriseSystem.Application.Common.Interfaces;
using EnterpriseSystem.Application.Common.Models;
using EnterpriseSystem.Domain.Exceptions;
using MediatR;

namespace EnterpriseSystem.Application.Features.Auth.Queries.GetCurrentUser;

public record GetCurrentUserQuery : IRequest<AuthResult>;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, AuthResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IIdentityService _identityService;

    public GetCurrentUserQueryHandler(ICurrentUserService currentUserService, IIdentityService identityService)
    {
        _currentUserService = currentUserService;
        _identityService = identityService;
    }

    public async Task<AuthResult> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedException("User is not authenticated.");
        }

        var userId = _currentUserService.UserId.Value;
        var email = _currentUserService.Email ?? string.Empty;
        var fullName = await _identityService.GetUserNameAsync(userId, cancellationToken) ?? email;
        var roles = await _identityService.GetRolesAsync(userId, cancellationToken);

        return new AuthResult
        {
            Succeeded = true,
            UserId = userId,
            Email = email,
            FullName = fullName,
            Roles = roles
        };
    }
}
