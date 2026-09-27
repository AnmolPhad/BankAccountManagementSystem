using BankAccountManagementSystem.API.DTOs.Auth;

namespace BankAccountManagementSystem.API.Services.Interfaces
{
    /// <summary>
    /// Defines the contract for authentication operations.
    ///
    /// Why use an interface?
    ///   - Controllers depend on this abstraction, not on AuthService directly.
    ///   - This makes it easy to swap implementations (e.g., for testing).
    ///   - Follows the Dependency Inversion Principle (D in SOLID).
    ///
    /// The controller calls: await _authService.RegisterAsync(request)
    /// It doesn't know or care HOW it's implemented — just what it returns.
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// Registers a new user with the "User" role.
        /// Returns a result containing success status, message, and backend-generated credentials (CustomerId and AccountNumber).
        /// </summary>
        Task<RegisterResponse> RegisterAsync(RegisterRequest request);

        /// <summary>
        /// Authenticates a user by email/password and returns a JWT token.
        /// </summary>
        Task<LoginResponse> LoginAsync(LoginRequest request);
    }
}
