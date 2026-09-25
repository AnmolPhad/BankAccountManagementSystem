using System.ComponentModel.DataAnnotations;

namespace BankAccountManagementSystem.API.DTOs.Transactions
{
    /// <summary>
    /// Request body for POST /api/v1/transactions/transfer.
    ///
    /// Represents a cheque transaction: money moves from one Checking account to another account.
    ///
    /// Business rules enforced by TransactionService:
    ///   1. SourceAccountId != DestinationAccountId.
    ///   2. Source account must be a Checking account (Savings cannot initiate transfers).
    ///   3. Source account must be active.
    ///   4. Destination account must exist and be active.
    ///   5. Source must have sufficient balance.
    ///   6. Authenticated user must own the source account (unless Admin).
    ///   7. The entire operation (debit source + credit destination + two Transaction rows) is atomic.
    /// </summary>
    public class TransferRequest
    {
        /// <summary>
        /// The Checking account to debit. Authenticated user must own this account.
        /// </summary>
        [Required(ErrorMessage = "SourceAccountId is required.")]
        public Guid SourceAccountId { get; set; }

        /// <summary>
        /// The destination account to credit. May belong to any user.
        /// </summary>
        [Required(ErrorMessage = "DestinationAccountId is required.")]
        public Guid DestinationAccountId { get; set; }

        /// <summary>
        /// Amount to transfer. Must be positive. Source must have sufficient balance.
        /// </summary>
        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Optional note (e.g. "Rent payment", "Invoice #1023").
        /// </summary>
        [MaxLength(500, ErrorMessage = "Description must be at most 500 characters.")]
        public string? Description { get; set; }
    }
}
