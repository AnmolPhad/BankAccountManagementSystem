namespace BankAccountManagementSystem.API.DTOs.Auth
{
    /// <summary>
    /// Data Transfer Object returned after user registration.
    /// Includes automatically generated CustomerId.
    /// Bank accounts are created separately via the Add Account functionality.
    /// </summary>
    public class RegisterResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? CustomerId { get; set; }
    }
}
