namespace BankAccountManagementSystem.API.DTOs.Users
{
    /// <summary>
    /// The outbound DTO used whenever we return a user to the caller.
    /// This is what all User endpoints return — never the raw ApplicationUser.
    ///
    /// We deliberately exclude:
    ///   - PasswordHash
    ///   - SecurityStamp
    ///   - ConcurrencyStamp
    ///   - NormalizedEmail / NormalizedUserName
    ///   - LockoutEnabled / AccessFailedCount (internal)
    /// </summary>
    public class UserResponse
    {
        public string Id { get; set; } = string.Empty;
        public string EmployeeCode { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
