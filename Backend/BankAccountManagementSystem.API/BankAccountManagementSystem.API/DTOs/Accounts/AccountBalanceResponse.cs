namespace BankAccountManagementSystem.API.DTOs.Accounts
{
    /// <summary>
    /// Response DTO containing account balance information.
    /// Excludes unnecessary internal fields while providing full balance transparency.
    /// </summary>
    public class AccountBalanceResponse
    {
        public Guid AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public bool IsActive { get; set; }
    }
}
