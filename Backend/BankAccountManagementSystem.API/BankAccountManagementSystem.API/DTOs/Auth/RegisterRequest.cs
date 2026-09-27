using System.ComponentModel.DataAnnotations;

namespace BankAccountManagementSystem.API.DTOs.Auth
{
    /// <summary>
    /// Data Transfer Object for the Register API request.
    ///
    /// Why use a DTO instead of passing ApplicationUser directly?
    ///   - Security: we never expose PasswordHash, SecurityStamp, or internal Identity fields
    ///   - Control: we only accept exactly the fields we want (no role selection allowed)
    ///   - Validation: [Required], [EmailAddress] etc. are applied here
    ///   - Separation: the request shape is independent of the database model
    /// </summary>
    public class RegisterRequest
    {
        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "First name cannot exceed 100 characters.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Last name cannot exceed 100 characters.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Invalid phone number format.")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        public string? Phone { get; set; }

        [StringLength(250, ErrorMessage = "Address cannot exceed 250 characters.")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 8,
            ErrorMessage = "Password must be at least 8 characters.")]
        public string Password { get; set; } = string.Empty;
    }
}
