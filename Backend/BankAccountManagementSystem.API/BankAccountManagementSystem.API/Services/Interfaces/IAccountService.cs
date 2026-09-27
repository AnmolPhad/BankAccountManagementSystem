using BankAccountManagementSystem.API.DTOs.Accounts;

namespace BankAccountManagementSystem.API.Services.Interfaces
{
    /// <summary>
    /// Service contract for bank account management operations.
    /// </summary>
    public interface IAccountService
    {
        /// <summary>
        /// Creates a new bank account associated with the authenticated user.
        /// </summary>
        Task<(bool Success, string Message, AccountResponse? Account)> CreateAccountAsync(string userId, CreateAccountRequest request);

        /// <summary>
        /// Retrieves an account by its unique identifier.
        /// Returns whether the account exists, whether the caller is authorized to view it, and the account details.
        /// </summary>
        Task<(bool Exists, bool IsAuthorized, AccountResponse? Account)> GetAccountByIdAsync(Guid id, string callerId, bool isAdmin);

        /// <summary>
        /// Retrieves accounts.
        /// If caller is an Admin, returns all accounts in the system.
        /// If caller is a regular User, returns only accounts owned by that user.
        /// </summary>
        Task<IEnumerable<AccountResponse>> GetAccountsAsync(string callerId, bool isAdmin);

        /// <summary>
        /// Queries the balance of a specific account.
        /// Enforces ownership authorization: regular users can only query their own account balance; admins can query any account.
        /// </summary>
        Task<(bool Exists, bool IsAuthorized, AccountBalanceResponse? Balance)> GetAccountBalanceAsync(Guid id, string callerId, bool isAdmin);

        /// <summary>
        /// Deactivates an account (soft delete: sets IsActive = false and stamps UpdatedAt).
        /// Enforces ownership: normal users can deactivate only their own accounts; admins can deactivate any account.
        /// Returns whether the account exists, whether caller is authorized, whether it was already inactive, and a status message.
        /// </summary>
        Task<(bool Exists, bool IsAuthorized, bool AlreadyInactive, string Message)> DeactivateAccountAsync(Guid id, string callerId, bool isAdmin);

        /// <summary>
        /// Looks up an active account by its customer-facing 10-digit account number.
        /// </summary>
        Task<AccountResponse?> GetAccountByNumberAsync(string accountNumber);
    }
}
