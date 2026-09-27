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
            // 1. Load the account with owner user
            var account = await _context.Accounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            if (account is null)
                return (Exists: false, IsAuthorized: false, Transaction: null);

            // 2. Ownership check
            if (!isAdmin && account.UserId != callerId)
                return (Exists: true, IsAuthorized: false, Transaction: null);

            // 3. Account must be active
            if (!account.IsActive)
                throw new AccountInactiveException(account.AccountNumber);

            // 4. Amount validation
            if (amount <= 0)
                throw new InvalidTransactionException("Deposit amount must be greater than zero.");

            // 5. Apply balance change (server-calculated)
            account.Balance  += amount;
            account.UpdatedAt = DateTime.UtcNow;

            // 6. Build the transaction record
            var reference = GenerateReferenceNumber();
            var transaction = new Transaction
            {
                TransactionId    = Guid.NewGuid(),
                AccountId        = account.AccountId,
                Account          = account,
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
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.AccountId == accountId);

            if (account is null)
                return (Exists: false, IsAuthorized: false, Transaction: null);

            if (!isAdmin && account.UserId != callerId)
                return (Exists: true, IsAuthorized: false, Transaction: null);

            if (!account.IsActive)
                throw new AccountInactiveException(account.AccountNumber);

            if (amount <= 0)
                throw new InvalidTransactionException("Withdrawal amount must be greater than zero.");

            if (account.Balance < amount)
                throw new InsufficientBalanceException(account.Balance, amount);

            account.Balance  -= amount;
            account.UpdatedAt = DateTime.UtcNow;

            var reference = GenerateReferenceNumber();
            var transaction = new Transaction
            {
                TransactionId    = Guid.NewGuid(),
                AccountId        = account.AccountId,
                Account          = account,
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
            // 1. Load source account with owner user
            var source = await _context.Accounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.AccountId == sourceAccountId);

            if (source is null)
                return (Exists: false, IsAuthorized: false, SourceTransaction: null, DestinationTransaction: null);

            // 2. Ownership check on source account
            if (!isAdmin && source.UserId != callerId)
                return (Exists: true, IsAuthorized: false, SourceTransaction: null, DestinationTransaction: null);

            // 3. Source must be active
            if (!source.IsActive)
                throw new AccountInactiveException(source.AccountNumber);

            // 4. Source must be a Checking account
            if (source.AccountType != AccountType.Checking)
                throw new InvalidAccountTypeException(
                    $"Account '{source.AccountNumber}' is a Savings account. " +
                    "Only Checking accounts can initiate cheque transfers.");

            // 5. Source and destination must differ
            if (sourceAccountId == destinationAccountId)
                throw new InvalidTransactionException("Source and destination accounts must be different.");

            // 6. Load destination account with owner user
            var destination = await _context.Accounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.AccountId == destinationAccountId);

            if (destination is null)
                throw new KeyNotFoundException($"Destination account not found.");

            // 7. Destination must be active
            if (!destination.IsActive)
                throw new AccountInactiveException(destination.AccountNumber);

            // 8. Amount validation
            if (amount <= 0)
                throw new InvalidTransactionException("Transfer amount must be greater than zero.");

            // 9. Balance check
            if (source.Balance < amount)
                throw new InsufficientBalanceException(source.Balance, amount);

            // ─── ATOMIC OPERATION using EF Core database transaction ──────────
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

                // Source Transaction row
                var sourceTxn = new Transaction
                {
                    TransactionId    = Guid.NewGuid(),
                    AccountId        = source.AccountId,
                    Account          = source,
                    TransactionType  = TransactionType.Transfer,
                    TransactionMode  = TransactionMode.Cheque,
                    Amount           = amount,
                    TransactionDate  = now,
                    Description      = description,
                    ReferenceNumber  = reference + "-SRC",
                    RelatedAccountId = destination.AccountId,
                    RelatedAccount   = destination,
                    Status           = TransactionStatus.Completed
                };

                // Destination Transaction row
                var destinationTxn = new Transaction
                {
                    TransactionId    = Guid.NewGuid(),
                    AccountId        = destination.AccountId,
                    Account          = destination,
                    TransactionType  = TransactionType.Transfer,
                    TransactionMode  = TransactionMode.Cheque,
                    Amount           = amount,
                    TransactionDate  = now,
                    Description      = description,
                    ReferenceNumber  = reference + "-DST",
                    RelatedAccountId = source.AccountId,
                    RelatedAccount   = source,
                    Status           = TransactionStatus.Completed
                };

                _context.Transactions.Add(sourceTxn);
                _context.Transactions.Add(destinationTxn);

                await _context.SaveChangesAsync();
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
            }

            // 3. Build IQueryable with AsNoTracking and eager load User navigation properties
            IQueryable<Transaction> queryable = _context.Transactions
                .AsNoTracking()
                .Include(t => t.Account)
                    .ThenInclude(a => a!.User)
                .Include(t => t.RelatedAccount)
                    .ThenInclude(ra => ra!.User);

            // 4. Apply Ownership & Account filter at database query level
            if (query.AccountId.HasValue)
            {
                queryable = queryable.Where(t => t.AccountId == query.AccountId.Value);
            }
            else if (!isAdmin)
            {
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

            // 6. Sorting: Newest transaction first
            queryable = queryable
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.TransactionId);

            // 7. Execute query: LastN or Paged
            if (query.LastN.HasValue)
            {
                var txns = await queryable.Take(query.LastN.Value).ToListAsync();
                var items = txns.Select(t => MapToResponse(t, t.Account)).ToList();
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
                var totalCount = await queryable.CountAsync();
                var txns = await queryable
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToListAsync();
                var items = txns.Select(t => MapToResponse(t, t.Account)).ToList();

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
                    .ThenInclude(a => a!.User)
                .Include(t => t.RelatedAccount)
                    .ThenInclude(ra => ra!.User)
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
        /// Computes SenderName, ReceiverName, and IsSelfTransfer for cheque transfer operations.
        /// </summary>
        private static TransactionResponse MapToResponse(Transaction txn, Account? account = null)
        {
            var currentAccount = account ?? txn.Account;
            var relatedAccount = txn.RelatedAccount;

            string? senderName = null;
            string? receiverName = null;
            string? senderAccountNumber = null;
            string? receiverAccountNumber = null;
            bool isSelfTransfer = false;

            if (txn.TransactionType == TransactionType.Transfer)
            {
                var primaryUser = currentAccount?.User;
                var relatedUser = relatedAccount?.User;

                var primaryName = primaryUser != null
                    ? $"{primaryUser.FirstName} {primaryUser.LastName}".Trim()
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(primaryName))
                    primaryName = primaryUser?.Email ?? currentAccount?.AccountNumber ?? string.Empty;

                var relatedName = relatedUser != null
                    ? $"{relatedUser.FirstName} {relatedUser.LastName}".Trim()
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(relatedName))
                    relatedName = relatedUser?.Email ?? relatedAccount?.AccountNumber ?? string.Empty;

                bool isDestinationRow = txn.ReferenceNumber.EndsWith("-DST");

                if (isDestinationRow)
                {
                    senderName = relatedName;
                    receiverName = primaryName;
                    senderAccountNumber = relatedAccount?.AccountNumber;
                    receiverAccountNumber = currentAccount?.AccountNumber;
                }
                else
                {
                    senderName = primaryName;
                    receiverName = relatedName;
                    senderAccountNumber = currentAccount?.AccountNumber;
                    receiverAccountNumber = relatedAccount?.AccountNumber;
                }

                if (currentAccount != null && relatedAccount != null)
                {
                    isSelfTransfer = !string.IsNullOrEmpty(currentAccount.UserId) &&
                                     currentAccount.UserId == relatedAccount.UserId;
                }
            }

            return new TransactionResponse
            {
                TransactionId            = txn.TransactionId,
                AccountId                = txn.AccountId,
                AccountNumber            = currentAccount?.AccountNumber ?? string.Empty,
                TransactionType          = txn.TransactionType.ToString(),
                TransactionMode          = txn.TransactionMode.ToString(),
                Amount                   = txn.Amount,
                BalanceAfterTransaction  = currentAccount?.Balance ?? 0.00m,
                TransactionDate          = txn.TransactionDate,
                Description              = txn.Description,
                ReferenceNumber          = txn.ReferenceNumber,
                RelatedAccountId         = txn.RelatedAccountId,
                Status                   = txn.Status.ToString(),

                SenderName               = senderName,
                ReceiverName             = receiverName,
                SenderAccountNumber      = senderAccountNumber,
                ReceiverAccountNumber    = receiverAccountNumber,
                IsSelfTransfer           = isSelfTransfer
            };
        }
    }
}
