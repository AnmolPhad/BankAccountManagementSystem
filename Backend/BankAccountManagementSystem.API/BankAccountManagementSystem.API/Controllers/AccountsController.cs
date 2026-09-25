using System.Security.Claims;
using BankAccountManagementSystem.API.DTOs.Accounts;
using BankAccountManagementSystem.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankAccountManagementSystem.API.Controllers
{
    /// <summary>
    /// Bank account management endpoints.
    ///
    /// Security & Authorization:
    /// - All endpoints require authentication ([Authorize]).
    /// - Accounts are created under the identity of the authenticated caller (ClaimTypes.NameIdentifier).
    ///   Client-supplied UserIds are never trusted.
    /// - Normal users can only retrieve accounts they own.
    /// - Admins can retrieve all accounts across the system.
    /// </summary>
    [ApiController]
    [Route("api/v1/accounts")]
    [Authorize]
    public class AccountsController : ControllerBase
    {
        private readonly IAccountService _accountService;

        public AccountsController(IAccountService accountService)
        {
            _accountService = accountService;
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/v1/accounts
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Opens a new Savings or Checking bank account for the authenticated user.
        /// Initial balance starts at 0.00. Account number is securely generated.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request)
        {
            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(callerId))
            {
                return Unauthorized(new { success = false, message = "User identity could not be verified from token." });
            }

            var (success, message, account) = await _accountService.CreateAccountAsync(callerId, request);

            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return CreatedAtAction(
                nameof(GetAccountById),
                new { id = account!.AccountId },
                new
                {
                    success = true,
                    message,
                    account
                });
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/v1/accounts/{id}
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Retrieves account details by ID. Normal users can only view their own accounts.
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAccountById(Guid id)
        {
            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(callerId))
            {
                return Unauthorized();
            }

            var isAdmin = User.IsInRole("Admin");

            var (exists, isAuthorized, account) = await _accountService.GetAccountByIdAsync(id, callerId, isAdmin);

            if (!exists)
            {
                return NotFound(new { success = false, message = "Account not found." });
            }

            if (!isAuthorized)
            {
                return Forbid(); // 403 Forbidden: Caller is not the account owner and not an Admin
            }

            return Ok(account);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/v1/accounts
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Lists bank accounts.
        /// - Normal users receive only accounts they own.
        /// - Admins receive all accounts in the system.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<AccountResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAccounts()
        {
            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(callerId))
            {
                return Unauthorized();
            }

            var isAdmin = User.IsInRole("Admin");

            var accounts = await _accountService.GetAccountsAsync(callerId, isAdmin);

            return Ok(accounts);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/v1/accounts/{id}/balance
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Queries the current balance and active state of an account.
        /// - Normal users can only query their own account balance.
        /// - Admins can query any account balance.
        /// </summary>
        [HttpGet("{id}/balance")]
        [ProducesResponseType(typeof(AccountBalanceResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAccountBalance(Guid id)
        {
            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(callerId))
            {
                return Unauthorized();
            }

            var isAdmin = User.IsInRole("Admin");

            var (exists, isAuthorized, balance) = await _accountService.GetAccountBalanceAsync(id, callerId, isAdmin);

            if (!exists)
            {
                return NotFound(new { success = false, message = "Account not found." });
            }

            if (!isAuthorized)
            {
                return Forbid(); // 403 Forbidden: User accessing another user's account
            }

            return Ok(balance);
        }

        // ─────────────────────────────────────────────────────────────────────
        // DELETE /api/v1/accounts/{id}
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Deactivates an account (soft delete: IsActive = false, UpdatedAt = UTC now).
        /// Row remains in the database for audit integrity.
        /// - Normal users can deactivate only their own accounts.
        /// - Admins can deactivate any account.
        /// </summary>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeactivateAccount(Guid id)
        {
            var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(callerId))
            {
                return Unauthorized();
            }

            var isAdmin = User.IsInRole("Admin");

            var (exists, isAuthorized, alreadyInactive, message) = await _accountService.DeactivateAccountAsync(id, callerId, isAdmin);

            if (!exists)
            {
                return NotFound(new { success = false, message });
            }

            if (!isAuthorized)
            {
                return Forbid(); // 403 Forbidden: User accessing another user's account
            }

            if (alreadyInactive)
            {
                return BadRequest(new { success = false, message }); // 400 Bad Request: already inactive
            }

            return Ok(new { success = true, message });
        }
    }
}
