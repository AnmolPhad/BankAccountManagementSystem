using BankAccountManagementSystem.API.Models;

namespace BankAccountManagementSystem.API.DTOs.System
{
    /// <summary>
    /// Represents the serialized state of a financial transaction in a system backup payload.
    /// Preserves double-entry reference numbers, amounts, and audit timestamps.
    /// </summary>
    public class TransactionBackupDto
    {
        public Guid TransactionId { get; set; }
        public Guid AccountId { get; set; }
        public int TransactionType { get; set; }
        public int TransactionMode { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public string? Description { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
        public Guid? RelatedAccountId { get; set; }
        public int Status { get; set; }
    }
}
