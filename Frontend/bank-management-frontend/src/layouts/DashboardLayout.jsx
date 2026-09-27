import { Outlet, NavLink, useNavigate, useLocation } from 'react-router-dom';
import {
  LayoutDashboard, LogOut, Building2, ShieldCheck,
  CreditCard, ArrowRightLeft, History, Database, Bell
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import styles from './DashboardLayout.module.css';

export default function DashboardLayout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

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

  const pathname = location.pathname;

  // Active state matching rules:
  // 1. Dashboard: active ONLY on /dashboard
  const isDashboardActive = pathname === '/dashboard';

  // 2. Accounts: active on /accounts, /accounts/add, /accounts/:id (BUT NOT /accounts/:id/history)
  const isAccountsActive =
    (pathname === '/accounts' || pathname.startsWith('/accounts/')) &&
    !pathname.endsWith('/history');

  // 3. Transactions: active on /transactions, /deposit, /withdraw, /transfer (BUT NOT /transactions/history)
  const isTransactionsActive =
    (pathname === '/transactions' ||
     pathname === '/deposit' ||
     pathname === '/withdraw' ||
     pathname === '/transfer') &&
    pathname !== '/transactions/history';

  // 4. Transaction History: active on /transactions/history OR any /history route (like /accounts/:id/history)
  const isTransactionHistoryActive =
    pathname === '/transactions/history' || pathname.endsWith('/history');

  // 5. Save & Restore: active ONLY on /backup-restore
  const isBackupRestoreActive = pathname === '/backup-restore';

  return (
    <div className={styles.root}>

      {/* ── Sidebar (Persistent Left Navigation) ────────────────────────── */}
      <aside className={styles.sidebar}>
        <div className={styles.sidebarLogo}>
          <div className={styles.logoIcon}><Building2 size={22} /></div>
          <span className={styles.logoText}>NexaBank</span>
        </div>

        <nav className={styles.nav}>
          <NavLink
            to="/dashboard"
            className={`${styles.navItem} ${isDashboardActive ? styles.navActive : ''}`}
          >
            <LayoutDashboard size={18} />
            <span>Dashboard</span>
          </NavLink>

          <NavLink
            to="/accounts"
            className={`${styles.navItem} ${isAccountsActive ? styles.navActive : ''}`}
          >
            <CreditCard size={18} />
            <span>Accounts</span>
          </NavLink>

          <NavLink
            to="/transactions"
            className={`${styles.navItem} ${isTransactionsActive ? styles.navActive : ''}`}
          >
            <ArrowRightLeft size={18} />
            <span>Transactions</span>
          </NavLink>

          <NavLink
            to="/transactions/history"
            className={`${styles.navItem} ${isTransactionHistoryActive ? styles.navActive : ''}`}
          >
            <History size={18} />
            <span>Transaction History</span>
          </NavLink>

          <NavLink
            to="/backup-restore"
            className={`${styles.navItem} ${isBackupRestoreActive ? styles.navActive : ''}`}
          >
            <Database size={18} />
            <span>Save & Restore</span>
          </NavLink>
        </nav>

        <div className={styles.sidebarFooter}>
          <div
            className={styles.userChip}
            onClick={() => navigate('/profile')}
            role="button"
            tabIndex={0}
            title="View Profile"
          >
            <div className={styles.avatar}>{initials}</div>
            <div className={styles.userInfo}>
              <p className={styles.userName}>{displayName}</p>
              <p className={styles.userRole}>
                <ShieldCheck size={11} />
                {user?.role ?? 'User'}
              </p>
            </div>
          </div>
          <button
            onClick={(e) => {
              e.stopPropagation();
              handleLogout();
            }}
            className={styles.logoutBtn}
            title="Sign out"
          >
            <LogOut size={16} />
          </button>
        </div>
      </aside>

      {/* ── Main Content Area ────────────────────────────────────────────── */}
      <main className={styles.main}>

        {/* Top bar Header */}
        <header className={styles.topbar}>
          <div>
            <h1 className={styles.pageTitle}>NexaBank Management System</h1>
            <p className={styles.pageSubtitle}>
              Welcome back, <strong>{displayName}</strong> ({user?.role || 'User'}).
            </p>
          </div>
          <div className={styles.topbarRight}>
            <div className={styles.userBadge}>
              <ShieldCheck size={14} />
              <span>{user?.email}</span>
            </div>
            <button className={styles.iconBtn} aria-label="Notifications" title="System Notifications">
              <Bell size={18} />
              <span className={styles.badgeDot} />
            </button>
          </div>
        </header>

        {/* ── Page Content (Outlet for active route) ───────────────────────── */}
        <div className={styles.contentContainer}>
          <Outlet />
        </div>

      </main>
    </div>
  );
}
