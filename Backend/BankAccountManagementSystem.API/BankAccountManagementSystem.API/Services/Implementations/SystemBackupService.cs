using BankAccountManagementSystem.API.Data;
using BankAccountManagementSystem.API.DTOs.System;
using BankAccountManagementSystem.API.Models;
using BankAccountManagementSystem.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BankAccountManagementSystem.API.Services.Implementations
{
    /// <summary>
    /// Implements system backup (save) and restore operations.
    ///
    /// Architectural Principles:
    /// 1. Security: Only accessible to Administrator role.
    /// 2. User Isolation & Safety: Existing Identity ApplicationUser records are never deleted or modified
    ///    during restore. This guarantees password hashes, security stamps, and user roles remain intact.
    /// 3. Validation-First: The entire backup payload is thoroughly verified before any database modifications.
    /// 4. Atomicity: Database restoration is executed inside an EF Core transaction (BeginTransactionAsync).
    ///    Any validation failure or database error rolls back all operations completely.
    /// 5. RowVersion Management: Concurrency tokens are excluded from export and generated naturally by
    ///    SQL Server upon restoration.
    /// </summary>
    public class SystemBackupService : ISystemBackupService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SystemBackupService> _logger;

        public SystemBackupService(ApplicationDbContext context, ILogger<SystemBackupService> logger)
        {
            _context = context;
            _logger  = logger;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // EXPORT / SAVE
        // ─────────────────────────────────────────────────────────────────────────────
        public async Task<SystemBackupDto> ExportBackupAsync()
        {
            _logger.LogInformation("Initiating system backup export...");

            // 1. Read accounts without tracking
            var accounts = await _context.Accounts
                .AsNoTracking()
                .OrderBy(a => a.CreatedAt)
                .Select(a => new AccountBackupDto
                {
                    AccountId     = a.AccountId,
                    AccountNumber = a.AccountNumber,
                    UserId        = a.UserId,
                    AccountType   = (int)a.AccountType,
                    Balance       = a.Balance,
                    IsActive      = a.IsActive,
                    CreatedAt     = a.CreatedAt,
                    UpdatedAt     = a.UpdatedAt
                })
                .ToListAsync();

            // 2. Read transactions without tracking
            var transactions = await _context.Transactions
                .AsNoTracking()
                .OrderBy(t => t.TransactionDate)
                .ThenBy(t => t.TransactionId)
                .Select(t => new TransactionBackupDto
                {
                    TransactionId    = t.TransactionId,
                    AccountId        = t.AccountId,
                    TransactionType  = (int)t.TransactionType,
                    TransactionMode  = (int)t.TransactionMode,
                    Amount           = t.Amount,
                    TransactionDate  = t.TransactionDate,
                    Description      = t.Description,
                    ReferenceNumber  = t.ReferenceNumber,
                    RelatedAccountId = t.RelatedAccountId,
                    Status           = (int)t.Status
                })
                .ToListAsync();

            _logger.LogInformation(
                "System backup export complete: {AccountCount} accounts, {TransactionCount} transactions.",
                accounts.Count, transactions.Count);

            return new SystemBackupDto
            {
                Version           = "1.0",
                ExportedAt        = DateTime.UtcNow,
                AccountsCount     = accounts.Count,
                TransactionsCount = transactions.Count,
                Accounts          = accounts,
                Transactions      = transactions
            };
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // RESTORE
        // ─────────────────────────────────────────────────────────────────────────────
        public async Task<RestoreResponseDto> RestoreBackupAsync(SystemBackupDto backup)
        {
            _logger.LogInformation("Initiating system backup restore verification...");

            // ─────────────────────────────────────────────────────────────────────
            // PHASE 1: COMPREHENSIVE PAYLOAD VALIDATION
            // ─────────────────────────────────────────────────────────────────────
            if (backup is null)
                throw new ArgumentException("Backup payload cannot be null.");

            if (string.IsNullOrWhiteSpace(backup.Version) || backup.Version != "1.0")
                throw new ArgumentException($"Unsupported backup version '{backup.Version}'. Expected schema version: '1.0'.");

            if (backup.Accounts is null)
                throw new ArgumentException("Accounts collection cannot be null.");

            if (backup.Transactions is null)
                throw new ArgumentException("Transactions collection cannot be null.");

            // 1. Validate Accounts
            var accountIdSet = new HashSet<Guid>();
            var accountNumberSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Fetch existing user IDs from Identity store to ensure referential integrity
            var existingUserIds = (await _context.Users
                .AsNoTracking()
                .Select(u => u.Id)
                .ToListAsync())
                .ToHashSet();

            foreach (var acc in backup.Accounts)
            {
                if (acc.AccountId == Guid.Empty)
                    throw new ArgumentException("Account contains an empty or invalid AccountId.");

                if (!accountIdSet.Add(acc.AccountId))
                    throw new ArgumentException($"Duplicate AccountId '{acc.AccountId}' detected in backup payload.");

                if (string.IsNullOrWhiteSpace(acc.AccountNumber))
                    throw new ArgumentException($"Account '{acc.AccountId}' has an empty AccountNumber.");

                if (!accountNumberSet.Add(acc.AccountNumber))
                    throw new ArgumentException($"Duplicate AccountNumber '{acc.AccountNumber}' detected in backup payload.");

                if (!Enum.IsDefined(typeof(AccountType), acc.AccountType))
                    throw new ArgumentException($"Account '{acc.AccountNumber}' has an invalid AccountType value: {acc.AccountType}.");

                if (acc.Balance < 0)
                    throw new ArgumentException($"Account '{acc.AccountNumber}' has an invalid negative balance: {acc.Balance}.");

                if (string.IsNullOrWhiteSpace(acc.UserId))
                    throw new ArgumentException($"Account '{acc.AccountNumber}' does not specify a UserId.");

                if (!existingUserIds.Contains(acc.UserId))
                    throw new ArgumentException(
                        $"Account '{acc.AccountNumber}' references a non-existent UserId '{acc.UserId}'. " +
                        "Existing user accounts are preserved and all accounts must reference valid existing users.");
            }

            // 2. Validate Transactions
            var transactionIdSet = new HashSet<Guid>();
            var referenceNumberSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var txn in backup.Transactions)
            {
                if (txn.TransactionId == Guid.Empty)
                    throw new ArgumentException("Transaction contains an empty or invalid TransactionId.");

                if (!transactionIdSet.Add(txn.TransactionId))
                    throw new ArgumentException($"Duplicate TransactionId '{txn.TransactionId}' detected in backup payload.");

                if (string.IsNullOrWhiteSpace(txn.ReferenceNumber))
                    throw new ArgumentException($"Transaction '{txn.TransactionId}' has an empty ReferenceNumber.");

                if (!referenceNumberSet.Add(txn.ReferenceNumber))
                    throw new ArgumentException($"Duplicate ReferenceNumber '{txn.ReferenceNumber}' detected in backup payload.");

                if (!accountIdSet.Contains(txn.AccountId))
                    throw new ArgumentException(
                        $"Transaction '{txn.ReferenceNumber}' references AccountId '{txn.AccountId}' which does not exist in the backup.");

                if (txn.RelatedAccountId.HasValue)
                {
                    if (!accountIdSet.Contains(txn.RelatedAccountId.Value))
                        throw new ArgumentException(
                            $"Transaction '{txn.ReferenceNumber}' references RelatedAccountId '{txn.RelatedAccountId.Value}' which does not exist in the backup.");

                    if (txn.RelatedAccountId.Value == txn.AccountId)
                        throw new ArgumentException(
                            $"Transaction '{txn.ReferenceNumber}' has identical AccountId and RelatedAccountId.");
                }

                if (!Enum.IsDefined(typeof(TransactionType), txn.TransactionType))
                    throw new ArgumentException($"Transaction '{txn.ReferenceNumber}' has an invalid TransactionType: {txn.TransactionType}.");

                if (!Enum.IsDefined(typeof(TransactionMode), txn.TransactionMode))
                    throw new ArgumentException($"Transaction '{txn.ReferenceNumber}' has an invalid TransactionMode: {txn.TransactionMode}.");

                if (!Enum.IsDefined(typeof(TransactionStatus), txn.Status))
                    throw new ArgumentException($"Transaction '{txn.ReferenceNumber}' has an invalid Status: {txn.Status}.");

                if (txn.Amount <= 0)
                    throw new ArgumentException($"Transaction '{txn.ReferenceNumber}' has an invalid amount: {txn.Amount}. Amount must be greater than zero.");

                if (txn.TransactionDate == default)
                    throw new ArgumentException($"Transaction '{txn.ReferenceNumber}' has an invalid or unset TransactionDate.");
            }

            _logger.LogInformation("Backup payload passed all validation checks. Beginning atomic database replacement...");

            // ─────────────────────────────────────────────────────────────────────
            // PHASE 2: ATOMIC DATABASE RESTORATION
            // ─────────────────────────────────────────────────────────────────────
            await using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Step 1: Remove existing operational records respecting foreign key order
                // First delete all Transactions (they reference Accounts)
                var existingTransactions = await _context.Transactions.ToListAsync();
                if (existingTransactions.Count > 0)
                {
                    _context.Transactions.RemoveRange(existingTransactions);
                    await _context.SaveChangesAsync();
                }

                // Second delete all Accounts (they reference Users, but Users are left completely intact)
                var existingAccounts = await _context.Accounts.ToListAsync();
                if (existingAccounts.Count > 0)
                {
                    _context.Accounts.RemoveRange(existingAccounts);
                    await _context.SaveChangesAsync();
                }

                // Step 2: Insert restored Accounts
                var accountsToInsert = backup.Accounts.Select(a => new Account
                {
                    AccountId     = a.AccountId,
                    AccountNumber = a.AccountNumber,
                    UserId        = a.UserId,
                    AccountType   = (AccountType)a.AccountType,
                    Balance       = a.Balance,
                    IsActive      = a.IsActive,
                    CreatedAt     = a.CreatedAt != default ? a.CreatedAt : DateTime.UtcNow,
                    UpdatedAt     = a.UpdatedAt
                    // RowVersion is intentionally null here: SQL Server will assign fresh concurrency tokens
                }).ToList();

                _context.Accounts.AddRange(accountsToInsert);
                await _context.SaveChangesAsync();

                // Step 3: Insert restored Transactions
                var transactionsToInsert = backup.Transactions.Select(t => new Transaction
                {
                    TransactionId    = t.TransactionId,
                    AccountId        = t.AccountId,
                    TransactionType  = (TransactionType)t.TransactionType,
                    TransactionMode  = (TransactionMode)t.TransactionMode,
                    Amount           = t.Amount,
                    TransactionDate  = t.TransactionDate != default ? t.TransactionDate : DateTime.UtcNow,
                    Description      = t.Description,
                    ReferenceNumber  = t.ReferenceNumber,
                    RelatedAccountId = t.RelatedAccountId,
                    Status           = (TransactionStatus)t.Status
                }).ToList();

                _context.Transactions.AddRange(transactionsToInsert);
                await _context.SaveChangesAsync();

                // Step 4: Commit transaction
                await dbTransaction.CommitAsync();

                _logger.LogInformation(
                    "System restore successfully committed. Restored {Accounts} accounts and {Transactions} transactions.",
                    accountsToInsert.Count, transactionsToInsert.Count);

                return new RestoreResponseDto
                {
                    Success              = true,
                    AccountsRestored     = accountsToInsert.Count,
                    TransactionsRestored = transactionsToInsert.Count,
                    RestoredAt           = DateTime.UtcNow,
                    Message              = $"System state restored successfully with {accountsToInsert.Count} accounts and {transactionsToInsert.Count} transactions."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "System restore failed. Rolling back database transaction to protect system integrity.");
                await dbTransaction.RollbackAsync();
                throw;
            }
        }
    }
}
