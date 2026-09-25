namespace BankAccountManagementSystem.API.Models
{
    /// <summary>
    /// Classifies what the transaction represents from a ledger perspective.
    ///
    /// Deposit  → Money coming INTO an account (credited).
    /// Withdrawal → Money going OUT of an account (debited).
    /// Transfer → Money moving from one Checking account to another (two-sided operation).
    ///
    /// Note: Savings accounts are restricted to Deposit and Withdrawal only.
    ///       Transfer (cheque transaction) is exclusive to Checking accounts as the source.
    /// </summary>
    public enum TransactionType
    {
        Deposit    = 1,
        Withdrawal = 2,
        Transfer   = 3
    }
}
