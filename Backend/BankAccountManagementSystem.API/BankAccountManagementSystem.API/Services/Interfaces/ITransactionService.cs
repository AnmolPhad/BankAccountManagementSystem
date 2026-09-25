using BankAccountManagementSystem.API.DTOs.Transactions;

namespace BankAccountManagementSystem.API.Services.Interfaces
{
    /// <summary>
    /// Service contract for all financial transaction operations.
    ///
    /// This interface separates the controller (thin routing layer) from the business logic.
    /// All domain rules — account type restrictions, ownership checks, balance arithmetic,
    /// concurrency, and database transaction atomicity — live in the implementation.
    ///
    /// The controller calls these methods and maps the results to HTTP responses.
    /// </summary>
    public interface ITransactionService
    {
        /// <summary>
        /// Deposits cash into an account.
        ///
        /// Rules enforced:
        ///   - Account must exist and be active.
        ///   - Caller must own the account (or be Admin).
        ///   - Amount must be greater than 0 (validated by DTO, but also re-checked here).
        ///   - Works on both Savings and Checking accounts (cash operation).
        ///   - Balance is updated atomically with the transaction record.
        ///   - newBalance = currentBalance + amount (server-calculated).
        ///
        /// Returns a structured tuple:
        ///   Exists        → false if accountId not found → controller returns 404.
        ///   IsAuthorized  → false if user doesn't own account → controller returns 403.
        ///   Transaction   → the persisted transaction record as a response DTO.
        /// </summary>
        Task<(bool Exists, bool IsAuthorized, TransactionResponse? Transaction)>
            DepositAsync(Guid accountId, decimal amount, string? description, string callerId, bool isAdmin);

        /// <summary>
        /// Withdraws cash from an account.
        ///
        /// Rules enforced:
        ///   - Account must exist and be active.
        ///   - Caller must own the account (or be Admin).
        ///   - Amount must be greater than 0.
        ///   - Account must have sufficient balance (throws InsufficientBalanceException → 422).
        ///   - No negative balances permitted.
        ///   - Works on both Savings and Checking accounts (cash operation).
        ///   - newBalance = currentBalance - amount (server-calculated).
        ///
        /// Returns a structured tuple (same Exists/IsAuthorized/Transaction pattern).
        /// </summary>
        Task<(bool Exists, bool IsAuthorized, TransactionResponse? Transaction)>
            WithdrawAsync(Guid accountId, decimal amount, string? description, string callerId, bool isAdmin);

        /// <summary>
        /// Performs a cheque transfer between a Checking source account and any destination account.
        ///
        /// Rules enforced:
        ///   - Source account must exist, be active, and be a CHECKING account.
        ///   - Destination account must exist and be active.
        ///   - Source != Destination (no self-transfer).
        ///   - Caller must own the source account (or be Admin).
        ///   - Source must have sufficient balance.
        ///   - The entire operation is wrapped in an EF Core database transaction:
        ///       BEGIN TRANSACTION
        ///         Validate source
        ///         Validate destination
        ///         Debit source  (source.Balance -= amount)
        ///         Credit destination (destination.Balance += amount)
        ///         Insert two Transaction rows (one per account)
        ///       COMMIT
        ///     On any failure → ROLLBACK → balances unchanged.
        ///
        /// Returns:
        ///   Exists        → false if source account not found → 404.
        ///   IsAuthorized  → false if caller doesn't own source → 403.
        ///   Transactions  → tuple of (SourceTransaction, DestinationTransaction) as response DTOs.
        /// </summary>
        Task<(bool Exists, bool IsAuthorized, TransactionResponse? SourceTransaction, TransactionResponse? DestinationTransaction)>
            TransferAsync(Guid sourceAccountId, Guid destinationAccountId, decimal amount, string? description, string callerId, bool isAdmin);

        /// <summary>
        /// Retrieves paginated transaction history based on filter criteria.
        /// Enforces ownership: normal users only view transactions from their own accounts.
        /// Returns whether specified account exists, whether caller is authorized, and the paged results.
        /// </summary>
        Task<(bool AccountExists, bool IsAuthorized, BankAccountManagementSystem.API.DTOs.Common.PagedResult<TransactionResponse>? Result)>
            GetTransactionsAsync(TransactionHistoryQuery query, string callerId, bool isAdmin);

        /// <summary>
        /// Retrieves a single transaction by ID with ownership enforcement.
        /// </summary>
        Task<(bool Exists, bool IsAuthorized, TransactionResponse? Transaction)>
            GetTransactionByIdAsync(Guid transactionId, string callerId, bool isAdmin);
    }
}

