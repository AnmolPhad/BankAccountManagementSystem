namespace BankAccountManagementSystem.API.Configuration
{
    /// <summary>
    /// Strongly-typed configuration class for JWT settings.
    /// Bound to the "JwtSettings" section in appsettings.json.
    /// </summary>
    public class JwtSettings
    {
        /// <summary>
        /// Secret key used to sign the JWT.
        /// Never expose this in API responses or logs.
        /// </summary>
        public string SecretKey { get; set; } = string.Empty;

        /// <summary>
        /// Identifies who issued the JWT.
        /// </summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>
        /// Identifies the intended recipient of the JWT.
        /// </summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// Token expiration time in minutes.
        /// </summary>
        public int ExpirationMinutes { get; set; } = 60;
    }
}