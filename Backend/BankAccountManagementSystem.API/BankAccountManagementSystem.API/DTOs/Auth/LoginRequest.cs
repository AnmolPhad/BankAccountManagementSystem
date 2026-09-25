using System.ComponentModel.DataAnnotations;

namespace BankAccountManagementSystem.API.DTOs.Auth
{
    /// <summary>
    /// Data Transfer Object for the Login API request.
    /// Only needs email and password — nothing else.
    /// </summary>
    public class LoginRequest
    {
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required.")]
        public string Password { get; set; } = string.Empty;
    }
}
