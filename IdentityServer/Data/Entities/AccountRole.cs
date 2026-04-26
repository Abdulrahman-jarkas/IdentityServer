namespace IdentityServer.Data.Entities;

public class AccountRole
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public Guid RoleId { get; set; }
    public Role? Role { get; set; }

    // Audit
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public Guid? AssignedByAccountId { get; set; }
    public Account? AssignedByAccount { get; set; }
}
