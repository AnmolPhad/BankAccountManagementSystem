import { Link } from 'react-router-dom';
import {
  ArrowDownRight, ArrowUpRight, ArrowRightLeft, ArrowRight
} from 'lucide-react';
import styles from './TransactionsPage.module.css';

export default function TransactionsPage() {
  return (
    <div className={styles.pageContent}>
      <div className={styles.pageHeader}>
        <h1 className={styles.pageTitle}>Perform Transactions</h1>
        <p className={styles.pageSubtitle}>
          Select a cash deposit, cash withdrawal, or cheque transfer operation.
        </p>
      </div>

      {/* Action Cards */}
      <section className={styles.actionGrid}>

        {/* Deposit */}
        <Link to="/deposit" className={styles.actionCard}>
          <div className={styles.cardTop}>
            <div className={`${styles.cardIcon} ${styles.depositIcon}`}>
              <ArrowDownRight size={24} />
            </div>
            <span className={`${styles.badge} ${styles.badgeCash}`}>Cash Only</span>
          </div>
          <div>
            <h3 className={styles.cardTitle}>Cash Deposit</h3>
            <p className={styles.cardDesc}>
              Deposit cash into your Savings or Checking account. Balance updates instantly.
            </p>
          </div>
          <div className={styles.cardFooter}>
            <span>Make Deposit</span>
            <ArrowRight size={16} />
          </div>
        </Link>

        {/* Withdrawal */}
        <Link to="/withdraw" className={styles.actionCard}>
          <div className={styles.cardTop}>
            <div className={`${styles.cardIcon} ${styles.withdrawIcon}`}>
              <ArrowUpRight size={24} />
            </div>
            <span className={`${styles.badge} ${styles.badgeCash}`}>Cash Only</span>
          </div>
          <div>
            <h3 className={styles.cardTitle}>Cash Withdrawal</h3>
            <p className={styles.cardDesc}>
              Withdraw cash from your Savings or Checking account up to your available balance.
            </p>
          </div>
          <div className={styles.cardFooter}>
            <span>Make Withdrawal</span>
            <ArrowRight size={16} />
          </div>
        </Link>

        {/* Cheque Transfer */}
        <Link to="/transfer" className={styles.actionCard}>
          <div className={styles.cardTop}>
            <div className={`${styles.cardIcon} ${styles.transferIcon}`}>
              <ArrowRightLeft size={24} />
            </div>
            <span className={`${styles.badge} ${styles.badgeCheque}`}>Cheque / Checking Only</span>
          </div>
          <div>
            <h3 className={styles.cardTitle}>Cheque Transfer</h3>
            <p className={styles.cardDesc}>
              Transfer funds from a Checking account to any destination account using cheque mode.
            </p>
          </div>
          <div className={styles.cardFooter}>
            <span>Initiate Transfer</span>
            <ArrowRight size={16} />
          </div>
        </Link>

      </section>
    </div>
  );
}
