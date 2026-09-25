using BankAccountManagementSystem.API.DTOs.Auth;
using BankAccountManagementSystem.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace BankAccountManagementSystem.API.Controllers
{
    /// <summary>
    /// Handles authentication operations such as registration and login.
    /// </summary>
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Registers a new user.
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var (success, message) = await _authService.RegisterAsync(request);

            if (!success)
            {
                var isDuplicate = message.Contains("already exists");
                return isDuplicate
                    ? Conflict(new { success = false, message })
                    : BadRequest(new { success = false, message });
            }

            return StatusCode(StatusCodes.Status201Created, new
            {
                success = true,
                message
            });
        }

        /// <summary>
        /// Logs in an existing user and returns a JWT token.
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var response = await _authService.LoginAsync(request);

            if (!response.Success)
            {
                return Unauthorized(response);
            }

            return Ok(response);
        }
    }
}