using BankAccountManagementSystem.API.Models;

namespace BankAccountManagementSystem.API.DTOs.System
{
    /// <summary>
    /// Represents the serialized state of a bank account in a system backup payload.
    /// Excludes RowVersion so that SQL Server manages concurrency tokens upon restoration.
    /// </summary>
    public class AccountBackupDto
    {
        public Guid AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int AccountType { get; set; }
        public decimal Balance { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
