import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import {
  ArrowLeft, ArrowRightLeft, CheckCircle2,
  Loader2, AlertCircle, AlertTriangle, PlusCircle
} from 'lucide-react';
import accountService from '../services/accountService';
import transactionService from '../services/transactionService';
import styles from './TransferPage.module.css';

export default function TransferPage() {
  const [accounts, setAccounts] = useState([]);
  const [sourceAccountId, setSourceAccountId] = useState('');
  const [destinationAccountNumber, setDestinationAccountNumber] = useState('');
  const [amount, setAmount] = useState('');
  const [description, setDescription] = useState('');
  const [loading, setLoading] = useState(false);
  const [loadingAccounts, setLoadingAccounts] = useState(true);
  const [error, setError] = useState('');
  const [warningMsg, setWarningMsg] = useState('');
  const [showConfirmModal, setShowConfirmModal] = useState(false);
  const [result, setResult] = useState(null);

  useEffect(() => {
    loadAccounts();
  }, []);

  const loadAccounts = async () => {
    setLoadingAccounts(true);
    try {
      const data = await accountService.getAccounts();
      const active = (data || []).filter((a) => a.isActive !== false);
      setAccounts(active);
      if (active.length > 0) {
        // Prefer selecting a Checking account first if available
        const checking = active.find((a) => a.accountType === 'Checking' || a.accountType === 2);
        setSourceAccountId(checking ? checking.accountId : active[0].accountId);
      }
    } catch (err) {
      console.error('Error loading accounts:', err);
      setError('Failed to load accounts. Please try again.');
    } finally {
      setLoadingAccounts(false);
    }
  };

  const sourceAccount = accounts.find((a) => a.accountId === sourceAccountId);
  const isSavingsSource = sourceAccount && (sourceAccount.accountType === 'Savings' || sourceAccount.accountType === 1);

  // Update warning message whenever source account changes
  useEffect(() => {
    if (isSavingsSource) {
      setWarningMsg('Cheque transfers are NOT allowed from a Savings account. Please select or open a Checking account to initiate cheque transfers.');
    } else {
      setWarningMsg('');
    }
  }, [sourceAccountId, isSavingsSource]);

  const handleInitiateClick = (e) => {
    e.preventDefault();
    setError('');

    if (!sourceAccountId || !sourceAccount) {
      setError('Please select a source account.');
      return;
    }

    if (isSavingsSource) {
      setError('Cheque transfers are prohibited from Savings accounts. Only Checking accounts can initiate cheque transfers.');
      return;
    }

    const destNum = destinationAccountNumber.trim();
    if (!destNum) {
      setError('Please enter a destination account number.');
      return;
    }

    if (destNum === sourceAccount.accountNumber) {
      setError('Source and destination accounts must be different.');
      return;
    }

    const numAmount = Number(amount);
    if (isNaN(numAmount) || numAmount <= 0) {
      setError('Transfer amount must be greater than 0.');
      return;
    }

    if (numAmount > (sourceAccount.balance || 0)) {
      setError(`Insufficient funds. Source account balance is ₹${(sourceAccount.balance || 0).toLocaleString('en-IN', { minimumFractionDigits: 2 })}.`);
      return;
    }

    // Open confirmation modal
    setShowConfirmModal(true);
  };

  const confirmAndExecuteTransfer = async () => {
    setShowConfirmModal(false);
    setError('');
    setLoading(true);

    try {
      // 1. Resolve Destination Account Number to Account ID GUID
      let destAccount;
      try {
        destAccount = await transactionService.getAccountByNumber(destinationAccountNumber.trim());
      } catch (lookupErr) {
        throw new Error(lookupErr.response?.data?.message || `Destination account '${destinationAccountNumber}' not found or inactive.`);
      }

      if (!destAccount || !destAccount.accountId) {
        throw new Error(`Destination account '${destinationAccountNumber}' not found.`);
      }

      if (destAccount.accountId === sourceAccountId) {
        throw new Error('Source and destination accounts must be different.');
      }

      // 2. Submit Cheque Transfer
      const res = await transactionService.transfer({
        sourceAccountId,
        destinationAccountId: destAccount.accountId,
        amount: Number(amount),
        description,
      });

      setResult({
        message: res.message || 'Cheque transfer completed successfully.',
        sourceTransaction: res.sourceTransaction,
        destinationAccountNumber: destAccount.accountNumber,
      });

      // Reload accounts to refresh balances
      await loadAccounts();
    } catch (err) {
      console.error('Transfer error:', err);
      const msg = err.response?.data?.message || err.message || 'Transfer failed. Please verify destination account number and balance.';
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className={styles.pageContent}>

      <Link to="/transactions" className={styles.backLink}>
        <ArrowLeft size={16} /> Back to Transactions
      </Link>

      {result ? (
        /* ── Success Screen ────────────────────────────────────────── */
        <div className={styles.successCard}>
          <div className={styles.successIcon}><CheckCircle2 size={40} /></div>
          <h2 className={styles.successTitle}>Cheque Transfer Completed!</h2>
          <p className={styles.successBody}>{result.message}</p>

          <div className={styles.detailsBox}>
            <div className={styles.detailRow}>
              <span className={styles.detailLabel}>Reference Number</span>
              <span className={styles.detailValue}>
                {result.sourceTransaction?.referenceNumber || 'N/A'}
              </span>
            </div>

            <div className={styles.detailRow}>
              <span className={styles.detailLabel}>Transfer Amount</span>
              <span className={styles.detailValue} style={{ color: '#3b82f6' }}>
                ₹{(result.sourceTransaction?.amount ?? Number(amount)).toLocaleString('en-IN', { minimumFractionDigits: 2 })}
              </span>
            </div>

            {sourceAccount && (
              <div className={styles.detailRow}>
                <span className={styles.detailLabel}>From Source Account</span>
                <span className={styles.detailValue}>{sourceAccount.accountNumber} (Checking)</span>
              </div>
            )}

            <div className={styles.detailRow}>
              <span className={styles.detailLabel}>To Destination Account</span>
              <span className={styles.detailValue}>{result.destinationAccountNumber}</span>
            </div>
          </div>

          <div className={styles.actionGroup}>
            <Link to="/accounts" className={styles.viewBtn}>
              View Accounts
            </Link>
            <button
              onClick={() => {
                setResult(null);
                setDestinationAccountNumber('');
                setAmount('');
                setDescription('');
              }}
              className={styles.anotherBtn}
            >
              <PlusCircle size={16} /> Make Another Transfer
            </button>
          </div>
        </div>
      ) : (
        /* ── Transfer Form ─────────────────────────────────────────── */
        <>
          <div className={styles.pageHeader}>
            <h1 className={styles.pageTitle}>Cheque Transfer</h1>
            <p className={styles.pageSubtitle}>
              Transfer funds from a Checking account to any destination account.
            </p>
          </div>

          {error && (
            <div className={styles.errorBanner} role="alert">
              <AlertCircle size={16} />
              <span>{error}</span>
            </div>
          )}

          {warningMsg && (
            <div className={styles.warningBanner} role="alert">
              <AlertTriangle size={16} />
              <span>{warningMsg}</span>
            </div>
          )}

          {loadingAccounts ? (
            <div className={styles.loadingContainer}>
              <Loader2 size={32} className={styles.spinner} />
              <p>Loading your bank accounts…</p>
            </div>
          ) : accounts.length === 0 ? (
            <div className={styles.errorBanner} role="alert">
              <AlertCircle size={16} />
              <span>You do not have any open bank accounts. Please open an account first.</span>
            </div>
          ) : (
            <form onSubmit={handleInitiateClick} className={styles.formCard}>

              {/* Source Account Selector */}
              <div className={styles.fieldGroup}>
                <label htmlFor="sourceAccountId" className={styles.label}>Select Source Account (Checking Only)</label>
                <select
                  id="sourceAccountId"
                  value={sourceAccountId}
                  onChange={(e) => setSourceAccountId(e.target.value)}
                  className={styles.select}
                  disabled={loading}
                >
                  {accounts.map((acc) => (
                    <option key={acc.accountId} value={acc.accountId}>
                      {acc.accountNumber} ({acc.accountType}) — ₹{(acc.balance || 0).toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                    </option>
                  ))}
                </select>
              </div>

              {/* Selected Source Account Card */}
              {sourceAccount && (
                <div className={styles.selectedAccountCard}>
                  <div className={styles.accountMeta}>
                    <span className={styles.accountNum}>{sourceAccount.accountNumber}</span>
                    <span className={styles.accountType}>{sourceAccount.accountType} Account</span>
                  </div>
                  <div className={styles.accountBalance}>
                    <span className={styles.balLabel}>Available Balance</span>
                    <span className={styles.balAmount}>₹{(sourceAccount.balance || 0).toLocaleString('en-IN', { minimumFractionDigits: 2 })}</span>
                  </div>
                </div>
              )}

              {/* Destination Account Number */}
              <div className={styles.fieldGroup}>
                <label htmlFor="destinationAccountNumber" className={styles.label}>Destination Account Number</label>
                <input
                  id="destinationAccountNumber"
                  type="text"
                  placeholder="e.g. 1095416885 or 2021477643"
                  value={destinationAccountNumber}
                  onChange={(e) => setDestinationAccountNumber(e.target.value)}
                  className={styles.input}
                  disabled={loading || isSavingsSource}
                  required
                />
              </div>

              {/* Amount */}
              <div className={styles.fieldGroup}>
                <label htmlFor="amount" className={styles.label}>Transfer Amount (₹)</label>
                <input
                  id="amount"
                  type="number"
                  step="0.01"
                  min="0.01"
                  placeholder="0.00"
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  className={styles.input}
                  disabled={loading || isSavingsSource}
                  required
                />
              </div>

              {/* Description */}
              <div className={styles.fieldGroup}>
                <label htmlFor="description" className={styles.label}>Description / Reference Note (optional)</label>
                <input
                  id="description"
                  type="text"
                  maxLength={500}
                  placeholder="e.g. Rent payment, Invoice #1042"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  className={styles.input}
                  disabled={loading || isSavingsSource}
                />
              </div>

              <button
                type="submit"
                className={styles.submitBtn}
                disabled={loading || isSavingsSource}
              >
                {loading ? (
                  <><Loader2 size={18} className={styles.spinner} /> Processing Transfer…</>
                ) : (
                  <><ArrowRightLeft size={18} /> Initiate Cheque Transfer</>
                )}
              </button>
            </form>
          )}

          {/* Confirmation Modal */}
          {showConfirmModal && (
            <div className={styles.modalOverlay} role="dialog" aria-modal="true">
              <div className={styles.modalCard}>
                <div className={styles.modalWarnIcon}><AlertTriangle size={28} /></div>
                <h3 className={styles.modalTitle}>Confirm Cheque Transfer</h3>
                <p className={styles.modalBody}>
                  Please review your cheque transfer details before confirming:
                </p>

                <div className={styles.modalSummary}>
                  <div className={styles.summaryRow}>
                    <span className={styles.summaryLabel}>From Checking Account:</span>
                    <span className={styles.summaryVal}>{sourceAccount?.accountNumber}</span>
                  </div>
                  <div className={styles.summaryRow}>
                    <span className={styles.summaryLabel}>To Destination Account:</span>
                    <span className={styles.summaryVal}>{destinationAccountNumber}</span>
                  </div>
                  <div className={styles.summaryRow}>
                    <span className={styles.summaryLabel}>Transfer Amount:</span>
                    <span className={styles.summaryVal} style={{ color: '#60a5fa' }}>
                      ₹{Number(amount).toLocaleString('en-IN', { minimumFractionDigits: 2 })}
                    </span>
                  </div>
                  {description && (
                    <div className={styles.summaryRow}>
                      <span className={styles.summaryLabel}>Note:</span>
                      <span className={styles.summaryVal}>{description}</span>
                    </div>
                  )}
                </div>

                <div className={styles.modalActions}>
                  <button
                    onClick={() => setShowConfirmModal(false)}
                    className={styles.cancelBtn}
                    disabled={loading}
                  >
                    Cancel
                  </button>
                  <button
                    onClick={confirmAndExecuteTransfer}
                    className={styles.confirmBtn}
                    disabled={loading}
                  >
                    {loading ? <Loader2 size={16} className={styles.spinner} /> : <ArrowRightLeft size={16} />}
                    Confirm Transfer
                  </button>
                </div>
              </div>
            </div>
          )}
        </>
      )}

    </div>
  );
}
