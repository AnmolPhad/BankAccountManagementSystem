import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  User, Mail, ShieldCheck, CreditCard, ArrowLeft,
  IdCard, Phone, MapPin, Layers, Check, XCircle, RefreshCw, Loader2
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import accountService from '../services/accountService';
import userService from '../services/userService';
import styles from './ProfilePage.module.css';

export default function ProfilePage() {
  const { user } = useAuth();
  const navigate = useNavigate();

  const [profileData, setProfileData] = useState(null);
  const [accounts, setAccounts] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    setError('');
    try {
      // 1. Fetch account information from backend
      const accList = await accountService.getAccounts();
      setAccounts(accList || []);

      // 2. Fetch full user profile details if ID is available
      if (user?.id) {
        const uData = await userService.getUserById(user.id);
        if (uData) {
          setProfileData(uData);
        }
      }
    } catch (err) {
      console.error('Error loading profile or accounts:', err);
      setError('Unable to load account data from backend.');
    } finally {
      setLoading(false);
    }
  };

  const formatCurrency = (val) => {
    const num = Number(val ?? 0);
    return `₹${num.toLocaleString('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  };

  // Derive real values from AuthContext or fetched profile data
  const firstName = profileData?.firstName || user?.firstName || 'Saurabh';
  const lastName = profileData?.lastName || user?.lastName || 'Phad';
  const fullName = `${firstName} ${lastName}`.trim();
  const email = profileData?.email || user?.email || 'N/A';
  const phone = profileData?.phoneNumber || profileData?.phone || user?.phoneNumber || user?.phone || 'N/A';
  const address = profileData?.address || user?.address || 'N/A';
  const customerId = profileData?.employeeCode || user?.employeeCode || user?.id || 'CUST-00001';
  const role = profileData?.role || user?.role || 'Customer';

  const initials = `${firstName?.[0] || ''}${lastName?.[0] || ''}`.toUpperCase() || 'SP';

  return (
    <div className={styles.pageContainer}>

      {/* ── Page Header & Navigation ─────────────────────────────────────── */}
      <div className={styles.topHeaderRow}>
        <div>
          <h1 className={styles.pageTitle}>Profile</h1>
          <p className={styles.pageSubtitle}>Manage your personal banking information</p>
        </div>

        <button
          onClick={() => navigate('/dashboard')}
          className={styles.backBtn}
          title="Return to Dashboard"
        >
          <ArrowLeft size={16} />
          <span>Back to Dashboard</span>
        </button>
      </div>

      {error && (
        <div className={styles.errorBanner} role="alert">
          <span>{error}</span>
          <button onClick={loadData} className={styles.retryBtn}>
            <RefreshCw size={14} /> Retry
          </button>
        </div>
      )}

      {loading ? (
        <div className={styles.loadingBox}>
          <Loader2 size={32} className={styles.spinner} />
          <p>Loading profile information...</p>
        </div>
      ) : (
        <div className={styles.profileLayout}>

          {/* ── 1. PROFILE CARD ────────────────────────────────────────────── */}
          <section className={styles.cardSection}>
            <div className={styles.profileCardContent}>
              <div className={styles.avatarCircle}>
                <span>{initials}</span>
              </div>
              <h2 className={styles.profileCardName}>{fullName}</h2>
              <span className={styles.roleBadge}>
                <ShieldCheck size={14} />
                {role}
              </span>
            </div>
          </section>

          {/* ── 2. PERSONAL INFORMATION ────────────────────────────────────── */}
          <section className={styles.cardSection}>
            <div className={styles.sectionHeader}>
              <User size={18} className={styles.sectionHeaderIcon} />
              <h3 className={styles.sectionTitle}>PERSONAL INFORMATION</h3>
            </div>

            <div className={styles.infoGrid}>
              <div className={styles.infoRow}>
                <span className={styles.infoLabel}>First Name</span>
                <span className={styles.infoValue}>{firstName}</span>
              </div>

              <div className={styles.infoRow}>
                <span className={styles.infoLabel}>Last Name</span>
                <span className={styles.infoValue}>{lastName}</span>
              </div>

              <div className={styles.infoRow}>
                <span className={styles.infoLabel}>Email</span>
                <span className={styles.infoValue}>
                  <Mail size={14} className={styles.inlineIcon} />
                  {email}
                </span>
              </div>

              <div className={styles.infoRow}>
                <span className={styles.infoLabel}>Phone</span>
                <span className={styles.infoValue}>
                  <Phone size={14} className={styles.inlineIcon} />
                  {phone}
                </span>
              </div>

              <div className={styles.infoRow}>
                <span className={styles.infoLabel}>Address</span>
                <span className={styles.infoValue}>
                  <MapPin size={14} className={styles.inlineIcon} />
                  {address}
                </span>
              </div>
            </div>
          </section>

          {/* ── 3. CUSTOMER INFORMATION (READ-ONLY) ────────────────────────── */}
          <section className={styles.cardSection}>
            <div className={styles.sectionHeader}>
              <IdCard size={18} className={styles.sectionHeaderIcon} />
              <h3 className={styles.sectionTitle}>CUSTOMER INFORMATION</h3>
            </div>

            <div className={styles.infoGrid}>
              <div className={styles.infoRow}>
                <span className={styles.infoLabel}>Customer ID</span>
                <div className={styles.readOnlyValueWrapper}>
                  <span className={styles.readOnlyText}>{customerId}</span>
                  <span className={styles.readOnlyTag}>READ-ONLY</span>
                </div>
              </div>

              <div className={styles.infoRow}>
                <span className={styles.infoLabel}>Account Count</span>
                <div className={styles.readOnlyValueWrapper}>
                  <span className={styles.readOnlyText}>{accounts.length}</span>
                  <span className={styles.readOnlyTag}>READ-ONLY</span>
                </div>
              </div>
            </div>
          </section>

          {/* ── 4. YOUR ACCOUNTS (READ-ONLY) ───────────────────────────────── */}
          <section className={styles.cardSection}>
            <div className={styles.sectionHeaderBetween}>
              <div className={styles.sectionHeader}>
                <CreditCard size={18} className={styles.sectionHeaderIcon} />
                <h3 className={styles.sectionTitle}>YOUR ACCOUNTS</h3>
              </div>
              <span className={styles.readOnlySectionBadge}>READ-ONLY SYSTEM DATA</span>
            </div>

            {accounts.length === 0 ? (
              <div className={styles.emptyState}>
                <CreditCard size={28} />
                <p>No active accounts found for this customer profile.</p>
              </div>
            ) : (
              <div className={styles.tableWrapper}>
                <table className={styles.accountsTable}>
                  <thead>
                    <tr>
                      <th>Account Number</th>
                      <th>Type</th>
                      <th>Balance</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {accounts.map((acc) => {
                      const isSavings = acc.accountType === 'Savings' || acc.accountType === 1;
                      const typeName = isSavings ? 'Savings' : 'Checking';
                      const isActive = acc.isActive !== false;

                      return (
                        <tr key={acc.accountId}>
                          {/* Account Number displayed strictly as non-editable text */}
                          <td className={styles.accountNumberCell}>
                            <span className={styles.accountNumberText}>{acc.accountNumber}</span>
                          </td>

                          {/* Account Type - Read Only */}
                          <td>
                            <span className={`${styles.typeBadge} ${isSavings ? styles.savingsBadge : styles.checkingBadge}`}>
                              {typeName}
                            </span>
                          </td>

                          {/* Balance - Read Only */}
                          <td className={styles.balanceCell}>
                            {formatCurrency(acc.balance)}
                          </td>

                          {/* Account Status - Read Only */}
                          <td>
                            {isActive ? (
                              <span className={styles.activeBadge}>
                                <Check size={12} /> Active
                              </span>
                            ) : (
                              <span className={styles.inactiveBadge}>
                                <XCircle size={12} /> Inactive
                              </span>
                            )}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </section>

          {/* ── Bottom Action Row ────────────────────────────────────────── */}
          <div className={styles.bottomNavRow}>
            <button
              onClick={() => navigate('/dashboard')}
              className={styles.backBtnLarge}
            >
              <ArrowLeft size={16} />
              <span>Back to Dashboard</span>
            </button>
          </div>

        </div>
      )}

    </div>
  );
}
