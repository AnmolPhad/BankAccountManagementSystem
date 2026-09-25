using System.ComponentModel.DataAnnotations;

namespace BankAccountManagementSystem.API.DTOs.Users
{
    /// <summary>
    /// DTO for role assignment via PUT /api/v1/users/{id}/role.
    /// Only admins can call this endpoint.
    /// </summary>
    public class AssignRoleRequest
    {
        [Required(ErrorMessage = "Role is required.")]
        public string Role { get; set; } = string.Empty;
    }
}
