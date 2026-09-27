import { useState, useEffect, useCallback } from 'react';
import { useSearchParams, Link, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard, LogOut, Building2, ShieldCheck,
  CreditCard, ArrowLeft, RefreshCw, Loader2, AlertCircle,
  ArrowDownRight, ArrowUpRight, ArrowRightLeft, History, Filter,
  Calendar, RotateCcw, Search, CheckCircle2
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import accountService from '../services/accountService';
import transactionService from '../services/transactionService';
import styles from './TransactionHistoryPage.module.css';

export default function TransactionHistoryPage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  // Accounts state
  const [accounts, setAccounts] = useState([]);
  const [selectedAccountId, setSelectedAccountId] = useState(searchParams.get('accountId') || '');
  const [loadingAccounts, setLoadingAccounts] = useState(true);

  // Filter state
  const [activeTab, setActiveTab] = useState('all'); // 'all' | 'date' | 'lastN'
  const [fromDate, setFromDate] = useState('');
  const [toDate, setToDate] = useState('');
  const [lastNOption, setLastNOption] = useState(5);

  // Transaction records state
  const [transactions, setTransactions] = useState([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loadingTxns, setLoadingTxns] = useState(false);
  const [error, setError] = useState('');
  const [dateError, setDateError] = useState('');

  // Initial load: Accounts
  useEffect(() => {
    loadAccounts();
  }, []);

  const loadAccounts = async () => {
    setLoadingAccounts(true);
    setError('');
    try {
      const data = await accountService.getAccounts();
      const active = (data || []).filter((a) => a.isActive !== false);
      setAccounts(active);

      // If URL has accountId and it's valid, use it; otherwise pick first account
      const urlAccountId = searchParams.get('accountId');
      if (urlAccountId && active.some((a) => a.accountId === urlAccountId)) {
        setSelectedAccountId(urlAccountId);
      } else if (active.length > 0) {
        setSelectedAccountId(active[0].accountId);
      }
    } catch (err) {
      console.error('Failed to load accounts:', err);
      setError('Unable to load accounts. Please try again.');
    } finally {
      setLoadingAccounts(false);
    }
  };

  // Selected account object
  const selectedAccount = accounts.find((a) => a.accountId === selectedAccountId);

  // Load transactions based on current filters
  const fetchTransactions = useCallback(async (overrides = {}) => {
    if (!selectedAccountId) return;

    setLoadingTxns(true);
    setError('');
    setDateError('');

    const params = {
      accountId: selectedAccountId,
      pageSize: 50,
      ...overrides
    };

    try {
      const result = await transactionService.getTransactions(params);
      setTransactions(result.items || []);
      setTotalCount(result.totalCount || 0);
    } catch (err) {
      console.error('Failed to fetch transactions:', err);
      if (err.response?.status === 403) {
        setError('You are not authorized to view transactions for this account.');
      } else {
        setError(err.response?.data?.message || 'Failed to fetch transaction history.');
      }
      setTransactions([]);
      setTotalCount(0);
    } finally {
      setLoadingTxns(false);
    }
  }, [selectedAccountId]);

  // Fetch transactions whenever selected account changes
  useEffect(() => {
    if (selectedAccountId) {
      // Update URL query param quietly
      setSearchParams({ accountId: selectedAccountId }, { replace: true });
      fetchTransactions();
    }
  }, [selectedAccountId, fetchTransactions, setSearchParams]);

  const handleAccountChange = (e) => {
    setSelectedAccountId(e.target.value);
    // Reset date/lastN filters on account switch
    setFromDate('');
    setToDate('');
    setDateError('');
    setActiveTab('all');
  };

  // Option 1: Date Range Submit
  const handleDateSearch = (e) => {
    e.preventDefault();
    setDateError('');
    setError('');

    if (fromDate && toDate && fromDate > toDate) {
      setDateError('From date cannot be after To date.');
      return;
    }

    setActiveTab('date');
    fetchTransactions({
      fromDate: fromDate || undefined,
      toDate: toDate ? `${toDate}T23:59:59` : undefined,
      lastN: undefined
    });
  };

  // Option 2: Last N Submit
  const handleLastNSearch = (e) => {
    e.preventDefault();
    setDateError('');
    setError('');
    setActiveTab('lastN');

    fetchTransactions({
      lastN: Number(lastNOption),
      fromDate: undefined,
      toDate: undefined
    });
  };

  // Clear Filters
  const handleClearFilters = () => {
    setFromDate('');
    setToDate('');
    setLastNOption(5);
    setDateError('');
    setError('');
    setActiveTab('all');
    fetchTransactions({
      fromDate: undefined,
      toDate: undefined,
      lastN: undefined
    });
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

  const formatCurrency = (val) => {
    const num = Number(val ?? 0);
    return `₹${num.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  const formatDate = (isoStr) => {
    if (!isoStr) return '-';
    const dt = new Date(isoStr);
    return dt.toLocaleDateString('en-GB', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  };

  // Determine Debit / Credit direction and styling
  const getTxnDirectionDetails = (txn) => {
    const type = txn.transactionType;
    const mode = txn.transactionMode;
    const ref = txn.referenceNumber || '';

    if (type === 'Deposit') {
      return {
        direction: 'Credit',
        badgeClass: styles.creditBadge,
        sign: '+',
        amountClass: styles.creditAmount,
        typeLabel: 'Deposit (Cash)'
      };
    } else if (type === 'Withdrawal') {
      return {
        direction: 'Debit',
        badgeClass: styles.debitBadge,
        sign: '-',
        amountClass: styles.debitAmount,
        typeLabel: 'Withdrawal (Cash)'
      };
    } else if (type === 'Transfer') {
      if (ref.endsWith('-DST')) {
        return {
          direction: 'Credit / Incoming',
          badgeClass: styles.creditBadge,
          sign: '+',
          amountClass: styles.creditAmount,
          typeLabel: 'Cheque Transfer (Credit)'
        };
      } else {
        return {
          direction: 'Debit / Outgoing',
          badgeClass: styles.debitBadge,
          sign: '-',
          amountClass: styles.debitAmount,
          typeLabel: 'Cheque Transfer (Debit)'
        };
      }
    }

    return {
      direction: 'Unknown',
      badgeClass: styles.neutralBadge,
      sign: '',
      amountClass: '',
      typeLabel: `${type} (${mode})`
    };
  };

  return (
    <div className={styles.pageContent}>

        <div className={styles.header}>
          <div>
            <h1 className={styles.pageTitle}>Transaction History</h1>
            <p className={styles.pageSubtitle}>
              View and filter historical transactions by date range or last N transactions
            </p>
          </div>
          <button
            onClick={() => fetchTransactions()}
            className={styles.refreshBtn}
            disabled={loadingTxns}
            title="Refresh transaction history"
          >
            <RefreshCw size={15} className={loadingTxns ? styles.spinner : ''} />
            {loadingTxns ? 'Refreshing...' : 'Refresh'}
          </button>
        </div>

        {/* Account Selector & Summary Card */}
        <section className={styles.accountSelectorCard}>
          <div className={styles.selectorHeader}>
            <label htmlFor="accountSelect" className={styles.selectLabel}>
              <CreditCard size={16} /> Select Account
            </label>
            {loadingAccounts ? (
              <span className={styles.loadingText}>Loading your accounts...</span>
            ) : (
              <select
                id="accountSelect"
                value={selectedAccountId}
                onChange={handleAccountChange}
                className={styles.accountSelect}
              >
                {accounts.length === 0 ? (
                  <option value="">No accounts available</option>
                ) : (
                  accounts.map((acc) => (
                    <option key={acc.accountId} value={acc.accountId}>
                      {acc.accountNumber} ({acc.accountType === 1 || acc.accountType === 'Savings' ? 'Savings' : 'Checking'}) — {formatCurrency(acc.balance)}
                    </option>
                  ))
                )}
              </select>
            )}
          </div>

          {/* Account Summary Banner */}
          {selectedAccount && (
            <div className={styles.accountSummaryGrid}>
              <div className={styles.summaryItem}>
                <span className={styles.summaryLabel}>Account Number</span>
                <span className={styles.summaryValue}>{selectedAccount.accountNumber}</span>
              </div>
              <div className={styles.summaryItem}>
                <span className={styles.summaryLabel}>Account Type</span>
                <span className={styles.summaryValue}>
                  {selectedAccount.accountType === 1 || selectedAccount.accountType === 'Savings' ? 'Savings' : 'Checking'}
                </span>
              </div>
              <div className={styles.summaryItem}>
                <span className={styles.summaryLabel}>Current Balance</span>
                <span className={`${styles.summaryValue} ${styles.balanceText}`}>
                  {formatCurrency(selectedAccount.balance)}
                </span>
              </div>
            </div>
          )}
        </section>

        {/* Filter Controls Section */}
        <section className={styles.filterSection}>
          <div className={styles.filterHeader}>
            <Filter size={16} />
            <span>Filter Transactions</span>
          </div>

          <div className={styles.optionsContainer}>

            {/* OPTION 1 — DATE RANGE */}
            <form onSubmit={handleDateSearch} className={styles.optionBox}>
              <h3 className={styles.optionTitle}>Option 1: Date Range</h3>
              <div className={styles.dateInputsRow}>
                <div className={styles.inputGroup}>
                  <label htmlFor="fromDate" className={styles.inputLabel}>From Date</label>
                  <input
                    type="date"
                    id="fromDate"
                    value={fromDate}
                    onChange={(e) => setFromDate(e.target.value)}
                    className={styles.dateInput}
                  />
                </div>

                <div className={styles.inputGroup}>
                  <label htmlFor="toDate" className={styles.inputLabel}>To Date</label>
                  <input
                    type="date"
                    id="toDate"
                    value={toDate}
                    onChange={(e) => setToDate(e.target.value)}
                    className={styles.dateInput}
                  />
                </div>
              </div>

              <button type="submit" className={styles.searchBtn} disabled={loadingTxns}>
                <Search size={15} /> Search Transactions
              </button>
            </form>

            {/* OPTION 2 — LAST N TRANSACTIONS */}
            <form onSubmit={handleLastNSearch} className={styles.optionBox}>
              <h3 className={styles.optionTitle}>Option 2: Last N Transactions</h3>
              <div className={styles.inputGroup}>
                <label htmlFor="lastNSelect" className={styles.inputLabel}>Show Recent</label>
                <select
                  id="lastNSelect"
                  value={lastNOption}
                  onChange={(e) => setLastNOption(Number(e.target.value))}
                  className={styles.lastNSelect}
                >
                  <option value={5}>5 transactions</option>
                  <option value={10}>10 transactions</option>
                  <option value={20}>20 transactions</option>
                  <option value={50}>50 transactions</option>
                  <option value={100}>100 transactions</option>
                </select>
              </div>

              <button type="submit" className={styles.searchBtn} disabled={loadingTxns}>
                <History size={15} /> View Transactions
              </button>
            </form>

          </div>

          {/* Date Validation or Error Banners */}
          {dateError && (
            <div className={styles.validationError}>
              <AlertCircle size={15} /> {dateError}
            </div>
          )}

          {/* Reset / Clear Filters */}
          <div className={styles.filterActionsRow}>
            <button onClick={handleClearFilters} className={styles.clearBtn} type="button">
              <RotateCcw size={14} /> Clear Filters
            </button>
            {activeTab !== 'all' && (
              <span className={styles.activeFilterBadge}>
                Active Filter: {activeTab === 'date' ? 'Date Range' : `Last ${lastNOption}`}
              </span>
            )}
          </div>
        </section>

        {/* Global Error Banner */}
        {error && (
          <div className={styles.errorBanner} role="alert">
            <AlertCircle size={16} />
            <span>{error}</span>
          </div>
        )}

        {/* Transactions Table Section */}
        <section className={styles.tableSection}>
          <div className={styles.tableHeaderRow}>
            <h2 className={styles.tableTitle}>Transaction History</h2>
            <span className={styles.recordCount}>{totalCount} {totalCount === 1 ? 'record' : 'records'} found</span>
          </div>

          {loadingTxns ? (
            <div className={styles.loadingContainer}>
              <Loader2 size={32} className={styles.spinner} />
              <p>Fetching transaction records from backend...</p>
            </div>
          ) : transactions.length === 0 ? (
            <div className={styles.emptyState}>
              <History size={36} className={styles.emptyIcon} />
              <h3 className={styles.emptyTitle}>No Transactions Found</h3>
              <p className={styles.emptyDesc}>
                There are no transaction records matching your current filter criteria.
              </p>
            </div>
          ) : (
            <div className={styles.tableResponsive}>
              <table className={styles.txnTable}>
                <thead>
                  <tr>
                    <th>Date & Time</th>
                    <th>Type</th>
                    <th>Debit / Credit</th>
                    <th>Amount</th>
                    <th>Reference Number</th>
                    <th>Description</th>
                  </tr>
                </thead>
                <tbody>
                  {transactions.map((txn) => {
                    const dt = getTxnDirectionDetails(txn);
                    return (
                      <tr key={txn.transactionId}>
                        <td className={styles.dateCell}>{formatDate(txn.transactionDate)}</td>
                        <td>
                          <span className={styles.typeText}>{dt.typeLabel}</span>
                        </td>
                        <td>
                          <span className={`${styles.directionBadge} ${dt.badgeClass}`}>
                            {dt.direction}
                          </span>
                        </td>
                        <td className={`${styles.amountCell} ${dt.amountClass}`}>
                          {dt.sign}{formatCurrency(txn.amount)}
                        </td>
                        <td className={styles.refCell}>{txn.referenceNumber}</td>
                        <td className={styles.descCell}>{txn.description || 'N/A'}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </section>

    </div>
  );
}
