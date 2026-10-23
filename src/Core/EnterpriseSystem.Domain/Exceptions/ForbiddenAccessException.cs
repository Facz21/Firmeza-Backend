namespace EnterpriseSystem.Domain.Exceptions;

public class UnauthorizedException : DomainException
{
    public UnauthorizedException() : base("User is unauthorized.") { }

    public UnauthorizedException(string message) : base(message) { }
}

public class ForbiddenAccessException : DomainException
{
    public ForbiddenAccessException() : base("User does not have permission to access this resource.") { }

    public ForbiddenAccessException(string message) : base(message) { }
}
