using System.ComponentModel.DataAnnotations;

namespace BankAccountManagementSystem.API.DTOs.System
{
    /// <summary>
    /// Root backup payload containing the complete serialized banking system state.
    /// Excludes security credentials, password hashes, and secrets.
    /// </summary>
    public class SystemBackupDto
    {
        /// <summary>
        /// Backup schema version. Defaults to "1.0".
        /// </summary>
        [Required(ErrorMessage = "Version is required.")]
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// UTC timestamp of when this backup was exported.
        /// </summary>
        public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Total number of accounts contained in this backup.
        /// </summary>
        public int AccountsCount { get; set; }

        /// <summary>
        /// Total number of transactions contained in this backup.
        /// </summary>
        public int TransactionsCount { get; set; }

        /// <summary>
        /// The collection of accounts to restore.
        /// </summary>
        public List<AccountBackupDto> Accounts { get; set; } = new();

        /// <summary>
        /// The collection of financial transactions to restore.
        /// </summary>
        public List<TransactionBackupDto> Transactions { get; set; } = new();
    }
}
