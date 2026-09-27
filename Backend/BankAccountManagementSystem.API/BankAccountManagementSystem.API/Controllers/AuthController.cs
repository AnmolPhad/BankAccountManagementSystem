using BankAccountManagementSystem.API.DTOs.Auth;
using BankAccountManagementSystem.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
        /// Automatically generates Customer ID and default Bank Account Number.
        /// </summary>
        [HttpPost("register")]
        [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(RegisterResponse), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var response = await _authService.RegisterAsync(request);

            if (!response.Success)
            {
                var isDuplicate = response.Message.Contains("already exists");
                return isDuplicate
                    ? Conflict(response)
                    : BadRequest(response);
            }

            return StatusCode(StatusCodes.Status201Created, response);
        }

        /// <summary>
        /// Logs in an existing user and returns a JWT token.
        /// Rate-limited to 10 requests per minute per IP address (HTTP 429 when exceeded).
        /// </summary>
        [HttpPost("login")]
        [EnableRateLimiting("login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
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