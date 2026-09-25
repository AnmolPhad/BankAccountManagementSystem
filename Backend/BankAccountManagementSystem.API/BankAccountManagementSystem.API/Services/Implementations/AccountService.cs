using System.Security.Cryptography;
using BankAccountManagementSystem.API.Data;
using BankAccountManagementSystem.API.DTOs.Accounts;
using BankAccountManagementSystem.API.Models;
using BankAccountManagementSystem.API.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BankAccountManagementSystem.API.Services.Implementations
{
    /// <summary>
    /// Implements account management business logic.
    ///
    /// Design & Security Principles:
    /// 1. Balance Initialization: Server strictly enforces initial balance of 0.00m.
    /// 2. Account Number Generation: Cryptographically secure 10-digit numbers with type prefix and collision check.
    /// 3. Ownership Enforcement: User ID is extracted from authenticated claims and verified against the database.
    /// 4. Thin Controllers: All data queries and domain logic reside in this service.
    /// </summary>
    public class AccountService : IAccountService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AccountService> _logger;

        public AccountService(
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            ILogger<AccountService> logger)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _logger = logger;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CREATE ACCOUNT
        // ─────────────────────────────────────────────────────────────────────
        public async Task<(bool Success, string Message, AccountResponse? Account)> CreateAccountAsync(
            string userId,
            CreateAccountRequest request)
        {
            // 1. Validate that the owning user exists and is active
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return (false, "User account not found.", null);
            }

            if (!user.IsActive)
            {
                return (false, "User account is deactivated. Cannot open a bank account.", null);
            }

            // 2. Validate AccountType enum
            if (!Enum.IsDefined(typeof(AccountType), request.AccountType))
            {
                return (false, "Invalid account type. Valid types are 1 (Savings) or 2 (Checking).", null);
            }

            // 3. Generate a cryptographically secure, unique account number
            var accountNumber = await GenerateUniqueAccountNumberAsync(request.AccountType);

            // 4. Construct the domain entity with strictly controlled defaults
            var account = new Account
            {
                AccountId = Guid.NewGuid(),
                AccountNumber = accountNumber,
                UserId = userId,
                AccountType = request.AccountType,
                Balance = 0.00m,       // Initial balance is always zero
                IsActive = true,       // Account starts active
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };

            // 5. Save to database
            _dbContext.Accounts.Add(account);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("New {AccountType} account {AccountNumber} created for user {UserId}",
                account.AccountType, account.AccountNumber, userId);

            return (true, "Account created successfully.", MapToResponse(account));
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET ACCOUNT BY ID
        // ─────────────────────────────────────────────────────────────────────
        public async Task<(bool Exists, bool IsAuthorized, AccountResponse? Account)> GetAccountByIdAsync(
            Guid id,
            string callerId,
            bool isAdmin)
        {
            var account = await _dbContext.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountId == id);

            if (account == null)
            {
                return (false, false, null);
            }

            // If not admin and caller doesn't own this account, forbid access
            if (!isAdmin && account.UserId != callerId)
            {
                return (true, false, null);
            }

            return (true, true, MapToResponse(account));
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET ACCOUNTS (Filtered by caller / All for admin)
        // ─────────────────────────────────────────────────────────────────────
        public async Task<IEnumerable<AccountResponse>> GetAccountsAsync(string callerId, bool isAdmin)
        {
            var query = _dbContext.Accounts.AsNoTracking();

            if (!isAdmin)
            {
                // Normal user sees only accounts they own
                query = query.Where(a => a.UserId == callerId);
            }

            var accounts = await query
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return accounts.Select(MapToResponse);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET ACCOUNT BALANCE
        // ─────────────────────────────────────────────────────────────────────
        public async Task<(bool Exists, bool IsAuthorized, AccountBalanceResponse? Balance)> GetAccountBalanceAsync(
            Guid id,
            string callerId,
            bool isAdmin)
        {
            var account = await _dbContext.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.AccountId == id);

            if (account == null)
            {
                return (false, false, null);
            }

            // Enforce ownership: normal users can only query their own balance; admins can query any
            if (!isAdmin && account.UserId != callerId)
            {
                return (true, false, null);
            }

            var balanceResponse = new AccountBalanceResponse
            {
                AccountId = account.AccountId,
                AccountNumber = account.AccountNumber,
                AccountType = account.AccountType.ToString(),
                Balance = account.Balance,
                IsActive = account.IsActive
            };

            return (true, true, balanceResponse);
        }

        // ─────────────────────────────────────────────────────────────────────
        // DEACTIVATE ACCOUNT (Soft delete: IsActive = false, UpdatedAt = UtcNow)
        // ─────────────────────────────────────────────────────────────────────
        public async Task<(bool Exists, bool IsAuthorized, bool AlreadyInactive, string Message)> DeactivateAccountAsync(
            Guid id,
            string callerId,
            bool isAdmin)
        {
            var account = await _dbContext.Accounts.FirstOrDefaultAsync(a => a.AccountId == id);

            if (account == null)
            {
                return (false, false, false, "Account not found.");
            }

            // Enforce ownership: normal users can deactivate only their own accounts; admins can deactivate any
            if (!isAdmin && account.UserId != callerId)
            {
                return (true, false, false, "You do not have permission to deactivate this account.");
            }

            // Check if already deactivated
            if (!account.IsActive)
            {
                return (true, true, true, "Account is already deactivated.");
            }

            // Soft-deactivate: set IsActive to false and record update timestamp
            account.IsActive = false;
            account.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Account {AccountNumber} (ID: {AccountId}) deactivated by caller {CallerId}",
                account.AccountNumber, account.AccountId, callerId);

            return (true, true, false, "Account has been deactivated successfully.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPER: Account Number Generation Strategy
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Generates a standard 10-digit bank account number:
        /// - First 2 digits identify the product type: 10 = Savings, 20 = Checking
        /// - Remaining 8 digits are generated using cryptographically secure random numbers
        /// - A database check ensures no duplicate account number is ever produced
        /// </summary>
        private async Task<string> GenerateUniqueAccountNumberAsync(AccountType accountType)
        {
            var prefix = accountType == AccountType.Savings ? "10" : "20";
            string accountNumber;
            bool exists;

            do
            {
                // Generate 8 cryptographically secure random digits [10,000,000 to 99,999,999]
                var randomPart = RandomNumberGenerator.GetInt32(10000000, 100000000);
                accountNumber = $"{prefix}{randomPart}";

                // Ensure uniqueness in database
                exists = await _dbContext.Accounts.AnyAsync(a => a.AccountNumber == accountNumber);
            }
            while (exists);

            return accountNumber;
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPER: Entity to DTO Mapper
        // ─────────────────────────────────────────────────────────────────────
        private static AccountResponse MapToResponse(Account account)
        {
            return new AccountResponse
            {
                AccountId = account.AccountId,
                AccountNumber = account.AccountNumber,
                UserId = account.UserId,
                AccountType = account.AccountType.ToString(),
                Balance = account.Balance,
                IsActive = account.IsActive,
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt
            };
        }
    }
}
