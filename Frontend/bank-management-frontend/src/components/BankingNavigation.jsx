import { NavLink } from 'react-router-dom';
import {
  CreditCard, ArrowRightLeft, History, Database, ArrowRight
} from 'lucide-react';
import styles from './BankingNavigation.module.css';

const bankingServices = [
  {
    id: 'accounts',
    title: 'Accounts',
    description: 'Manage Savings & Checking accounts',
    path: '/accounts',
    icon: CreditCard,
    accent: '#3b82f6',
    badge: 'Core'
  },
  {
    id: 'transactions',
    title: 'Transactions',
    description: 'Deposit, withdraw, or transfer funds',
    path: '/transactions',
    icon: ArrowRightLeft,
    accent: '#10b981',
    badge: 'Operations'
  },
  {
    id: 'history',
    title: 'Transaction History',
    description: 'Query history by date range or last N',
    path: '/transactions/history',
    icon: History,
    accent: '#8b5cf6',
    badge: 'Audit'
  },
  {
    id: 'backup-restore',
    title: 'Save & Restore',
    description: 'Export system backup JSON or restore data',
    path: '/backup-restore',
    icon: Database,
    accent: '#f59e0b',
    badge: 'Admin'
  }
];

export default function BankingNavigation() {
  return (
    <section className={styles.navSection}>
      <div className={styles.sectionHeader}>
        <h2 className={styles.sectionTitle}>Banking Services</h2>
        <span className={styles.sectionSubtitle}>Quick Navigation</span>
      </div>

      <div className={styles.servicesGrid}>
        {bankingServices.map((service) => {
          const IconComp = service.icon;
          return (
            <NavLink
              key={service.id}
              to={service.path}
              className={({ isActive }) =>
                `${styles.serviceCard} ${isActive ? styles.serviceCardActive : ''}`
              }
              style={{ '--accent-color': service.accent }}
            >
              <div className={styles.cardHeader}>
                <div className={styles.iconBox}>
                  <IconComp size={20} />
                </div>
                <span className={styles.badge}>{service.badge}</span>
              </div>

              <div className={styles.cardBody}>
                <h3 className={styles.cardTitle}>{service.title}</h3>
                <p className={styles.cardDesc}>{service.description}</p>
              </div>

              <div className={styles.cardFooter}>
                <span className={styles.cardActionText}>Open {service.title}</span>
                <ArrowRight size={14} className={styles.arrowIcon} />
              </div>
            </NavLink>
          );
        })}
      </div>
    </section>
  );
}
