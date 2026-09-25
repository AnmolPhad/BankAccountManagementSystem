using BankAccountManagementSystem.API.DTOs.System;

namespace BankAccountManagementSystem.API.Services.Interfaces
{
    /// <summary>
    /// Contract for system-level save (export) and restore (import) operations.
    /// Strictly restricted to administrative authorization.
    /// </summary>
    public interface ISystemBackupService
    {
        /// <summary>
        /// Exports the current state of all bank accounts and transactions.
        /// Uses AsNoTracking() and executes without modifying database state.
        /// </summary>
        Task<SystemBackupDto> ExportBackupAsync();

        /// <summary>
        /// Atomically restores accounts and transactions from the supplied backup payload.
        /// Validates backup integrity and referenced users before making changes.
        /// Executes within an EF Core database transaction with complete rollback on any failure.
        /// </summary>
        Task<RestoreResponseDto> RestoreBackupAsync(SystemBackupDto backup);
    }
}
