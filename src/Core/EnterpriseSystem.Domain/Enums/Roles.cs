namespace EnterpriseSystem.Domain.Enums;

public static class Roles
{
    public const string Administrator = "Administrator";
    public const string Auditor = "Auditor";
    public const string StandardUser = "StandardUser";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Administrator,
        Auditor,
        StandardUser
    };
}
