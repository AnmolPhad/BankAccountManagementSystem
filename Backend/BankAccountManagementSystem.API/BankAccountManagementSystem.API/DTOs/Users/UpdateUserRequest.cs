using System.ComponentModel.DataAnnotations;

namespace BankAccountManagementSystem.API.DTOs.Users
{
    /// <summary>
    /// DTO for updating user profile via PUT /api/v1/users/{id}.
    ///
    /// Rules:
    ///   - Admin can update any user's FirstName, LastName, Phone, EmployeeCode, IsActive
    ///   - Regular User can only update their own FirstName, LastName, Phone
    ///   - Password is NOT updated here — a separate change-password endpoint would handle that
    ///   - Role is NOT updated here — use PUT /api/v1/users/{id}/role for that
    ///   - Email is NOT updated here to avoid email uniqueness conflicts (would need verification flow)
    /// </summary>
    public class UpdateUserRequest
    {
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Employee code must be between 3 and 50 characters.")]
        [RegularExpression(@"^[A-Za-z0-9\-]+$",
            ErrorMessage = "Employee code can only contain letters, numbers, and hyphens.")]
        public string? EmployeeCode { get; set; }

        [StringLength(100, MinimumLength = 1, ErrorMessage = "First name cannot exceed 100 characters.")]
        public string? FirstName { get; set; }

        [StringLength(100, MinimumLength = 1, ErrorMessage = "Last name cannot exceed 100 characters.")]
        public string? LastName { get; set; }

        [Phone(ErrorMessage = "Invalid phone number format.")]
        [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters.")]
        public string? Phone { get; set; }

        /// <summary>
        /// Only Admins are allowed to change this.
        /// Ignored if a regular user sends it (enforced in the service layer).
        /// </summary>
        public bool? IsActive { get; set; }
    }
}
