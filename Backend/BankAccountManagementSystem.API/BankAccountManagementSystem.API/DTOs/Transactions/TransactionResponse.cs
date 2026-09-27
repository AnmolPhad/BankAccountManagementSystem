using BankAccountManagementSystem.API.Models;

namespace BankAccountManagementSystem.API.DTOs.Transactions
{
    /// <summary>
    /// Outbound DTO returned by all transaction endpoints (Deposit, Withdrawal, Transfer).
    ///
    /// Safe fields only — no internal IDs, no balance that could be tampered with,
    /// no user identifiers from the database.
    ///
    /// For Transfer operations, two TransactionResponse objects are returned in the response
    /// (one for the source debit, one for the destination credit).
    /// </summary>
    public class TransactionResponse
    {
        /// <summary>
        /// Unique transaction identifier (GUID). Useful for support queries and audit.
        /// </summary>
        public Guid TransactionId { get; set; }

        /// <summary>
        /// The account this ledger entry belongs to.
        /// </summary>
        public Guid AccountId { get; set; }

        /// <summary>
        /// Human-readable bank account number (e.g. "1012345678").
        /// </summary>
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// The direction of this ledger entry (Deposit, Withdrawal, Transfer).
        /// </summary>
        public string TransactionType { get; set; } = string.Empty;

        /// <summary>
        /// The payment instrument used (Cash or Cheque).
        /// </summary>
        public string TransactionMode { get; set; } = string.Empty;

        /// <summary>
        /// The absolute monetary amount (always positive).
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Account balance AFTER this transaction was applied.
        /// Snapshot of the account balance at transaction time.
        /// </summary>
        public decimal BalanceAfterTransaction { get; set; }

        /// <summary>
        /// UTC timestamp of when the transaction was recorded.
        /// </summary>
        public DateTime TransactionDate { get; set; }

        /// <summary>
        /// Optional caller-supplied note.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Server-generated unique reference number (e.g. "TXN-20260925-AB12CD34").
        /// </summary>
        public string ReferenceNumber { get; set; } = string.Empty;

        /// <summary>
        /// For Transfer transactions: the account on the other side.
        /// Null for Deposit and Withdrawal.
        /// </summary>
        public Guid? RelatedAccountId { get; set; }

        /// <summary>
        /// Transaction outcome (Completed or Failed).
        /// </summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Full name of the sender for transfer transactions.
        /// </summary>
        public string? SenderName { get; set; }

        /// <summary>
        /// Full name of the receiver for transfer transactions.
        /// </summary>
        public string? ReceiverName { get; set; }

        /// <summary>
        /// Account number of the sender for transfer transactions.
        /// </summary>
        public string? SenderAccountNumber { get; set; }

        /// <summary>
        /// Account number of the receiver for transfer transactions.
        /// </summary>
        public string? ReceiverAccountNumber { get; set; }

        /// <summary>
        /// True if this transfer is between two accounts owned by the same customer.
        /// </summary>
        public bool IsSelfTransfer { get; set; }
    }
}
