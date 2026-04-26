using IdentityServer.Data.Entities;

namespace IdentityServer.Services;

public interface IAccountService
{
    // Account queries
    Task<Account?> GetAccountByIdAsync(Guid accountId);
    Task<Account?> GetAccountWithDetailsAsync(Guid accountId);
    Task<IList<Account>> GetUserAccountsAsync(string userId);
    Task<Account?> GetUserAccountInTenantAsync(string userId, Guid tenantId);

    // Account management
    Task<Account> CreateAccountAsync(
        string userId, 
        Guid tenantId, 
        string? displayName = null,
        Guid? createdByAccountId = null);

    Task<Account> UpdateAccountAsync(Account account);
    Task<bool> DeleteAccountAsync(Guid accountId);
    Task<bool> SuspendAccountAsync(Guid accountId);
    Task<bool> ReactivateAccountAsync(Guid accountId);

    // Role assignment
    Task AssignRoleToAccountAsync(Guid accountId, Guid roleId, Guid? assignedByAccountId = null);
    Task RemoveRoleFromAccountAsync(Guid accountId, Guid roleId);
    Task<IList<Role>> GetAccountRolesAsync(Guid accountId);
    Task<IList<string>> GetAccountPermissionsAsync(Guid accountId);

    // Validation
    Task<bool> AccountExistsAsync(Guid accountId);
    Task<bool> UserHasAccountInTenantAsync(string userId, Guid tenantId);
    Task<bool> AccountHasPermissionAsync(Guid accountId, string permission);
}
