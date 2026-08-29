using Microsoft.AspNetCore.Identity;

namespace AtlasNOC.Domain.Identity;

/// <summary>Usuario humano de la plataforma. El login humano siempre usa Identity (usuario+contraseña).</summary>
public class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ApplicationUser()
    {
    }

    public ApplicationUser(string userName)
    {
        UserName = userName ?? throw new ArgumentNullException(nameof(userName));
    }
}