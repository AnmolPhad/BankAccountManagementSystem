import {
  TrendingUp, CreditCard, ArrowUpRight, Activity, ShieldCheck, User
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import BankingNavigation from '../components/BankingNavigation';
import styles from './DashboardPage.module.css';

// ── Stat card component ────────────────────────────────────────────────────────
function StatCard({ icon: Icon, label, value, sub, accent }) {
  return (
    <div className={styles.statCard} style={{ '--accent': accent }}>
      <div className={styles.statIcon}>
        <Icon size={20} />
      </div>
      <div className={styles.statBody}>
        <p className={styles.statLabel}>{label}</p>
        <p className={styles.statValue}>{value}</p>
        {sub && <p className={styles.statSub}>{sub}</p>}
      </div>
      <ArrowUpRight size={14} className={styles.statArrow} />
    </div>
  );
}

export default function DashboardPage() {
  const { user } = useAuth();

  const displayName = user
    ? `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim() || user.email
    : 'User';

  return (
    <div className={styles.pageContent}>

      {/* Header Banner */}
      <div className={styles.welcomeBanner}>
        <h2 className={styles.welcomeTitle}>Dashboard Overview</h2>
        <p className={styles.welcomeSubtitle}>
          Welcome back, <strong>{displayName}</strong>. Your banking session is active and secure.
        </p>
      </div>

      {/* ── Banking Services Navigation (EXCLUSIVELY ON DASHBOARD) ──────────── */}
      <BankingNavigation />

      {/* Stats row */}
      <section className={styles.statsGrid}>
        <StatCard
          icon={CreditCard}
          label="Total Accounts"
          value="Active"
          sub="Savings & Checking"
          accent="#3b82f6"
        />
        <StatCard
          icon={TrendingUp}
          label="Transactions Today"
          value="Operational"
          sub="Cash & Cheque"
          accent="#10b981"
        />
        <StatCard
          icon={Activity}
          label="Active Session"
          value="Authenticated"
          sub="JWT Protected"
          accent="#8b5cf6"
        />
        <StatCard
          icon={ShieldCheck}
          label="System Status"
          value="Healthy"
          sub="All services operational"
          accent="#f59e0b"
        />
      </section>

      {/* Auth confirmation panel */}
      <section className={styles.authPanel}>
        <div className={styles.authPanelIcon}>
          <ShieldCheck size={32} />
        </div>
        <div>
          <h2 className={styles.authPanelTitle}>Authentication Verified</h2>
          <p className={styles.authPanelBody}>
            You are securely authenticated with a JWT Bearer token. Your session
            will automatically expire after 60 minutes of inactivity, and you
            will be redirected to the login page if your token is revoked.
          </p>
          <div className={styles.authMeta}>
            <span className={styles.pill}>
              <User size={11} /> {user?.email}
            </span>
            <span className={styles.pill}>
              <ShieldCheck size={11} /> {user?.role}
            </span>
          </div>
        </div>
      </section>

    </div>
  );
}
