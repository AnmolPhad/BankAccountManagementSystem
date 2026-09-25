using Microsoft.AspNetCore.Identity;

namespace BankAccountManagementSystem.API.Models
{
    /// <summary>
    /// Represents an application user.
    /// We inherit from IdentityUser which already provides:
    ///   - Id (string GUID)
    ///   - UserName
    ///   - Email / EmailConfirmed
    ///   - PhoneNumber / PhoneNumberConfirmed
    ///   - PasswordHash (hashed by Identity — we never store plain text)
    ///   - SecurityStamp, ConcurrencyStamp
    ///   - TwoFactorEnabled, LockoutEnd, AccessFailedCount
    ///
    /// We only add properties that IdentityUser does NOT already have.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        /// <summary>
        /// Unique employee identifier (e.g. "EMP-001").
        /// Used for internal tracking separate from email.
        /// </summary>
        public string EmployeeCode { get; set; } = string.Empty;

        /// <summary>
        /// User's first name.
        /// </summary>
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// User's last name.
        /// </summary>
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// UTC timestamp of when the user account was created.
        /// Useful for audit trails in a banking system.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Soft-delete flag.
        /// When false, the user is considered deactivated (not physically deleted).
        /// This is critical for a banking system because we must preserve
        /// the history of all transactions associated with a user, even
        /// if the user account is no longer active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        // ─── Convenience property (not stored in DB) ───────────────────────
        /// <summary>
        /// Returns the user's full name as a formatted string.
        /// Not mapped to a database column.
        /// </summary>
        public string FullName => $"{FirstName} {LastName}".Trim();

        /// <summary>
        /// Bank accounts owned by this user (1-to-many relationship).
        /// </summary>
        public ICollection<Account> Accounts { get; set; } = new List<Account>();
    }
}
