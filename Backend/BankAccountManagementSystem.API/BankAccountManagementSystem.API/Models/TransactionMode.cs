namespace BankAccountManagementSystem.API.Models
{
    /// <summary>
    /// Describes the payment channel / instrument used in the transaction.
    ///
    /// Cash   → Physical currency; single-account operation (Deposit or Withdrawal).
    ///          Allowed on both Savings and Checking accounts.
    ///
    /// Cheque → Paper instrument; triggers a Transfer between two Checking accounts.
    ///          Only Checking accounts may initiate a Cheque transfer as the source.
    ///          Destination account may be Savings or Checking (it is a credit to the destination).
    /// </summary>
    public enum TransactionMode
    {
        Cash   = 1,
        Cheque = 2
    }
}
