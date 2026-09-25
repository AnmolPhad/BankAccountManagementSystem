using System.ComponentModel.DataAnnotations;

namespace BankAccountManagementSystem.API.DTOs.Transactions
{
    /// <summary>
    /// Request body for POST /api/v1/transactions/deposit.
    ///
    /// Only the caller-controlled fields are accepted.
    /// The server controls: TransactionId, ReferenceNumber, TransactionDate, Status.
    /// The server controls: Balance (calculated as OldBalance + Amount — never client-supplied).
    /// </summary>
    public class DepositRequest
    {
        /// <summary>
        /// The account to credit. Must belong to the authenticated user (unless Admin).
        /// </summary>
        [Required(ErrorMessage = "AccountId is required.")]
        public Guid AccountId { get; set; }

        /// <summary>
        /// Amount to deposit. Must be a positive decimal value.
        /// The server will add this to the current balance — client never supplies the new balance.
        /// </summary>
        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Optional human-readable note (e.g. "Salary deposit", "ATM cash-in").
        /// </summary>
        [MaxLength(500, ErrorMessage = "Description must be at most 500 characters.")]
        public string? Description { get; set; }
    }
}
