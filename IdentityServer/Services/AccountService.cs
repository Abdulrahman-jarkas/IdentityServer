using IdentityServer.Data;
using IdentityServer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityServer.Services;

public class AccountService : IAccountService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        ApplicationDbContext context,
        ILogger<AccountService> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Account Queries

    public async Task<Account?> GetAccountByIdAsync(Guid accountId)
    {
        return await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId);
    }

    public async Task<Account?> GetAccountWithDetailsAsync(Guid accountId)
    {
        return await _context.Accounts
            .Include(a => a.AccountRoles)
                .ThenInclude(ar => ar.Role)
            .FirstOrDefaultAsync(a => a.Id == accountId);
    }

    public async Task<IList<Account>> GetUserAccountsAsync(string userId)
    {
        return await _context.Accounts
            .Include(a => a.AccountRoles)
                .ThenInclude(ar => ar.Role)
            .Where(a => a.UserId == userId && a.Status != AccountStatus.Deleted)
            .OrderBy(a => a.TenantType)
            .ThenBy(a => a.DisplayName)
            .ToListAsync();
    }

    public async Task<Account?> GetUserAccountInTenantAsync(string userId, Guid tenantId)
    {
        return await _context.Accounts
            .Include(a => a.AccountRoles)
                .ThenInclude(ar => ar.Role)
            .FirstOrDefaultAsync(a => a.UserId == userId && a.TenantId == tenantId);
    }

    #endregion

    #region Account Management

    public async Task<Account> CreateAccountAsync(
        string userId,
        Guid tenantId,
        string? displayName = null,
        Guid? createdByAccountId = null)
    {
        // Check if account already exists
        var existingAccount = await GetUserAccountInTenantAsync(userId, tenantId);
        if (existingAccount != null)
        {
            throw new InvalidOperationException(
                $"User {userId} already has an account in tenant {tenantId}");
        }

        var account = new Account
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            DisplayName = displayName,
            Status = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow,
            CreatedByAccountId = createdByAccountId
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created account {AccountId} for user {UserId} in tenant {TenantId}",
            account.Id, userId, tenantId);

        return account;
    }

    public async Task<Account> UpdateAccountAsync(Account account)
    {
        _context.Accounts.Update(account);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated account {AccountId}", account.Id);

        return account;
    }

    public async Task<bool> DeleteAccountAsync(Guid accountId)
    {
        var account = await GetAccountByIdAsync(accountId);
        if (account == null)
        {
            return false;
        }

        // Soft delete - refresh security stamp to invalidate tokens
        account.Status = AccountStatus.Deleted;
        account.RefreshSecurityStamp();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted account {AccountId}, security stamp refreshed", accountId);

        return true;
    }

    public async Task<bool> SuspendAccountAsync(Guid accountId)
    {
        var account = await GetAccountByIdAsync(accountId);
        if (account == null)
        {
            return false;
        }

        account.Status = AccountStatus.Suspended;
        account.RefreshSecurityStamp();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Suspended account {AccountId}, security stamp refreshed", accountId);

        return true;
    }

    public async Task<bool> ReactivateAccountAsync(Guid accountId)
    {
        var account = await GetAccountByIdAsync(accountId);
        if (account == null)
        {
            return false;
        }

        account.Status = AccountStatus.Active;
        account.RefreshSecurityStamp();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Reactivated account {AccountId}, security stamp refreshed", accountId);

        return true;
    }

    #endregion

    #region Role Assignment

    public async Task AssignRoleToAccountAsync(
        Guid accountId, 
        Guid roleId, 
        Guid? assignedByAccountId = null)
    {
        // Check if already assigned
        var existingAssignment = await _context.AccountRoles
            .FirstOrDefaultAsync(ar => ar.AccountId == accountId && ar.RoleId == roleId);

        if (existingAssignment != null)
        {
            _logger.LogDebug("Role {RoleId} already assigned to account {AccountId}", 
                roleId, accountId);
            return;
        }

        var accountRole = new AccountRole
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow,
            AssignedByAccountId = assignedByAccountId
        };

        _context.AccountRoles.Add(accountRole);

        // Refresh security stamp - roles changed
        var account = await GetAccountByIdAsync(accountId);
        if (account != null)
        {
            account.RefreshSecurityStamp();
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Assigned role {RoleId} to account {AccountId}, security stamp refreshed", 
            roleId, accountId);
    }

    public async Task RemoveRoleFromAccountAsync(Guid accountId, Guid roleId)
    {
        var accountRole = await _context.AccountRoles
            .FirstOrDefaultAsync(ar => ar.AccountId == accountId && ar.RoleId == roleId);

        if (accountRole == null)
        {
            return;
        }

        _context.AccountRoles.Remove(accountRole);

        // Refresh security stamp - roles changed
        var account = await GetAccountByIdAsync(accountId);
        if (account != null)
        {
            account.RefreshSecurityStamp();
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Removed role {RoleId} from account {AccountId}, security stamp refreshed", 
            roleId, accountId);
    }

    public async Task<IList<Role>> GetAccountRolesAsync(Guid accountId)
    {
        return await _context.AccountRoles
            .Where(ar => ar.AccountId == accountId)
            .Include(ar => ar.Role)
            .Select(ar => ar.Role!)
            .ToListAsync();
    }

    public async Task<IList<string>> GetAccountPermissionsAsync(Guid accountId)
    {
        var roles = await _context.AccountRoles
            .Where(ar => ar.AccountId == accountId)
            .Include(ar => ar.Role)
            .Select(ar => ar.Role!)
            .ToListAsync();

        return roles
            .SelectMany(r => r.GetPermissions())
            .Distinct()
            .ToList();
    }

    #endregion

    #region Validation

    public async Task<bool> AccountExistsAsync(Guid accountId)
    {
        return await _context.Accounts
            .AnyAsync(a => a.Id == accountId && a.Status != AccountStatus.Deleted);
    }

    public async Task<bool> UserHasAccountInTenantAsync(string userId, Guid tenantId)
    {
        return await _context.Accounts
            .AnyAsync(a => a.UserId == userId && 
                          a.TenantId == tenantId && 
                          a.Status != AccountStatus.Deleted);
    }

    public async Task<bool> AccountHasPermissionAsync(Guid accountId, string permission)
    {
        var roles = await _context.AccountRoles
            .Where(ar => ar.AccountId == accountId)
            .Include(ar => ar.Role)
            .Select(ar => ar.Role!)
            .ToListAsync();

        return roles.Any(r => r.HasPermission(permission));
    }

    #endregion
}
