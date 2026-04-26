namespace IdentityServer.Constants;

/// <summary>
/// Permission constants organized by resource.
/// Format: {resource}.{action}
/// </summary>
public static class Permissions
{
    /// <summary>
    /// User management permissions.
    /// </summary>
    public static class Users
    {
        public const string Read = "users.read";
        public const string Create = "users.create";
        public const string Update = "users.update";
        public const string Delete = "users.delete";
        public const string Lock = "users.lock";
    }

    /// <summary>
    /// Account management permissions.
    /// </summary>
    public static class Accounts
    {
        public const string Read = "accounts.read";
        public const string Create = "accounts.create";
        public const string Update = "accounts.update";
        public const string Delete = "accounts.delete";
    }

    /// <summary>
    /// Role management permissions.
    /// </summary>
    public static class Roles
    {
        public const string Read = "roles.read";
        public const string Create = "roles.create";
        public const string Update = "roles.update";
        public const string Delete = "roles.delete";
    }

    /// <summary>
    /// Analytics permissions.
    /// </summary>
    public static class Analytics
    {
        public const string Read = "analytics.read";
    }

    /// <summary>
    /// Order permissions.
    /// </summary>
    public static class Orders
    {
        public const string Read = "orders.read";
        public const string Create = "orders.create";
        public const string Update = "orders.update";
        public const string Cancel = "orders.cancel";
    }

    /// <summary>
    /// Menu permissions.
    /// </summary>
    public static class Menu
    {
        public const string Read = "menu.read";
        public const string Manage = "menu.manage";
    }

    /// <summary>
    /// Reports permissions.
    /// </summary>
    public static class Reports
    {
        public const string Read = "reports.read";
    }

    /// <summary>
    /// Profile permissions.
    /// </summary>
    public static class Profile
    {
        public const string Read = "profile.read";
        public const string Update = "profile.update";
    }

    /// <summary>
    /// Address permissions.
    /// </summary>
    public static class Addresses
    {
        public const string Manage = "addresses.manage";
    }

    /// <summary>
    /// Favorites permissions.
    /// </summary>
    public static class Favorites
    {
        public const string Manage = "favorites.manage";
    }

    /// <summary>
    /// Delivery permissions.
    /// </summary>
    public static class Deliveries
    {
        public const string Read = "deliveries.read";
        public const string Update = "deliveries.update";
    }

    /// <summary>
    /// Driver status permissions.
    /// </summary>
    public static class Status
    {
        public const string Update = "status.update";
    }

    /// <summary>
    /// Earnings permissions.
    /// </summary>
    public static class Earnings
    {
        public const string Read = "earnings.read";
    }

    /// <summary>
    /// Inventory permissions.
    /// </summary>
    public static class Inventory
    {
        public const string Read = "inventory.read";
        public const string Manage = "inventory.manage";
    }
}
