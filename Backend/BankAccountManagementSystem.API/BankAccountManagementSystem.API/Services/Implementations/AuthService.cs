using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BankAccountManagementSystem.API.Configuration;
using BankAccountManagementSystem.API.DTOs.Auth;
using BankAccountManagementSystem.API.Models;
using BankAccountManagementSystem.API.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BankAccountManagementSystem.API.Services.Implementations
{
    /// <summary>
    /// Implements IAuthService: handles user registration and login.
    ///
    /// This service sits between the controller and Identity/EF Core.
    /// The controller stays thin — it only validates input and returns HTTP responses.
    /// All business logic lives here.
    ///
    /// Request flow for Login:
    ///   AuthController.Login()
    ///       → AuthService.LoginAsync()
    ///           → UserManager.FindByEmailAsync()       [Identity]
    ///           → UserManager.CheckPasswordAsync()     [Identity]
    ///           → UserManager.GetRolesAsync()          [Identity]
    ///           → GenerateJwtToken()                   [our code]
    ///       ← returns LoginResponse (with token)
    ///   AuthController returns 200 OK with LoginResponse
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAccountService _accountService;
        private readonly JwtSettings _jwtSettings;
        private readonly ILogger<AuthService> _logger;

        // Constructor injection — ASP.NET Core DI fills these automatically.
        public AuthService(
            UserManager<ApplicationUser> userManager,
            IAccountService accountService,
            IOptions<JwtSettings> jwtSettings,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _accountService = accountService;
            _jwtSettings = jwtSettings.Value;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // REGISTER
        // ─────────────────────────────────────────────────────────────────────
        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
        {
            // 1. Check duplicate email
            var existingByEmail = await _userManager.FindByEmailAsync(request.Email);
            if (existingByEmail != null)
            {
                return new RegisterResponse
                {
                    Success = false,
                    Message = "An account with this email already exists."
                };
            }

            // 2. Auto-generate unique Customer ID (e.g. CUST-00001)
            var customerId = await GenerateUniqueCustomerIdAsync();

            // 3. Build the user object (no password yet — Identity sets it)
            var user = new ApplicationUser
            {
                UserName = request.Email,       // Identity uses UserName for lookup
                Email = request.Email,
                EmployeeCode = customerId,      // Stores backend-generated Customer ID
                FirstName = request.FirstName,
                LastName = request.LastName,
                PhoneNumber = request.Phone,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // 4. CreateAsync: Identity validates the password, hashes it, saves the user.
            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
                _logger.LogWarning("Registration failed for {Email}: {Errors}", request.Email, errors);
                return new RegisterResponse
                {
                    Success = false,
                    Message = errors
                };
            }

            // 5. Always assign "User" role
            await _userManager.AddToRoleAsync(user, "User");

            _logger.LogInformation("New user registered: {Email} (Customer ID: {CustomerId})",
                request.Email, customerId);

            return new RegisterResponse
            {
                Success = true,
                Message = "Registration successful.",
                CustomerId = customerId
            };
        }

        private async Task<string> GenerateUniqueCustomerIdAsync()
        {
            var count = await _userManager.Users.CountAsync();
            string candidate;
            bool exists;
            int number = count + 1;
            do
            {
                candidate = $"CUST-{number:D5}";
                exists = await _userManager.Users.AnyAsync(u => u.EmployeeCode == candidate);
                if (exists)
                {
                    number++;
                }
            } while (exists);

            return candidate;
        }

        // ─────────────────────────────────────────────────────────────────────
        // LOGIN
        // ─────────────────────────────────────────────────────────────────────
        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var failResponse = new LoginResponse
            {
                Success = false,
                Message = "Invalid email or password."
                // NOTE: We use the same generic message for wrong email AND wrong password.
                // This is intentional — it prevents "user enumeration attacks" where
                // an attacker probes which emails are registered.
            };

            // 1. Find user by email
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return failResponse;

            // 2. Check if the account is active (soft-delete check)
            if (!user.IsActive)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = "This account has been deactivated. Please contact an administrator."
                };
            }

            // 3. Verify the password using Identity's PasswordHasher
            // CheckPasswordAsync handles lockout tracking automatically.
            var passwordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!passwordValid)
                return failResponse;

            // 4. Get the user's roles
            var roles = await _userManager.GetRolesAsync(user);
            var primaryRole = roles.FirstOrDefault() ?? "User";

            // 5. Generate the JWT token
            var token = GenerateJwtToken(user, primaryRole);

            _logger.LogInformation("User logged in: {Email}", request.Email);

            return new LoginResponse
            {
                Success = true,
                Message = "Login successful.",
                Token = token,
                User = new UserInfo
                {
                    Id = user.Id,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Email = user.Email!,
                    Role = primaryRole
                }
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // PRIVATE: JWT Token Generation
        // ─────────────────────────────────────────────────────────────────────
        private string GenerateJwtToken(ApplicationUser user, string role)
        {
            // Convert the secret key to bytes for HMAC-SHA256 signing
            var keyBytes = Encoding.UTF8.GetBytes(_jwtSettings.SecretKey);
            var signingKey = new SymmetricSecurityKey(keyBytes);
            var signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            // Claims are pieces of information embedded inside the JWT payload.
            // The client can decode these (they are Base64 encoded, not encrypted).
            // The server uses them to know who is making the request.
            var claims = new List<Claim>
            {
                // Sub (Subject) is the standard JWT claim for the user's identity
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),

                // Jti (JWT ID) is a unique identifier for this specific token
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

                // Standard email claim
                new Claim(JwtRegisteredClaimNames.Email, user.Email!),

                // ASP.NET Core's built-in name identifier claim
                // This is what ClaimTypes.NameIdentifier maps to in [Authorize] checks
                new Claim(ClaimTypes.NameIdentifier, user.Id),

                // User's role — used by [Authorize(Roles = "Admin")]
                new Claim(ClaimTypes.Role, role),

                // Custom claims specific to our application
                new Claim("employeeCode", user.EmployeeCode),
                new Claim("firstName", user.FirstName),
                new Claim("lastName", user.LastName)
            };

            var tokenDescriptor = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
                signingCredentials: signingCredentials
            );

            return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        }
    }
}
