using System.ComponentModel.DataAnnotations;

namespace BankAccountManagementSystem.API.DTOs.Users
{
    /// <summary>
    /// DTO for Admin-initiated user creation via POST /api/v1/users.
    ///
    /// This is similar to RegisterRequest but:
    ///   - Admin can assign a role (unlike self-registration where role is always "User")
    ///   - Used only by the [Authorize(Roles = "Admin")] endpoint
    /// </summary>
    public class CreateUserRequest
    {
        [Required(ErrorMessage = "Employee code is required.")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Employee code must be between 3 and 50 characters.")]
        [RegularExpression(@"^[A-Za-z0-9\-]+$",
            ErrorMessage = "Employee code can only contain letters, numbers, and hyphens.")]
        public string EmployeeCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "First name is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "First name cannot exceed 100 characters.")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Last name cannot exceed 100 characters.")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Invalid phone number format.")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// The role to assign. Only "Admin" or "User" are valid.
        /// Defaults to "User" if not specified.
        /// Only Admins can set this to "Admin".
        /// </summary>
        [Required(ErrorMessage = "Role is required.")]
        public string Role { get; set; } = "User";
    }
}
