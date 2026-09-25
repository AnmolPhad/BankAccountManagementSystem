using System.ComponentModel.DataAnnotations;
using BankAccountManagementSystem.API.Models;

namespace BankAccountManagementSystem.API.DTOs.Accounts
{
    /// <summary>
    /// DTO for creating a new bank account.
    ///
    /// Security & Domain Rules:
    /// - Does NOT allow AccountId, Balance, AccountNumber, UserId, CreatedAt, UpdatedAt, or IsActive.
    /// - The server strictly governs balance initialization (always 0.00), ownership (from JWT),
    ///   and account number generation.
    /// </summary>
    public class CreateAccountRequest
    {
        /// <summary>
        /// Type of account to create: 1 = Savings, 2 = Checking.
        /// </summary>
        [Required(ErrorMessage = "AccountType is required.")]
        [EnumDataType(typeof(AccountType), ErrorMessage = "Invalid account type. Valid types are 1 (Savings) or 2 (Checking).")]
        public AccountType AccountType { get; set; }
    }
}
