using System.Security.Claims;
using BankAccountManagementSystem.API.DTOs.Users;
using BankAccountManagementSystem.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankAccountManagementSystem.API.Controllers
{
    /// <summary>
    /// User CRUD endpoints. All require authentication.
    ///
    /// Authorization matrix:
    ///   GET    /users          → Admin only
    ///   GET    /users/{id}     → Admin (any user) OR User (own profile only)
    ///   POST   /users          → Admin only
    ///   PUT    /users/{id}     → Admin (any user) OR User (own profile only)
    ///   DELETE /users/{id}     → Admin only (soft delete)
    ///   PUT    /users/{id}/role → Admin only
    ///
    /// [Authorize] on the class means ALL endpoints require a valid JWT.
    /// Individual endpoints add [Authorize(Roles = "Admin")] for admin-only routes.
    /// </summary>
    [ApiController]
    [Route("api/v1/users")]
    [Authorize] // ← All endpoints require authentication by default
    public class UsersController : ControllerBase
    {
        private readonly IUserService _userService;

        public UsersController(IUserService userService)
        {
            _userService = userService;
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/v1/users — Admin only
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>Returns all users. Admin only.</summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(IEnumerable<UserResponse>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/v1/users/{id} — Admin (any) | User (own only)
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>Returns a user by ID. Users can only view their own profile.</summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserById(string id)
        {
            // Get the calling user's ID from the JWT claims
            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            // Non-admin users can only view their own profile
            if (!isAdmin && callerId != id)
                return Forbid(); // 403 Forbidden

            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
                return NotFound(new { success = false, message = "User not found." });

            return Ok(user);
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/v1/users — Admin only
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>Admin creates a new user with an explicit role.</summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            var (success, message, user) = await _userService.CreateUserAsync(request);

            if (!success)
            {
                var isDuplicate = message.Contains("already exists");
                return isDuplicate
                    ? Conflict(new { success = false, message })
                    : BadRequest(new { success = false, message });
            }

            // 201 Created with a Location header pointing to the new resource
            return CreatedAtAction(nameof(GetUserById), new { id = user!.Id }, new
            {
                success = true,
                message,
                user
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // PUT /api/v1/users/{id} — Admin (any) | User (own only)
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>Updates a user's profile. Users can only update their own.</summary>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
        {
            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            // Non-admin users can only update their own profile
            if (!isAdmin && callerId != id)
                return Forbid(); // 403 Forbidden

            var (success, message) = await _userService.UpdateUserAsync(id, request, isAdmin);

            if (!success)
            {
                // Distinguish 404 vs 400
                return message.Contains("not found")
                    ? NotFound(new { success = false, message })
                    : BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }

        // ─────────────────────────────────────────────────────────────────────
        // DELETE /api/v1/users/{id} — Admin only (soft delete)
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Deactivates a user (soft delete: sets IsActive = false).
        /// The user record is NOT removed from the database.
        /// This preserves transaction history, audit trails, and referential integrity.
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeactivateUser(string id)
        {
            var (success, message) = await _userService.DeactivateUserAsync(id);

            if (!success)
            {
                return message.Contains("not found")
                    ? NotFound(new { success = false, message })
                    : BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }

        // ─────────────────────────────────────────────────────────────────────
        // PUT /api/v1/users/{id}/role — Admin only
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Assigns a new role to a user.
        /// Removes all existing roles and assigns the specified one.
        /// Admin only.
        /// </summary>
        [HttpPut("{id}/role")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AssignRole(string id, [FromBody] AssignRoleRequest request)
        {
            var (success, message) = await _userService.AssignRoleAsync(id, request);

            if (!success)
            {
                return message.Contains("not found")
                    ? NotFound(new { success = false, message })
                    : BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message });
        }
    }
}
