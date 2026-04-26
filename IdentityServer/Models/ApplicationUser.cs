// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using IdentityServer.Data.Entities;
using Microsoft.AspNetCore.Identity;

namespace IdentityServer.Models;

/// <summary>
/// Application user represents a human identity.
/// One user can have multiple accounts across different tenant types.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // Navigation - all accounts for this user
    public ICollection<Account> Accounts { get; set; } = new List<Account>();

    // Convenience accessors (not mapped to DB)
    public Account? CustomerAccount => Accounts.FirstOrDefault(a => a.TenantType == TenantType.Customer && a.Status == AccountStatus.Active);
    public Account? SystemAccount => Accounts.FirstOrDefault(a => a.TenantType == TenantType.System && a.Status == AccountStatus.Active);
    public IEnumerable<Account> ShopAccounts => Accounts.Where(a => a.TenantType == TenantType.Shop && a.Status == AccountStatus.Active);
}
