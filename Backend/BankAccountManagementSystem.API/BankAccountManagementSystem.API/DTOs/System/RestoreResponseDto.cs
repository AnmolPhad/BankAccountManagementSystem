namespace BankAccountManagementSystem.API.DTOs.System
{
    /// <summary>
    /// Outbound response DTO detailing the outcome of a system restore operation.
    /// </summary>
    public class RestoreResponseDto
    {
        public bool Success { get; set; }
        public int AccountsRestored { get; set; }
        public int TransactionsRestored { get; set; }
        public DateTime RestoredAt { get; set; } = DateTime.UtcNow;
        public string Message { get; set; } = string.Empty;
    }
}
