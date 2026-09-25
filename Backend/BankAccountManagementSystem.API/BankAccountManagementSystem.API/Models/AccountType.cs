namespace BankAccountManagementSystem.API.Models
{
    /// <summary>
    /// Represents the type of bank account.
    /// 
    /// - Savings: Designed for personal savings. In later phases, restricted strictly to cash transactions on a single account.
    /// - Checking: Designed for day-to-day transactions. In later phases, permits cash and cheque transactions (inter-account transfers).
    /// </summary>
    public enum AccountType
    {
        Savings = 1,
        Checking = 2
    }
}
