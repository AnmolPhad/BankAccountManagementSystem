import { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard, LogOut, Building2, ShieldCheck,
  CreditCard, ArrowLeft, ArrowDownRight, CheckCircle2,
  Loader2, AlertCircle, ArrowRightLeft, PlusCircle
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import accountService from '../services/accountService';
import transactionService from '../services/transactionService';
import styles from './DepositPage.module.css';

export default function DepositPage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const [accounts, setAccounts] = useState([]);
  const [selectedAccountId, setSelectedAccountId] = useState('');
  const [amount, setAmount] = useState('');
  const [description, setDescription] = useState('');
  const [loading, setLoading] = useState(false);
  const [loadingAccounts, setLoadingAccounts] = useState(true);
  const [error, setError] = useState('');
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
        setSelectedAccountId(active[0].accountId);
      }
    } catch (err) {
      console.error('Error loading accounts:', err);
      setError('Failed to load accounts. Please try again.');
    } finally {
      setLoadingAccounts(false);
    }
  };

  const selectedAccount = accounts.find((a) => a.accountId === selectedAccountId);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');

    if (!selectedAccountId) {
      setError('Please select an account.');
      return;
    }

    const numAmount = Number(amount);
    if (isNaN(numAmount) || numAmount <= 0) {
      setError('Deposit amount must be greater than 0.');
      return;
    }

    setLoading(true);
    try {
      const res = await transactionService.deposit({
        accountId: selectedAccountId,
        amount: numAmount,
        description,
      });

      setResult({
        message: res.message || 'Deposit completed successfully.',
        transaction: res.transaction,
      });

      // Reload accounts to refresh balances
      await loadAccounts();
    } catch (err) {
      console.error('Deposit error:', err);
      const msg = err.response?.data?.message || 'Deposit failed. Please try again.';
      setError(msg);
    } finally {
      setLoading(false);
    }
  };

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  const displayName = user
    ? `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim() || user.email
    : 'User';

  const initials = user
    ? `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase() || 'U'
    : 'U';

  return (
    <div className={styles.pageContent}>

        <Link to="/transactions" className={styles.backLink}>
          <ArrowLeft size={16} /> Back to Transactions
        </Link>

        {result ? (
          /* ── Success Screen ────────────────────────────────────────── */
          <div className={styles.successCard}>
            <div className={styles.successIcon}><CheckCircle2 size={40} /></div>
            <h2 className={styles.successTitle}>Cash Deposit Successful!</h2>
            <p className={styles.successBody}>{result.message}</p>

            <div className={styles.detailsBox}>
              <div className={styles.detailRow}>
                <span className={styles.detailLabel}>Reference Number</span>
                <span className={styles.detailValue}>
                  {result.transaction?.referenceNumber || 'N/A'}
                </span>
              </div>

              <div className={styles.detailRow}>
                <span className={styles.detailLabel}>Deposited Amount</span>
                <span className={styles.detailValue} style={{ color: '#10b981' }}>
                  +${(result.transaction?.amount ?? Number(amount)).toFixed(2)}
                </span>
              </div>

              {selectedAccount && (
                <div className={styles.detailRow}>
                  <span className={styles.detailLabel}>Account Number</span>
                  <span className={styles.detailValue}>{selectedAccount.accountNumber}</span>
                </div>
              )}
            </div>

            <div className={styles.actionGroup}>
              <Link to="/accounts" className={styles.viewBtn}>
                View Accounts
              </Link>
              <button
                onClick={() => {
                  setResult(null);
                  setAmount('');
                  setDescription('');
                }}
                className={styles.anotherBtn}
              >
                <PlusCircle size={16} /> Make Another Deposit
              </button>
            </div>
          </div>
        ) : (
          /* ── Deposit Form ──────────────────────────────────────────── */
          <>
            <div className={styles.pageHeader}>
              <h1 className={styles.pageTitle}>Cash Deposit</h1>
              <p className={styles.pageSubtitle}>
                Deposit cash into your Savings or Checking bank account.
              </p>
            </div>

            {error && (
              <div className={styles.errorBanner} role="alert">
                <AlertCircle size={16} />
                <span>{error}</span>
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
              <form onSubmit={handleSubmit} className={styles.formCard}>

                {/* Account Selector */}
                <div className={styles.fieldGroup}>
                  <label htmlFor="accountId" className={styles.label}>Select Account</label>
                  <select
                    id="accountId"
                    value={selectedAccountId}
                    onChange={(e) => setSelectedAccountId(e.target.value)}
                    className={styles.select}
                    disabled={loading}
                  >
                    {accounts.map((acc) => (
                      <option key={acc.accountId} value={acc.accountId}>
                        {acc.accountNumber} ({acc.accountType}) — ${(acc.balance || 0).toFixed(2)}
                      </option>
                    ))}
                  </select>
                </div>

                {/* Selected Account Details Card */}
                {selectedAccount && (
                  <div className={styles.selectedAccountCard}>
                    <div className={styles.accountMeta}>
                      <span className={styles.accountNum}>{selectedAccount.accountNumber}</span>
                      <span className={styles.accountType}>{selectedAccount.accountType} Account</span>
                    </div>
                    <div className={styles.accountBalance}>
                      <span className={styles.balLabel}>Current Balance</span>
                      <span className={styles.balAmount}>${(selectedAccount.balance || 0).toFixed(2)}</span>
                    </div>
                  </div>
                )}

                {/* Amount */}
                <div className={styles.fieldGroup}>
                  <label htmlFor="amount" className={styles.label}>Deposit Amount ($)</label>
                  <input
                    id="amount"
                    type="number"
                    step="0.01"
                    min="0.01"
                    placeholder="0.00"
                    value={amount}
                    onChange={(e) => setAmount(e.target.value)}
                    className={styles.input}
                    disabled={loading}
                    required
                  />
                </div>

                {/* Description */}
                <div className={styles.fieldGroup}>
                  <label htmlFor="description" className={styles.label}>Description / Note (optional)</label>
                  <input
                    id="description"
                    type="text"
                    maxLength={500}
                    placeholder="e.g. Salary deposit, ATM cash-in"
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    className={styles.input}
                    disabled={loading}
                  />
                </div>

                <button type="submit" className={styles.submitBtn} disabled={loading}>
                  {loading ? (
                    <><Loader2 size={18} className={styles.spinner} /> Processing Deposit…</>
                  ) : (
                    <><ArrowDownRight size={18} /> Confirm Cash Deposit</>
                  )}
                </button>
              </form>
            )}
          </>
        )}

    </div>
  );
}
