namespace BankAccountManagementSystem.API.Models
{
    /// <summary>
    /// Represents the outcome of a transaction attempt.
    ///
    /// Completed → The transaction succeeded: balances updated, record persisted.
    /// Failed    → Reserved for future use (e.g., async payment gateway failures).
    ///             In the current synchronous implementation, failed transactions throw
    ///             domain exceptions before reaching persistence, so we only persist
    ///             Completed records. If you later integrate a payment gateway,
    ///             you may persist a Failed record for auditing declined transactions.
    /// </summary>
    public enum TransactionStatus
    {
        Completed = 1,
        Failed    = 2
    }
}
