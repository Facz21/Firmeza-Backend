namespace EnterpriseSystem.Domain.Exceptions;

public class NotFoundException : DomainException
{
    public NotFoundException() : base("The requested resource was not found.") { }

    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string name, object key)
        : base($"Entity \"{name}\" ({key}) was not found.") { }
}
