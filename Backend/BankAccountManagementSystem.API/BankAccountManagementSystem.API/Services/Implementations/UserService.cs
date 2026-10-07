using BankAccountManagementSystem.API.DTOs.Users;
using BankAccountManagementSystem.API.Models;
using BankAccountManagementSystem.API.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BankAccountManagementSystem.API.Services.Implementations
{
    /// <summary>
    /// Implements IUserService: all User CRUD operations.
    ///
    /// Key design decisions:
    ///   - Uses UserManager<ApplicationUser> for all user operations (no raw EF queries on users)
    ///   - Soft delete: IsActive = false instead of physical removal
    ///   - Returns DTOs, never raw ApplicationUser objects
    ///   - isCallerAdmin flag controls which fields can be modified
    /// </summary>
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<UserService> _logger;

        public UserService(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<UserService> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET ALL — Admin only
        // ─────────────────────────────────────────────────────────────────────
        public async Task<IEnumerable<UserResponse>> GetAllUsersAsync()
        {
            // Get all users from the database asynchronously
            var users = await _userManager.Users.ToListAsync();
            var result = new List<UserResponse>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                result.Add(MapToResponse(user, roles.FirstOrDefault() ?? "User"));
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET BY ID
        // ─────────────────────────────────────────────────────────────────────
        public async Task<UserResponse?> GetUserByIdAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return null;

            var roles = await _userManager.GetRolesAsync(user);
            return MapToResponse(user, roles.FirstOrDefault() ?? "User");
        }

        // ─────────────────────────────────────────────────────────────────────
        // CREATE — Admin only
        // ─────────────────────────────────────────────────────────────────────
        public async Task<(bool Success, string Message, UserResponse? User)> CreateUserAsync(
            CreateUserRequest request)
        {
            // Validate the role
            if (!await _roleManager.RoleExistsAsync(request.Role))
                return (false, $"Role '{request.Role}' does not exist. Valid roles: Admin, User.", null);

            // Check duplicate email
            var existingByEmail = await _userManager.FindByEmailAsync(request.Email);
            if (existingByEmail != null)
                return (false, "An account with this email already exists.", null);

            // Check duplicate employee code
            var existingByCode = await _userManager.Users
                .FirstOrDefaultAsync(u => u.EmployeeCode == request.EmployeeCode);
            if (existingByCode != null)
                return (false, "An account with this employee code already exists.", null);
 
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                EmployeeCode = request.EmployeeCode,
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.Phone,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
                return (false, errors, null);
            }

            await _userManager.AddToRoleAsync(user, request.Role);

            _logger.LogInformation("Admin created user: {Email} with role {Role}", request.Email, request.Role);
            var response = MapToResponse(user, request.Role);
            return (true, "User created successfully.", response);
        }

        // ─────────────────────────────────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────────────────────────────────
        public async Task<(bool Success, string Message)> UpdateUserAsync(
            string id, UpdateUserRequest request, bool isCallerAdmin)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return (false, "User not found.");

            // Apply updates only if values are provided (partial update)
            if (request.FirstName != null) user.FirstName = request.FirstName;
            if (request.LastName != null)  user.LastName  = request.LastName;
            if (request.Phone != null)     user.PhoneNumber = request.Phone;

            // Only Admins can change EmployeeCode and IsActive
            if (isCallerAdmin)
            {
                if (request.EmployeeCode != null)
                {
                    // Check uniqueness if code is changing
                    if (user.EmployeeCode != request.EmployeeCode)
                    {
                        var conflict = await _userManager.Users
                            .FirstOrDefaultAsync(u => u.EmployeeCode == request.EmployeeCode && u.Id != id);
                        if (conflict != null)
                            return (false, "This employee code is already in use.");
                        user.EmployeeCode = request.EmployeeCode;
                    }
                }

                if (request.IsActive.HasValue)
                    user.IsActive = request.IsActive.Value;
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
                return (false, errors);
            }

            _logger.LogInformation("User updated: {UserId}", id);
            return (true, "User updated successfully.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // SOFT DELETE (Deactivate) — Admin only
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Why soft delete for a banking system?
        ///
        ///   1. AUDIT TRAIL: Financial regulations (e.g., RBI, SOX) require
        ///      that transaction history be preserved indefinitely. If we physically
        ///      delete a user, their transaction records become orphaned or must
        ///      be cascade-deleted, destroying audit history.
        ///
        ///   2. LEGAL COMPLIANCE: Banks must retain customer records for a
        ///      minimum period (typically 5–7 years) even after account closure.
        ///
        ///   3. DATA RECOVERY: A deactivated account can be reactivated if needed.
        ///      A physically deleted account is gone forever.
        ///
        ///   4. REFERENTIAL INTEGRITY: Future tables (BankAccounts, Transactions)
        ///      will have foreign keys to ApplicationUser. Physical deletion would
        ///      require complex cascade rules or violate FK constraints.
        ///
        ///   5. FRAUD INVESTIGATION: If a deactivated user was involved in fraud,
        ///      investigators need access to their full history.
        /// </summary>
        public async Task<(bool Success, string Message)> DeactivateUserAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return (false, "User not found.");

            if (!user.IsActive)
                return (false, "User is already deactivated.");

            user.IsActive = false;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
                return (false, errors);
            }

            _logger.LogInformation("User deactivated (soft delete): {UserId}", id);
            return (true, "User has been deactivated successfully.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // ASSIGN ROLE — Admin only
        // ─────────────────────────────────────────────────────────────────────
        public async Task<(bool Success, string Message)> AssignRoleAsync(string id, AssignRoleRequest request)
        {
            // 1. Find the user
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return (false, "User not found.");

            // 2. Validate the role exists
            if (!await _roleManager.RoleExistsAsync(request.Role))
                return (false, $"Role '{request.Role}' does not exist. Valid roles: Admin, User.");

            // 3. Remove ALL existing roles first (user can only have one role at a time)
            var currentRoles = await _userManager.GetRolesAsync(user);
            if (currentRoles.Any())
                await _userManager.RemoveFromRolesAsync(user, currentRoles);

            // 4. Assign the new role
            var result = await _userManager.AddToRoleAsync(user, request.Role);
            if (!result.Succeeded)
            {
                var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
                return (false, errors);
            }

            _logger.LogInformation("User {UserId} role changed to {Role}", id, request.Role);
            return (true, $"Role '{request.Role}' assigned successfully.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // PRIVATE HELPER: Map ApplicationUser → UserResponse DTO
        // ─────────────────────────────────────────────────────────────────────
        private static UserResponse MapToResponse(ApplicationUser user, string role)
        {
            return new UserResponse
            {
                Id = user.Id,
                EmployeeCode = user.EmployeeCode,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = user.FullName,
                Email = user.Email!,
                PhoneNumber = user.PhoneNumber,
                Role = role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            };
        }
    }
}
