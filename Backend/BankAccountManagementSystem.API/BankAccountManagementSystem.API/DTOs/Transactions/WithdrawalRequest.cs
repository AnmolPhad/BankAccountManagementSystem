using System.ComponentModel.DataAnnotations;

namespace BankAccountManagementSystem.API.DTOs.Transactions
{
    /// <summary>
    /// Request body for POST /api/v1/transactions/withdraw.
    ///
    /// Only caller-controlled fields are accepted.
    /// The server enforces: sufficient balance check, AccountId ownership, account active status.
    /// The server calculates: newBalance = currentBalance - Amount (never accepted from client).
    /// </summary>
    public class WithdrawalRequest
    {
        /// <summary>
        /// The account to debit. Must belong to the authenticated user (unless Admin).
        /// </summary>
        [Required(ErrorMessage = "AccountId is required.")]
        public Guid AccountId { get; set; }

        /// <summary>
        /// Amount to withdraw. Must be positive. Service rejects if balance is insufficient.
        /// </summary>
        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        /// <summary>
        /// Optional human-readable note (e.g. "ATM withdrawal", "Rent payment").
        /// </summary>
        [MaxLength(500, ErrorMessage = "Description must be at most 500 characters.")]
        public string? Description { get; set; }
    }
}
