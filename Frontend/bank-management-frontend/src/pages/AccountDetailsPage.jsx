import { useState, useEffect, useCallback } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard, LogOut, Building2, ShieldCheck,
  CreditCard, ArrowLeft, RefreshCw, Loader2, AlertCircle,
  ArrowDownRight, ArrowUpRight, ArrowRightLeft, History, CheckCircle2, XCircle
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import accountService from '../services/accountService';
import styles from './AccountDetailsPage.module.css';

export default function AccountDetailsPage() {
  const { id } = useParams();
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const [account, setAccount] = useState(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState('');

  const loadAccountDetails = useCallback(async (isRefresh = false) => {
    if (isRefresh) {
      setRefreshing(true);
    } else {
      setLoading(true);
    }
    setError('');

    try {
      const data = await accountService.getAccountById(id);
      setAccount(data);
    } catch (err) {
      console.error('Failed to fetch account details:', err);
      if (err.response?.status === 404) {
        setError('Account not found or has been removed.');
      } else if (err.response?.status === 403) {
        setError('You are not authorized to view this account.');
      } else {
        setError(err.response?.data?.message || 'Failed to load account details. Please try again.');
      }
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [id]);

  useEffect(() => {
    if (id) {
      loadAccountDetails();
    }
  }, [id, loadAccountDetails]);

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

  const formatCurrency = (val) => {
    const num = Number(val ?? 0);
    return `₹${num.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  const isSavings = account && (account.accountType === 'Savings' || account.accountType === 1);
  const typeName = isSavings ? 'Savings' : 'Checking';

  return (
    <div className={styles.pageContent}>

        <div className={styles.topRow}>
          <Link to="/accounts" className={styles.backBtn}>
            <ArrowLeft size={16} /> Back to Accounts
          </Link>
          {account && (
            <button
              onClick={() => loadAccountDetails(true)}
              className={styles.refreshBtn}
              disabled={refreshing}
              title="Refresh Balance from Server"
            >
              <RefreshCw size={15} className={refreshing ? styles.spinner : ''} />
              {refreshing ? 'Refreshing...' : 'Refresh Balance'}
            </button>
          )}
        </div>

        {error && (
          <div className={styles.errorBanner} role="alert">
            <AlertCircle size={18} />
            <span>{error}</span>
          </div>
        )}

        {loading ? (
          <div className={styles.loadingContainer}>
            <Loader2 size={36} className={styles.spinner} />
            <p>Fetching account details from backend...</p>
          </div>
        ) : account ? (
          <div className={styles.detailsWrapper}>

            {/* Main Details Card */}
            <div className={styles.cardHeader}>
              <div className={styles.cardTitleGroup}>
                <div className={`${styles.typeIcon} ${isSavings ? styles.savingsIcon : styles.checkingIcon}`}>
                  <CreditCard size={28} />
                </div>
                <div>
                  <h1 className={styles.cardTitle}>Account Details</h1>
                  <span className={`${styles.typeBadge} ${isSavings ? styles.savingsBadge : styles.checkingBadge}`}>
                    {typeName} Account
                  </span>
                </div>
              </div>
              <div className={styles.statusBadgeGroup}>
                {account.isActive !== false ? (
                  <span className={styles.activeBadge}>
                    <CheckCircle2 size={14} /> Active
                  </span>
                ) : (
                  <span className={styles.inactiveBadge}>
                    <XCircle size={14} /> Inactive
                  </span>
                )}
              </div>
            </div>

            {/* Grid Information */}
            <div className={styles.infoGrid}>
              <div className={styles.infoBlock}>
                <span className={styles.infoLabel}>Account Number</span>
                <span className={styles.infoValue}>{account.accountNumber}</span>
              </div>

              <div className={styles.infoBlock}>
                <span className={styles.infoLabel}>Account Type</span>
                <span className={styles.infoValue}>{typeName}</span>
              </div>

              <div className={`${styles.infoBlock} ${styles.balanceHighlight}`}>
                <span className={styles.infoLabel}>Current Balance</span>
                <span className={styles.balanceValue}>{formatCurrency(account.balance)}</span>
                <span className={styles.balanceSubtext}>Source of truth: NexaBank Core Ledger</span>
              </div>

              <div className={styles.infoBlock}>
                <span className={styles.infoLabel}>Status</span>
                <span className={styles.infoValue}>
                  {account.isActive !== false ? 'Active' : 'Deactivated'}
                </span>
              </div>
            </div>

            {/* Quick Actions */}
            <div className={styles.actionSection}>
              <h2 className={styles.sectionTitle}>Quick Actions</h2>
              <div className={styles.actionButtonsRow}>
                <Link
                  to={`/deposit?accountId=${account.accountId}`}
                  className={`${styles.actionBtn} ${styles.depositBtn}`}
                >
                  <ArrowDownRight size={18} />
                  <span>Deposit</span>
                </Link>

                <Link
                  to={`/withdraw?accountId=${account.accountId}`}
                  className={`${styles.actionBtn} ${styles.withdrawBtn}`}
                >
                  <ArrowUpRight size={18} />
                  <span>Withdraw</span>
                </Link>

                <Link
                  to={`/transactions/history?accountId=${account.accountId}`}
                  className={`${styles.actionBtn} ${styles.historyBtn}`}
                >
                  <History size={18} />
                  <span>Transactions</span>
                </Link>
              </div>
            </div>

          </div>
        ) : null}

    </div>
  );
}
