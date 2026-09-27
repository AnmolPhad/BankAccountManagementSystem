import { Outlet, NavLink, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard, LogOut, Building2, ShieldCheck,
  CreditCard, ArrowRightLeft, History, Database, Bell
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import styles from './DashboardLayout.module.css';

export default function DashboardLayout() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

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

      {/* ── Sidebar (Persistent Left Navigation) ────────────────────────── */}
      <aside className={styles.sidebar}>
        <div className={styles.sidebarLogo}>
          <div className={styles.logoIcon}><Building2 size={22} /></div>
          <span className={styles.logoText}>NexaBank</span>
        </div>

        <nav className={styles.nav}>
          <NavLink
            to="/dashboard"
            className={({ isActive }) =>
              `${styles.navItem} ${isActive ? styles.navActive : ''}`
            }
          >
            <LayoutDashboard size={18} />
            <span>Dashboard</span>
          </NavLink>

          <NavLink
            to="/accounts"
            className={({ isActive }) =>
              `${styles.navItem} ${isActive ? styles.navActive : ''}`
            }
          >
            <CreditCard size={18} />
            <span>Accounts</span>
          </NavLink>

          <NavLink
            to="/transactions"
            className={({ isActive }) =>
              `${styles.navItem} ${isActive ? styles.navActive : ''}`
            }
          >
            <ArrowRightLeft size={18} />
            <span>Transactions</span>
          </NavLink>

          <NavLink
            to="/transactions/history"
            className={({ isActive }) =>
              `${styles.navItem} ${isActive ? styles.navActive : ''}`
            }
          >
            <History size={18} />
            <span>Transaction History</span>
          </NavLink>

          <NavLink
            to="/backup-restore"
            className={({ isActive }) =>
              `${styles.navItem} ${isActive ? styles.navActive : ''}`
            }
          >
            <Database size={18} />
            <span>Save & Restore</span>
          </NavLink>
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
          <button
            onClick={handleLogout}
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
