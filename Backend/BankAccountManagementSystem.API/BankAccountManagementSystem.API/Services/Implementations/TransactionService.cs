using BankAccountManagementSystem.API.Data;
using BankAccountManagementSystem.API.DTOs.Common;
using BankAccountManagementSystem.API.DTOs.Transactions;
using BankAccountManagementSystem.API.Exceptions;
using BankAccountManagementSystem.API.Models;
using BankAccountManagementSystem.API.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace BankAccountManagementSystem.API.Services.Implementations
{
    /// <summary>
    /// Implements all financial transaction business logic.
    ///
    /// Architecture: TransactionsController → ITransactionService → TransactionService → ApplicationDbContext → SQL Server
    ///
    /// Key design rules followed throughout this class:
    ///   1. No balance accepted from the client. newBalance is always server-calculated.
    ///   2. No float/double for money. All monetary values use decimal.
    ///   3. Domain exceptions (InsufficientBalanceException etc.) bubble up to ExceptionMiddleware.
    ///   4. RowVersion concurrency token is on Account — EF Core throws DbUpdateConcurrencyException
    ///      if two concurrent requests attempt to update the same account balance.
    ///   5. Transfers use an explicit EF Core database transaction for atomicity.
    ///   6. Soft-deleted (IsActive = false) accounts cannot perform transactions.
    ///   7. User identity is always taken from the JWT (callerId parameter) — never from request body.
    /// </summary>
    public class TransactionService : ITransactionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TransactionService> _logger;

        public TransactionService(ApplicationDbContext context, ILogger<TransactionService> logger)
        {
            _context = context;
            _logger  = logger;
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // DEPOSIT
        // ─────────────────────────────────────────────────────────────────────────────
        public async Task<(bool Exists, bool IsAuthorized, TransactionResponse? Transaction)>
            DepositAsync(Guid accountId, decimal amount, string? description, string callerId, bool isAdmin)
        {
            // 1. Load the account
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            if (account is null)
                return (Exists: false, IsAuthorized: false, Transaction: null);

            // 2. Ownership check
            if (!isAdmin && account.UserId != callerId)
                return (Exists: true, IsAuthorized: false, Transaction: null);

            // 3. Account must be active
            // We throw a domain exception here (not return a tuple field) because the account EXISTS
            // and caller IS authorized — the failure is a business rule, not a routing decision.
            // ExceptionMiddleware catches InvalidOperationException → 422 Unprocessable Entity.
            if (!account.IsActive)
                throw new AccountInactiveException(account.AccountNumber);

            // 4. Amount validation — belt-and-suspenders (DTO already validates this)
            if (amount <= 0)
                throw new InvalidTransactionException("Deposit amount must be greater than zero.");

            // 5. Apply balance change (server-calculated — client never supplies balance)
            account.Balance  += amount;
            account.UpdatedAt = DateTime.UtcNow;

            // 6. Build the transaction record
            var reference = GenerateReferenceNumber();
            var transaction = new Transaction
            {
                TransactionId    = Guid.NewGuid(),
                AccountId        = account.AccountId,
                TransactionType  = TransactionType.Deposit,
                TransactionMode  = TransactionMode.Cash,
                Amount           = amount,
                TransactionDate  = DateTime.UtcNow,
                Description      = description,
                ReferenceNumber  = reference,
                RelatedAccountId = null,
                Status           = TransactionStatus.Completed
            };

            _context.Transactions.Add(transaction);

            // 7. SaveChangesAsync — EF Core will:
            //    a. UPDATE Accounts SET Balance=..., UpdatedAt=... WHERE AccountId=@id AND RowVersion=@original
            //    b. INSERT INTO Transactions (...)
            //    If another request already updated RowVersion, EF throws DbUpdateConcurrencyException → 409.
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Deposit: Account={AccountId} Ref={Ref} Amount={Amount} BalanceAfter={Balance}",
                accountId, reference, amount, account.Balance);

            return (Exists: true, IsAuthorized: true, Transaction: MapToResponse(transaction, account));
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // WITHDRAWAL
        // ─────────────────────────────────────────────────────────────────────────────
        public async Task<(bool Exists, bool IsAuthorized, TransactionResponse? Transaction)>
            WithdrawAsync(Guid accountId, decimal amount, string? description, string callerId, bool isAdmin)
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            if (account is null)
                return (Exists: false, IsAuthorized: false, Transaction: null);

            if (!isAdmin && account.UserId != callerId)
                return (Exists: true, IsAuthorized: false, Transaction: null);

            if (!account.IsActive)
                throw new AccountInactiveException(account.AccountNumber);

            if (amount <= 0)
                throw new InvalidTransactionException("Withdrawal amount must be greater than zero.");

            // Sufficient balance check — prevents negative balances
            if (account.Balance < amount)
                throw new InsufficientBalanceException(account.Balance, amount);

            account.Balance  -= amount;
            account.UpdatedAt = DateTime.UtcNow;

            var reference = GenerateReferenceNumber();
            var transaction = new Transaction
            {
                TransactionId    = Guid.NewGuid(),
                AccountId        = account.AccountId,
                TransactionType  = TransactionType.Withdrawal,
                TransactionMode  = TransactionMode.Cash,
                Amount           = amount,
                TransactionDate  = DateTime.UtcNow,
                Description      = description,
                ReferenceNumber  = reference,
                RelatedAccountId = null,
                Status           = TransactionStatus.Completed
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Withdrawal: Account={AccountId} Ref={Ref} Amount={Amount} BalanceAfter={Balance}",
                accountId, reference, amount, account.Balance);

            return (Exists: true, IsAuthorized: true, Transaction: MapToResponse(transaction, account));
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // TRANSFER (Cheque Transaction)
        // ─────────────────────────────────────────────────────────────────────────────
        public async Task<(bool Exists, bool IsAuthorized, TransactionResponse? SourceTransaction, TransactionResponse? DestinationTransaction)>
            TransferAsync(Guid sourceAccountId, Guid destinationAccountId, decimal amount, string? description, string callerId, bool isAdmin)
        {
            // 1. Load source account
            var source = await _context.Accounts
                .FirstOrDefaultAsync(a => a.AccountId == sourceAccountId);

            if (source is null)
                return (Exists: false, IsAuthorized: false, SourceTransaction: null, DestinationTransaction: null);

            // 2. Ownership: only source account ownership is checked (transfers FROM your account)
            //    The destination can belong to any user — that is the purpose of a transfer.
            if (!isAdmin && source.UserId != callerId)
                return (Exists: true, IsAuthorized: false, SourceTransaction: null, DestinationTransaction: null);

            // 3. Source must be active
            if (!source.IsActive)
                throw new AccountInactiveException(source.AccountNumber);

            // 4. Source must be a Checking account
            //    Savings accounts are restricted to cash transactions involving ONE account only.
            //    The original requirement: "In a cheque transaction, amount is transferred from one account to another."
            //    Cheque = Transfer = Checking accounts only as source.
            if (source.AccountType != AccountType.Checking)
                throw new InvalidAccountTypeException(
                    $"Account '{source.AccountNumber}' is a Savings account. " +
                    "Only Checking accounts can initiate cheque transfers.");

            // 5. Source and destination must differ
            if (sourceAccountId == destinationAccountId)
                throw new InvalidTransactionException("Source and destination accounts must be different.");

            // 6. Load destination account
            var destination = await _context.Accounts
                .FirstOrDefaultAsync(a => a.AccountId == destinationAccountId);

            if (destination is null)
                throw new KeyNotFoundException($"Destination account not found.");

            // 7. Destination must be active
            if (!destination.IsActive)
                throw new AccountInactiveException(destination.AccountNumber);

            // 8. Amount validation
            if (amount <= 0)
                throw new InvalidTransactionException("Transfer amount must be greater than zero.");

            // 9. Source must have sufficient balance
            if (source.Balance < amount)
                throw new InsufficientBalanceException(source.Balance, amount);

            // ─── ATOMIC OPERATION using EF Core database transaction ──────────
            // BEGIN TRANSACTION
            //   Debit source
            //   Credit destination
            //   Insert source Transaction row
            //   Insert destination Transaction row
            // COMMIT
            // Any exception → ROLLBACK → both balances remain unchanged.
            //
            // Why not just SaveChangesAsync() twice?
            // If we debit source and save, then credit destination fails, the money disappears.
            // A DB transaction guarantees BOTH changes commit or NEITHER does.
            //
            // Concurrency: Both account rows have RowVersion concurrency tokens.
            // If another request modifies either account between our read and save,
            // DbUpdateConcurrencyException is thrown → ExceptionMiddleware → 409 Conflict.
            await using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var now       = DateTime.UtcNow;
                var reference = GenerateReferenceNumber();

                // Debit source
                source.Balance  -= amount;
                source.UpdatedAt = now;

                // Credit destination
                destination.Balance  += amount;
                destination.UpdatedAt = now;

                // Source Transaction row — records the debit on source's ledger
                var sourceTxn = new Transaction
                {
                    TransactionId    = Guid.NewGuid(),
                    AccountId        = source.AccountId,
                    TransactionType  = TransactionType.Transfer,
                    TransactionMode  = TransactionMode.Cheque,
                    Amount           = amount,
                    TransactionDate  = now,
                    Description      = description,
                    ReferenceNumber  = reference + "-SRC",
                    RelatedAccountId = destination.AccountId,
                    Status           = TransactionStatus.Completed
                };

                // Destination Transaction row — records the credit on destination's ledger
                var destinationTxn = new Transaction
                {
                    TransactionId    = Guid.NewGuid(),
                    AccountId        = destination.AccountId,
                    TransactionType  = TransactionType.Transfer,
                    TransactionMode  = TransactionMode.Cheque,
                    Amount           = amount,
                    TransactionDate  = now,
                    Description      = description,
                    ReferenceNumber  = reference + "-DST",
                    RelatedAccountId = source.AccountId,
                    Status           = TransactionStatus.Completed
                };

                _context.Transactions.Add(sourceTxn);
                _context.Transactions.Add(destinationTxn);

                // Single SaveChangesAsync saves all 4 changes (2 UPDATE + 2 INSERT) in one DB round trip.
                await _context.SaveChangesAsync();

                // Commit the transaction — all changes are now permanent in the database.
                await dbTransaction.CommitAsync();

                _logger.LogInformation(
                    "Transfer: Source={SourceId} Dest={DestId} Ref={Ref} Amount={Amount} " +
                    "SourceBalanceAfter={SBal} DestBalanceAfter={DBal}",
                    sourceAccountId, destinationAccountId, reference, amount,
                    source.Balance, destination.Balance);

                return (
                    Exists:               true,
                    IsAuthorized:         true,
                    SourceTransaction:    MapToResponse(sourceTxn, source),
                    DestinationTransaction: MapToResponse(destinationTxn, destination)
                );
            }
            catch
            {
                // Rollback on ANY exception (concurrency, business rule, DB failure).
                // Because we re-throw, ExceptionMiddleware handles the response.
                await dbTransaction.RollbackAsync();
                throw;
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // TRANSACTION HISTORY (READ-ONLY)
        // ─────────────────────────────────────────────────────────────────────────────
        public async Task<(bool AccountExists, bool IsAuthorized, PagedResult<TransactionResponse>? Result)>
            GetTransactionsAsync(TransactionHistoryQuery query, string callerId, bool isAdmin)
        {
            // 1. Parameter Validations
            if (query.Page <= 0)
                throw new ArgumentException("Page must be greater than 0.");

            if (query.PageSize <= 0)
                throw new ArgumentException("PageSize must be greater than 0.");

            if (query.PageSize > 100)
                throw new ArgumentException("PageSize cannot exceed 100.");

            if (query.LastN.HasValue && query.LastN.Value <= 0)
                throw new ArgumentException("LastN must be greater than 0.");

            if (query.LastN.HasValue && query.LastN.Value > 100)
                throw new ArgumentException("LastN cannot exceed 100.");

            if (query.FromDate.HasValue && query.ToDate.HasValue && query.FromDate.Value > query.ToDate.Value)
                throw new ArgumentException("FromDate cannot be later than ToDate.");

            if (query.TransactionType.HasValue && !Enum.IsDefined(typeof(TransactionType), query.TransactionType.Value))
                throw new ArgumentException("Invalid transaction type specified.");

            if (query.TransactionMode.HasValue && !Enum.IsDefined(typeof(TransactionMode), query.TransactionMode.Value))
                throw new ArgumentException("Invalid transaction mode specified.");

            // 2. Account verification & ownership (if AccountId is supplied)
            if (query.AccountId.HasValue)
            {
                var account = await _context.Accounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.AccountId == query.AccountId.Value);

                if (account is null)
                    return (AccountExists: false, IsAuthorized: false, Result: null);

                if (!isAdmin && account.UserId != callerId)
                    return (AccountExists: true, IsAuthorized: false, Result: null);

                // Inactive accounts still have historical records viewable by authorized users/admin
            }

            // 3. Build IQueryable with AsNoTracking for read-only performance
            var queryable = _context.Transactions
                .AsNoTracking();

            // 4. Apply Ownership & Account filter at database query level
            if (query.AccountId.HasValue)
            {
                queryable = queryable.Where(t => t.AccountId == query.AccountId.Value);
            }
            else if (!isAdmin)
            {
                // Normal user without specific accountId: view all transactions across their own accounts
                queryable = queryable.Where(t => t.Account!.UserId == callerId);
            }

            // 5. Apply filters
            if (query.FromDate.HasValue)
            {
                var fromUtc = DateTime.SpecifyKind(query.FromDate.Value, DateTimeKind.Utc);
                queryable = queryable.Where(t => t.TransactionDate >= fromUtc);
            }

            if (query.ToDate.HasValue)
            {
                var toUtc = DateTime.SpecifyKind(query.ToDate.Value, DateTimeKind.Utc);
                queryable = queryable.Where(t => t.TransactionDate <= toUtc);
            }

            if (query.TransactionType.HasValue)
            {
                queryable = queryable.Where(t => t.TransactionType == query.TransactionType.Value);
            }

            if (query.TransactionMode.HasValue)
            {
                queryable = queryable.Where(t => t.TransactionMode == query.TransactionMode.Value);
            }

            // 6. Sorting: Newest transaction first (TransactionDate DESC, then TransactionId DESC)
            queryable = queryable
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.TransactionId);

            // 7. Project to TransactionResponse DTO to avoid tracking and N+1 queries
            var projected = queryable.Select(t => new TransactionResponse
            {
                TransactionId           = t.TransactionId,
                AccountId               = t.AccountId,
                AccountNumber           = t.Account != null ? t.Account.AccountNumber : string.Empty,
                TransactionType         = t.TransactionType.ToString(),
                TransactionMode         = t.TransactionMode.ToString(),
                Amount                  = t.Amount,
                BalanceAfterTransaction = t.Account != null ? t.Account.Balance : 0.00m,
                TransactionDate         = t.TransactionDate,
                Description             = t.Description,
                ReferenceNumber         = t.ReferenceNumber,
                RelatedAccountId        = t.RelatedAccountId,
                Status                  = t.Status.ToString()
            });

            // 8. Execute query: LastN or Paged
            if (query.LastN.HasValue)
            {
                var items = await projected.Take(query.LastN.Value).ToListAsync();
                var result = new PagedResult<TransactionResponse>
                {
                    Items      = items,
                    Page       = 1,
                    PageSize   = query.LastN.Value,
                    TotalCount = items.Count
                };

                return (AccountExists: true, IsAuthorized: true, Result: result);
            }
            else
            {
                var totalCount = await projected.CountAsync();
                var items = await projected
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToListAsync();

                var result = new PagedResult<TransactionResponse>
                {
                    Items      = items,
                    Page       = query.Page,
                    PageSize   = query.PageSize,
                    TotalCount = totalCount
                };

                return (AccountExists: true, IsAuthorized: true, Result: result);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // TRANSACTION DETAIL (READ-ONLY)
        // ─────────────────────────────────────────────────────────────────────────────
        public async Task<(bool Exists, bool IsAuthorized, TransactionResponse? Transaction)>
            GetTransactionByIdAsync(Guid transactionId, string callerId, bool isAdmin)
        {
            var txn = await _context.Transactions
                .AsNoTracking()
                .Include(t => t.Account)
                .FirstOrDefaultAsync(t => t.TransactionId == transactionId);

            if (txn is null)
                return (Exists: false, IsAuthorized: false, Transaction: null);

            if (!isAdmin && txn.Account?.UserId != callerId)
                return (Exists: true, IsAuthorized: false, Transaction: null);

            return (Exists: true, IsAuthorized: true, Transaction: MapToResponse(txn, txn.Account!));
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Generates a cryptographically random, unique transaction reference number.
        ///
        /// Format: TXN-YYYYMMDD-XXXXXXXX
        ///   - TXN    : fixed prefix for easy identification and database query filtering.
        ///   - YYYYMMDD: UTC date of the transaction (makes references readable by humans).
        ///   - XXXXXXXX: 8 random uppercase alphanumeric characters (36^8 ≈ 2.8 trillion combinations).
        ///
        /// Uniqueness guarantee:
        ///   - The random suffix space (36^8 combinations) is large enough that collisions are
        ///     astronomically unlikely even at high transaction volume.
        ///   - The database enforces a unique index on ReferenceNumber (IX_Transactions_ReferenceNumber)
        ///     as the final safety net. If a collision ever occurred, the INSERT would fail with a
        ///     unique constraint violation (caught by ExceptionMiddleware → 500 Internal Server Error).
        ///   - For Transfer, we append "-SRC" / "-DST" to differentiate the two sides while keeping
        ///     them grouped by their shared base reference number.
        ///
        /// Not supplied by the client — always generated here on the server.
        /// </summary>
        private static string GenerateReferenceNumber()
        {
            const string chars   = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var dateSegment      = DateTime.UtcNow.ToString("yyyyMMdd");
            var randomBytes      = new byte[8];
            RandomNumberGenerator.Fill(randomBytes);

            var randomSegment = new StringBuilder(8);
            foreach (var b in randomBytes)
                randomSegment.Append(chars[b % chars.Length]);

            return $"TXN-{dateSegment}-{randomSegment}";
        }

        /// <summary>
        /// Maps a Transaction entity + its owning Account to the safe outbound TransactionResponse DTO.
        /// BalanceAfterTransaction is read from account.Balance which is already the updated value
        /// (balance was mutated in memory before SaveChangesAsync).
        /// </summary>
        private static TransactionResponse MapToResponse(Transaction txn, Account account)
        {
            return new TransactionResponse
            {
                TransactionId            = txn.TransactionId,
                AccountId                = txn.AccountId,
                AccountNumber            = account.AccountNumber,
                TransactionType          = txn.TransactionType.ToString(),
                TransactionMode          = txn.TransactionMode.ToString(),
                Amount                   = txn.Amount,
                BalanceAfterTransaction  = account.Balance,
                TransactionDate          = txn.TransactionDate,
                Description              = txn.Description,
                ReferenceNumber          = txn.ReferenceNumber,
                RelatedAccountId         = txn.RelatedAccountId,
                Status                   = txn.Status.ToString()
            };
        }
    }
}
