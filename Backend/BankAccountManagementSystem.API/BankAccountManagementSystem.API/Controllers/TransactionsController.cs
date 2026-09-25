using System.Security.Claims;
using BankAccountManagementSystem.API.DTOs.Common;
using BankAccountManagementSystem.API.DTOs.Transactions;
using BankAccountManagementSystem.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankAccountManagementSystem.API.Controllers
{
    /// <summary>
    /// Financial transaction endpoints.
    ///
    /// This controller is intentionally thin. It:
    ///   1. Extracts the caller identity from the JWT (ClaimTypes.NameIdentifier).
    ///   2. Calls the appropriate ITransactionService method.
    ///   3. Maps the structured tuple result to the correct HTTP response.
    ///
    /// All business logic — balance arithmetic, account type checks, ownership enforcement,
    /// concurrency, and database transactions — lives in TransactionService.
    /// </summary>
    [ApiController]
    [Route("api/v1/transactions")]
    [Authorize]
    public class TransactionsController : ControllerBase
    {
        private readonly ITransactionService _transactionService;

        public TransactionsController(ITransactionService transactionService)
        {
            _transactionService = transactionService;
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/v1/transactions/deposit
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Deposits cash into a bank account.
        ///
        /// Available for both Savings and Checking accounts.
        /// Authenticated user must own the account (Admin can deposit into any account).
        /// Amount must be greater than 0. Balance is server-calculated.
        /// </summary>
        [HttpPost("deposit")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(422)]
        public async Task<IActionResult> Deposit([FromBody] DepositRequest request)
        {
            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(callerId))
                return Unauthorized(new { success = false, message = "User identity could not be verified from token." });

            var isAdmin = User.IsInRole("Admin");

            var (exists, isAuthorized, transaction) = await _transactionService
                .DepositAsync(request.AccountId, request.Amount, request.Description, callerId, isAdmin);

            if (!exists)
                return NotFound(new { success = false, message = "Account not found." });

            if (!isAuthorized)
                return Forbid();

            return StatusCode(StatusCodes.Status201Created, new
            {
                success = true,
                message = $"Deposit of {request.Amount:F2} completed successfully.",
                transaction
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/v1/transactions/withdraw
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Withdraws cash from a bank account.
        ///
        /// Available for both Savings and Checking accounts.
        /// Authenticated user must own the account (Admin can withdraw from any account).
        /// Amount must be greater than 0. Insufficient balance returns 422.
        /// </summary>
        [HttpPost("withdraw")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(422)]
        public async Task<IActionResult> Withdraw([FromBody] WithdrawalRequest request)
        {
            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(callerId))
                return Unauthorized(new { success = false, message = "User identity could not be verified from token." });

            var isAdmin = User.IsInRole("Admin");

            var (exists, isAuthorized, transaction) = await _transactionService
                .WithdrawAsync(request.AccountId, request.Amount, request.Description, callerId, isAdmin);

            if (!exists)
                return NotFound(new { success = false, message = "Account not found." });

            if (!isAuthorized)
                return Forbid();

            return StatusCode(StatusCodes.Status201Created, new
            {
                success = true,
                message = $"Withdrawal of {request.Amount:F2} completed successfully.",
                transaction
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/v1/transactions/transfer
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Performs a cheque transfer from a Checking account to any destination account.
        ///
        /// Source account must be a Checking account owned by the authenticated user.
        /// Destination can belong to any user.
        /// Savings accounts cannot initiate transfers (returns 422).
        /// Entire operation is atomic (database transaction — either both accounts update or neither).
        /// </summary>
        [HttpPost("transfer")]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(422)]
        public async Task<IActionResult> Transfer([FromBody] TransferRequest request)
        {
            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(callerId))
                return Unauthorized(new { success = false, message = "User identity could not be verified from token." });

            var isAdmin = User.IsInRole("Admin");

            var (exists, isAuthorized, sourceTxn, destinationTxn) = await _transactionService
                .TransferAsync(
                    request.SourceAccountId,
                    request.DestinationAccountId,
                    request.Amount,
                    request.Description,
                    callerId,
                    isAdmin);

            if (!exists)
                return NotFound(new { success = false, message = "Source account not found." });

            if (!isAuthorized)
                return Forbid();

            return StatusCode(StatusCodes.Status201Created, new
            {
                success = true,
                message = $"Cheque transfer of {request.Amount:F2} completed successfully.",
                sourceTransaction      = sourceTxn,
                destinationTransaction = destinationTxn
            });
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/v1/transactions
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Retrieves paginated transaction history with optional filters.
        ///
        /// Supported query filters:
        ///   - accountId: Filter by specific account.
        ///   - fromDate, toDate: Filter by transaction date range (UTC).
        ///   - lastN: Retrieve the most recent N transactions (1 to 100).
        ///   - transactionType: Filter by Deposit, Withdrawal, or Transfer.
        ///   - transactionMode: Filter by Cash or Cheque.
        ///   - page, pageSize: Pagination controls (default page 1, pageSize 20, max 100).
        ///
        /// Authorization:
        ///   - Normal users: Can only view transactions from accounts they own.
        ///   - Admins: Can view transactions across all accounts.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(PagedResult<TransactionResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTransactions([FromQuery] TransactionHistoryQuery query)
        {
            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(callerId))
                return Unauthorized(new { success = false, message = "User identity could not be verified from token." });

            var isAdmin = User.IsInRole("Admin");

            var (accountExists, isAuthorized, result) = await _transactionService
                .GetTransactionsAsync(query, callerId, isAdmin);

            if (!accountExists)
                return NotFound(new { success = false, message = "Account not found." });

            if (!isAuthorized)
                return Forbid();

            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/v1/transactions/{transactionId}
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Retrieves detailed information for a specific transaction by ID.
        ///
        /// Authorization:
        ///   - Normal users: Can only access transactions for accounts they own.
        ///   - Admins: Can access any transaction.
        /// </summary>
        [HttpGet("{transactionId}")]
        [ProducesResponseType(typeof(TransactionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetTransactionById(Guid transactionId)
        {
            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(callerId))
                return Unauthorized(new { success = false, message = "User identity could not be verified from token." });

            var isAdmin = User.IsInRole("Admin");

            var (exists, isAuthorized, transaction) = await _transactionService
                .GetTransactionByIdAsync(transactionId, callerId, isAdmin);

            if (!exists)
                return NotFound(new { success = false, message = "Transaction not found." });

            if (!isAuthorized)
                return Forbid();

            return Ok(transaction);
        }
    }
}

