namespace BankAccountManagementSystem.API.Exceptions
{
    /// <summary>
    /// Thrown when an account does not have sufficient balance to complete a Withdrawal or Transfer.
    ///
    /// Maps to HTTP 422 Unprocessable Entity via ExceptionMiddleware.
    ///
    /// Example:
    ///   Account balance = 1000.00, requested withdrawal = 3000.00
    ///   → throw new InsufficientBalanceException(1000.00m, 3000.00m)
    ///   → Response: 422 { "success": false, "message": "Insufficient balance. Available: 1000.00, Requested: 3000.00." }
    /// </summary>
    public class InsufficientBalanceException : InvalidOperationException
    {
        public decimal AvailableBalance { get; }
        public decimal RequestedAmount  { get; }

        public InsufficientBalanceException(decimal available, decimal requested)
            : base($"Insufficient balance. Available: {available:F2}, Requested: {requested:F2}.")
        {
            AvailableBalance = available;
            RequestedAmount  = requested;
        }
    }

    /// <summary>
    /// Thrown when a transaction is attempted on an account that has been deactivated (soft-deleted).
    ///
    /// Maps to HTTP 422 Unprocessable Entity via ExceptionMiddleware.
    /// </summary>
    public class AccountInactiveException : InvalidOperationException
    {
        public AccountInactiveException(string accountNumber)
            : base($"Account '{accountNumber}' is not active. Transactions cannot be performed on a deactivated account.")
        {
        }
    }

    /// <summary>
    /// Thrown when a transaction violates general business rules
    /// (e.g., source and destination accounts are the same in a Transfer).
    ///
    /// Maps to HTTP 422 Unprocessable Entity via ExceptionMiddleware.
    /// </summary>
    public class InvalidTransactionException : InvalidOperationException
    {
        public InvalidTransactionException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Thrown when an operation is attempted on an account type that does not support it.
    ///
    /// Examples:
    ///   - A Savings account attempts to initiate a cheque Transfer.
    ///   - A Savings account uses Cheque mode.
    ///
    /// Maps to HTTP 422 Unprocessable Entity via ExceptionMiddleware.
    /// </summary>
    public class InvalidAccountTypeException : InvalidOperationException
    {
        public InvalidAccountTypeException(string message)
            : base(message)
        {
        }
    }
}
