using System.ComponentModel.DataAnnotations;
using BankAccountManagementSystem.API.Models;

namespace BankAccountManagementSystem.API.DTOs.Transactions
{
    /// <summary>
    /// Encapsulates all query parameters for retrieving transaction history.
    /// Supports filtering by account, date range, type, mode, recent N transactions, and pagination.
    /// </summary>
    public class TransactionHistoryQuery
    {
        /// <summary>
        /// Optional: Filter by specific bank account ID.
        /// </summary>
        public Guid? AccountId { get; set; }

        /// <summary>
        /// Optional: Filter transactions from this UTC date/time onward.
        /// </summary>
        public DateTime? FromDate { get; set; }

        /// <summary>
        /// Optional: Filter transactions up to this UTC date/time.
        /// </summary>
        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Optional: Retrieve the most recent N transactions (1 to 100).
        /// When specified, returns up to N newest transactions.
        /// </summary>
        [Range(1, 100, ErrorMessage = "LastN must be between 1 and 100.")]
        public int? LastN { get; set; }

        /// <summary>
        /// Optional: Filter by transaction type (Deposit = 1, Withdrawal = 2, Transfer = 3).
        /// </summary>
        public TransactionType? TransactionType { get; set; }

        /// <summary>
        /// Optional: Filter by transaction mode (Cash = 1, Cheque = 2).
        /// </summary>
        public TransactionMode? TransactionMode { get; set; }

        /// <summary>
        /// Page number for pagination (1-based, default: 1).
        /// </summary>
        [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
        public int Page { get; set; } = 1;

        /// <summary>
        /// Number of records per page (default: 20, max: 100).
        /// </summary>
        [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")]
        public int PageSize { get; set; } = 20;
    }
}
