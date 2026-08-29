using Microsoft.AspNetCore.Identity;

namespace AtlasNOC.Domain.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public const string Administrator = "Administrator";
    public const string NocOperator = "NocOperator";
    public const string ReadOnly = "ReadOnly";

    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName) : base(roleName)
    {
    }
}