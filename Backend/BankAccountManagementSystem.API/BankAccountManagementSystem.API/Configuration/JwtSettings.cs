namespace BankAccountManagementSystem.API.Configuration
{
    /// <summary>
    /// Strongly-typed configuration class for JWT settings.
    /// Bound to the "JwtSettings" section in appsettings.json.
    /// </summary>
    public class JwtSettings
    {
        /// Secret key used to sign the JWT.
        /// Never expose this in API responses or logs.
        public string SecretKey { get; set; } = string.Empty;

        /// Identifies who issued the JWT.
        public string Issuer { get; set; } = string.Empty;

        /// Identifies the intended recipient of the JWT.
        public string Audience { get; set; } = string.Empty;

        /// Token expiration time in minutes.
        public int ExpirationMinutes { get; set; } = 60;
    }
}