using BankAccountManagementSystem.API.DTOs.Users;

namespace BankAccountManagementSystem.API.Services.Interfaces
{
    /// <summary>
    /// Defines the contract for all User CRUD operations.
    /// </summary>
    public interface IUserService
    {
        /// <summary>
        /// Returns all users. Admin only.
        /// </summary>
        Task<IEnumerable<UserResponse>> GetAllUsersAsync();

        /// <summary>
        /// Returns a specific user by ID.
        /// Normal users can only access their own ID.
        /// </summary>
        Task<UserResponse?> GetUserByIdAsync(string id);

        /// <summary>
        /// Admin creates a new user with an explicit role.
        /// </summary>
        Task<(bool Success, string Message, UserResponse? User)> CreateUserAsync(CreateUserRequest request);

        /// <summary>
        /// Updates a user's profile fields.
        /// isCallerAdmin controls whether IsActive/EmployeeCode can be changed.
        /// </summary>
        Task<(bool Success, string Message)> UpdateUserAsync(string id, UpdateUserRequest request, bool isCallerAdmin);

        /// <summary>
        /// Soft-deletes a user by setting IsActive = false. Admin only.
        /// Does NOT remove the row from the database.
        /// </summary>
        Task<(bool Success, string Message)> DeactivateUserAsync(string id);

        /// <summary>
        /// Assigns a new role to a user, removing all existing roles first. Admin only.
        /// </summary>
        Task<(bool Success, string Message)> AssignRoleAsync(string id, AssignRoleRequest request);
    }
}
