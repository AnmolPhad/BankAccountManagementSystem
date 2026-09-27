import { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard, LogOut, Building2, ShieldCheck,
  CreditCard, ArrowLeft, ArrowUpRight, CheckCircle2,
  Loader2, AlertCircle, ArrowRightLeft, PlusCircle
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import accountService from '../services/accountService';
import transactionService from '../services/transactionService';
import styles from './WithdrawPage.module.css';

export default function WithdrawPage() {
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

    if (!selectedAccountId || !selectedAccount) {
      setError('Please select an account.');
      return;
    }

    const numAmount = Number(amount);
    if (isNaN(numAmount) || numAmount <= 0) {
      setError('Withdrawal amount must be greater than 0.');
      return;
    }

    // Client pre-check for balance
    if (numAmount > (selectedAccount.balance || 0)) {
      setError(`Insufficient funds. Your available balance is $${(selectedAccount.balance || 0).toFixed(2)}.`);
      return;
    }

    setLoading(true);
    try {
      const res = await transactionService.withdraw({
        accountId: selectedAccountId,
        amount: numAmount,
        description,
      });

      setResult({
        message: res.message || 'Withdrawal completed successfully.',
        transaction: res.transaction,
      });

      // Reload accounts to refresh balances
      await loadAccounts();
    } catch (err) {
      console.error('Withdrawal error:', err);
      const msg = err.response?.data?.message || 'Withdrawal failed. Please check your balance and try again.';
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
    <div className={styles.root}>

      {/* ── Sidebar ──────────────────────────────────────────── */}
      <aside className={styles.sidebar}>
        <div className={styles.sidebarLogo}>
          <div className={styles.logoIcon}><Building2 size={22} /></div>
          <span className={styles.logoText}>NexaBank</span>
        </div>

        <nav className={styles.nav}>
          <Link to="/dashboard" className={styles.navItem}>
            <LayoutDashboard size={18} />
            <span>Dashboard</span>
          </Link>
          <Link to="/accounts" className={styles.navItem}>
            <CreditCard size={18} />
            <span>Accounts</span>
          </Link>
          <Link to="/transactions" className={`${styles.navItem} ${styles.navActive}`}>
            <ArrowRightLeft size={18} />
            <span>Transactions</span>
          </Link>
        </nav>

        <div className={styles.sidebarFooter}>
          <div className={styles.userChip}>
            <div className={styles.avatar}>{initials}</div>
            <div className={styles.userInfo}>
              <p className={styles.userName}>{displayName}</p>
              <p className={styles.userRole}>
                <ShieldCheck size={11} />
                {user?.role ?? 'User'}
              </p>
            </div>
          </div>
          <button onClick={handleLogout} className={styles.logoutBtn} title="Sign out">
            <LogOut size={16} />
          </button>
        </div>
      </aside>

      {/* ── Main content ─────────────────────────────────────── */}
      <main className={styles.main}>

        <Link to="/transactions" className={styles.backLink}>
          <ArrowLeft size={16} /> Back to Transactions
        </Link>

        {result ? (
          /* ── Success Screen ────────────────────────────────────────── */
          <div className={styles.successCard}>
            <div className={styles.successIcon}><CheckCircle2 size={40} /></div>
            <h2 className={styles.successTitle}>Cash Withdrawal Successful!</h2>
            <p className={styles.successBody}>{result.message}</p>

            <div className={styles.detailsBox}>
              <div className={styles.detailRow}>
                <span className={styles.detailLabel}>Reference Number</span>
                <span className={styles.detailValue}>
                  {result.transaction?.referenceNumber || 'N/A'}
                </span>
              </div>

              <div className={styles.detailRow}>
                <span className={styles.detailLabel}>Withdrawn Amount</span>
                <span className={styles.detailValue} style={{ color: '#ef4444' }}>
                  -${(result.transaction?.amount ?? Number(amount)).toFixed(2)}
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
                <PlusCircle size={16} /> Make Another Withdrawal
              </button>
            </div>
          </div>
        ) : (
          /* ── Withdrawal Form ───────────────────────────────────────── */
          <>
            <div className={styles.pageHeader}>
              <h1 className={styles.pageTitle}>Cash Withdrawal</h1>
              <p className={styles.pageSubtitle}>
                Withdraw cash from your Savings or Checking bank account.
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
                      <span className={styles.balLabel}>Available Balance</span>
                      <span className={styles.balAmount}>${(selectedAccount.balance || 0).toFixed(2)}</span>
                    </div>
                  </div>
                )}

                {/* Amount */}
                <div className={styles.fieldGroup}>
                  <label htmlFor="amount" className={styles.label}>Withdrawal Amount ($)</label>
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
                    placeholder="e.g. ATM cash withdrawal, Rent payment"
                    value={description}
                    onChange={(e) => setDescription(e.target.value)}
                    className={styles.input}
                    disabled={loading}
                  />
                </div>

                <button type="submit" className={styles.submitBtn} disabled={loading}>
                  {loading ? (
                    <><Loader2 size={18} className={styles.spinner} /> Processing Withdrawal…</>
                  ) : (
                    <><ArrowUpRight size={18} /> Confirm Cash Withdrawal</>
                  )}
                </button>
              </form>
            )}
          </>
        )}

      </main>
    </div>
  );
}
