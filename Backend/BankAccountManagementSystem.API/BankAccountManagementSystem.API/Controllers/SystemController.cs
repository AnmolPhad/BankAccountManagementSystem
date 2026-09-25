using BankAccountManagementSystem.API.DTOs.System;
using BankAccountManagementSystem.API.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BankAccountManagementSystem.API.Controllers
{
    /// <summary>
    /// Administrative system management endpoints for Save (Export) and Restore (Import).
    /// Access is strictly restricted to users in the Admin role.
    /// </summary>
    [ApiController]
    [Route("api/v1/system")]
    [Authorize(Roles = "Admin")]
    public class SystemController : ControllerBase
    {
        private readonly ISystemBackupService _backupService;

        public SystemController(ISystemBackupService backupService)
        {
            _backupService = backupService;
        }

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/v1/system/save
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Exports a complete snapshot of all bank accounts and financial transactions.
        ///
        /// Requirements:
        ///   - Admin role required. Normal users receive 403 Forbidden.
        ///   - Read-only: does not modify database state.
        /// </summary>
        [HttpGet("save")]
        [ProducesResponseType(typeof(SystemBackupDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Save()
        {
            var backup = await _backupService.ExportBackupAsync();
            return Ok(backup);
        }

        // ─────────────────────────────────────────────────────────────────────
        // POST /api/v1/system/restore
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Atomically restores accounts and transactions from a supplied system backup.
        ///
        /// Requirements:
        ///   - Admin role required. Normal users receive 403 Forbidden.
        ///   - Pre-validates backup payload structure, account unique constraints, and referenced UserIds.
        ///   - Identity application users are preserved; only operational account and transaction data are replaced.
        ///   - Entire operation runs in an EF Core transaction with full rollback on any failure.
        /// </summary>
        [HttpPost("restore")]
        [ProducesResponseType(typeof(RestoreResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Restore([FromBody] SystemBackupDto backup)
        {
            var result = await _backupService.RestoreBackupAsync(backup);
            return Ok(result);
        }
    }
}
