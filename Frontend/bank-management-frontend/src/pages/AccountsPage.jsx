import { useState, useEffect } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard, LogOut, Building2, User, ShieldCheck,
  CreditCard, Plus, Trash2, AlertCircle, CheckCircle2, Loader2,
  DollarSign, Check, X, AlertTriangle, Eye, History, ArrowRightLeft, Database
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import accountService from '../services/accountService';
import styles from './AccountsPage.module.css';

export default function AccountsPage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const [accounts, setAccounts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [successMsg, setSuccessMsg] = useState('');
  const [deletingId, setDeletingId] = useState(null); // Account ID pending deletion
  const [isDeleting, setIsDeleting] = useState(false);

  // Fetch customer accounts on mount
  useEffect(() => {
    loadAccounts();
  }, []);

  const loadAccounts = async () => {
    setLoading(true);
    setError('');
    try {
      const data = await accountService.getAccounts();
      setAccounts(data || []);
    } catch (err) {
      console.error('Failed to load accounts:', err);
      setError(err.response?.data?.message || 'Unable to fetch accounts. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const handleDeleteClick = (accountId) => {
    setDeletingId(accountId);
  };

  const confirmDelete = async () => {
    if (!deletingId) return;
    const targetAccount = accounts.find((a) => a.accountId === deletingId);
    if (targetAccount && Number(targetAccount.balance ?? 0) > 0) {
      setError('Account cannot be deleted because the balance must be zero.');
      setDeletingId(null);
      return;
    }
    setIsDeleting(true);
    setError('');
    try {
      await accountService.deactivateAccount(deletingId);
      setSuccessMsg('Account deactivated successfully.');
      setDeletingId(null);
      await loadAccounts();
    } catch (err) {
      console.error('Deactivation error:', err);
      setError(err.response?.data?.message || 'Failed to deactivate account.');
      setDeletingId(null);
    } finally {
      setIsDeleting(false);
      setTimeout(() => setSuccessMsg(''), 4000);
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

  const totalBalance = accounts.reduce((sum, acc) => sum + (acc.balance || 0), 0);
  const activeAccounts = accounts.filter((acc) => acc.isActive !== false);

  const formatCurrency = (val) => {
    const num = Number(val ?? 0);
    return `₹${num.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  return (
    <div className={styles.pageContent}>

        {/* Topbar */}
        <header className={styles.topbar}>
          <div>
            <h1 className={styles.pageTitle}>Bank Accounts</h1>
            <p className={styles.pageSubtitle}>
              Manage your Savings and Checking bank accounts
            </p>
          </div>
          <Link to="/accounts/add" className={styles.addBtn}>
            <Plus size={18} /> Open New Account
          </Link>
        </header>

        {/* Banners */}
        {error && (
          <div className={styles.errorBanner} role="alert">
            <AlertCircle size={16} />
            <span>{error}</span>
          </div>
        )}

        {successMsg && (
          <div className={styles.successBanner} role="status">
            <CheckCircle2 size={16} />
            <span>{successMsg}</span>
          </div>
        )}

        {/* Summary Stats */}
        <section className={styles.statsRow}>
          <div className={styles.statCard}>
            <div className={styles.statIcon}><CreditCard size={20} /></div>
            <div className={styles.statInfo}>
              <span className={styles.statLabel}>Total Accounts</span>
              <span className={styles.statValue}>{activeAccounts.length}</span>
            </div>
          </div>

          <div className={styles.statCard}>
            <div className={styles.statIcon} style={{ color: '#10b981', background: 'rgba(16,185,129,0.12)', borderColor: 'rgba(16,185,129,0.25)' }}>
              <DollarSign size={20} />
            </div>
            <div className={styles.statInfo}>
              <span className={styles.statLabel}>Combined Balance</span>
              <span className={styles.statValue}>{formatCurrency(totalBalance)}</span>
            </div>
          </div>
        </section>

        {/* Accounts List / Loading / Empty */}
        {loading ? (
          <div className={styles.loadingContainer}>
            <Loader2 size={32} className={styles.spinner} />
            <p>Loading your bank accounts…</p>
          </div>
        ) : activeAccounts.length === 0 ? (
          <div className={styles.emptyState}>
            <div className={styles.emptyIcon}><CreditCard size={32} /></div>
            <h2 className={styles.emptyTitle}>No Bank Accounts Opened</h2>
            <p className={styles.emptyDesc}>
              You do not have any active bank accounts yet. Open a Savings or Checking account to begin managing your funds.
            </p>
            <Link to="/accounts/add" className={styles.addBtn}>
              <Plus size={18} /> Open Your First Account
            </Link>
          </div>
        ) : (
          <section className={styles.accountGrid}>
            {activeAccounts.map((acc) => {
              const isSavings = (acc.accountType === 'Savings' || acc.accountType === 1);
              const typeName = isSavings ? 'Savings' : 'Checking';
              const isZeroBalance = Number(acc.balance ?? 0) === 0;

              return (
                <div key={acc.accountId} className={styles.accountCard}>
                  {/* Header */}
                  <div className={styles.cardHeader}>
                    <div className={`${styles.cardTypeIcon} ${isSavings ? styles.savingsIcon : styles.checkingIcon}`}>
                      <CreditCard size={22} />
                    </div>
                    <span className={`${styles.typeBadge} ${isSavings ? styles.savingsBadge : styles.checkingBadge}`}>
                      {typeName} Account
                    </span>
                  </div>

                  {/* Account Number */}
                  <div className={styles.cardNumGroup}>
                    <span className={styles.cardNumLabel}>Account Number</span>
                    <span className={styles.cardNumValue}>{acc.accountNumber}</span>
                  </div>

                  {/* Balance */}
                  <div className={styles.balanceBlock}>
                    <span className={styles.balanceLabel}>Current Balance</span>
                    <span className={styles.balanceValue}>{formatCurrency(acc.balance)}</span>
                  </div>

                  {/* Account Rules indicator */}
                  <div className={styles.rulesSection}>
                    <div className={styles.ruleItem}>
                      <Check size={13} className={styles.ruleAllowed} /> Cash transactions allowed
                    </div>
                    <div className={styles.ruleItem}>
                      {isSavings ? (
                        <span className={styles.ruleNotAllowed}><X size={13} /> Cheque transactions NOT allowed</span>
                      ) : (
                        <span className={styles.ruleAllowed}><Check size={13} /> Cheque transactions allowed</span>
                      )}
                    </div>
                  </div>

                  {/* Action Buttons & Footer */}
                  <div className={styles.cardActionsRow}>
                    <Link
                      to={`/accounts/${acc.accountId}`}
                      className={styles.detailsBtn}
                      title="View Details & Balance"
                    >
                      <Eye size={14} /> Details
                    </Link>
                    <Link
                      to={`/transactions/history?accountId=${acc.accountId}`}
                      className={styles.historyCardBtn}
                      title="View Transaction History"
                    >
                      <History size={14} /> History
                    </Link>
                    <button
                      onClick={() => handleDeleteClick(acc.accountId)}
                      className={`${styles.deleteBtn} ${!isZeroBalance ? styles.deleteBtnDisabled : ''}`}
                      disabled={!isZeroBalance}
                      title={isZeroBalance ? "Delete Account" : "Account can be deleted only when the balance is ₹0.00."}
                    >
                      <Trash2 size={13} /> Delete
                    </button>
                  </div>

                  {!isZeroBalance && (
                    <div className={styles.balanceDeleteNotice}>
                      <AlertCircle size={12} className={styles.noticeIcon} />
                      <span>Account can be deleted only when the balance is ₹0.00.</span>
                    </div>
                  )}
                </div>
              );
            })}
          </section>
        )}

        {/* Confirmation / Information Modal */}
        {deletingId && (() => {
          const deletingAccount = accounts.find((a) => a.accountId === deletingId);
          if (!deletingAccount) return null;
          const isZeroBalance = Number(deletingAccount.balance ?? 0) === 0;

          return (
            <div className={styles.modalOverlay} role="dialog" aria-modal="true">
              <div className={styles.modalCard}>
                {isZeroBalance ? (
                  <>
                    <div className={styles.modalWarnIcon}><AlertTriangle size={28} /></div>
                    <h3 className={styles.modalTitle}>Delete Account?</h3>
                    <div className={styles.modalAccountMeta}>
                      <p><span>Account Number:</span> <strong>{deletingAccount.accountNumber}</strong></p>
                      <p><span>Balance:</span> <strong>{formatCurrency(deletingAccount.balance)}</strong></p>
                    </div>
                    <p className={styles.modalBody}>
                      Are you sure you want to deactivate this bank account? The account status will be set to inactive in compliance with banking retention policies.
                    </p>
                    <div className={styles.modalActions}>
                      <button
                        onClick={() => setDeletingId(null)}
                        className={styles.cancelBtn}
                        disabled={isDeleting}
                      >
                        Cancel
                      </button>
                      <button
                        onClick={confirmDelete}
                        className={styles.confirmDeleteBtn}
                        disabled={isDeleting}
                      >
                        {isDeleting ? <Loader2 size={16} className={styles.spinner} /> : <Trash2 size={16} />}
                        Delete
                      </button>
                    </div>
                  </>
                ) : (
                  <>
                    <div className={styles.modalBlockIcon}><AlertCircle size={28} /></div>
                    <h3 className={styles.modalTitle}>Account cannot be deleted.</h3>
                    <div className={styles.modalAccountMeta}>
                      <p><span>Account Number:</span> <strong>{deletingAccount.accountNumber}</strong></p>
                      <p><span>Current Balance:</span> <strong>{formatCurrency(deletingAccount.balance)}</strong></p>
                    </div>
                    <p className={styles.modalBody}>
                      Please withdraw or otherwise bring the balance to ₹0.00 before deleting the account.
                    </p>
                    <div className={styles.modalActions}>
                      <button
                        onClick={() => setDeletingId(null)}
                        className={styles.cancelBtn}
                      >
                        Close
                      </button>
                    </div>
                  </>
                )}
              </div>
            </div>
          );
        })()}

    </div>
  );
}
