namespace BankAccountManagementSystem.API.DTOs.Accounts
{
    /// <summary>
    /// Response DTO returning safe public details of a bank account.
    /// </summary>
    public class AccountResponse
    {
        public Guid AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
