namespace BankAccountManagementSystem.API.DTOs.Auth
{
    /// <summary>
    /// Data Transfer Object for the Login API response.
    ///
    /// This is what the client receives after a successful login.
    ///
    /// NEVER include in this response:
    ///   - Password
    ///   - PasswordHash
    ///   - SecurityStamp
    ///   - ConcurrencyStamp
    ///   - Any internal Identity fields
    /// </summary>
    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The JWT token the client must store (e.g., in localStorage or memory)
        /// and send with every subsequent request as:
        ///   Authorization: Bearer {Token}
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Basic user info returned alongside the token.
        /// The client can use this to display the user's name, role, etc.
        /// without making an extra API call.
        /// </summary>
        public UserInfo? User { get; set; }
    }

    /// <summary>
    /// Nested DTO: a slim summary of the logged-in user.
    /// </summary>
    public class UserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
