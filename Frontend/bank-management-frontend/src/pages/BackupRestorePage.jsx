import { useState, useRef } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard, LogOut, Building2, ShieldCheck,
  CreditCard, ArrowRightLeft, History, Database, Download,
  Upload, AlertTriangle, CheckCircle2, AlertCircle, Loader2, FileJson, X
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import systemService from '../services/systemService';
import styles from './BackupRestorePage.module.css';

export default function BackupRestorePage() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();
  const fileInputRef = useRef(null);

  // Backup (Save) State
  const [saving, setSaving] = useState(false);
  const [saveSuccessMsg, setSaveSuccessMsg] = useState('');
  const [saveError, setSaveError] = useState('');

  // Restore State
  const [selectedFile, setSelectedFile] = useState(null);
  const [fileContent, setFileContent] = useState(null);
  const [parsedBackup, setParsedBackup] = useState(null);
  const [restoring, setRestoring] = useState(false);
  const [restoreSuccessMsg, setRestoreSuccessMsg] = useState('');
  const [restoreError, setRestoreError] = useState('');
  const [showConfirmModal, setShowConfirmModal] = useState(false);

  // Authorization check
  const isAdmin = user?.role === 'Admin';

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

  // ─── 1. CREATE BACKUP (EXPORT) ──────────────────────────────────────────────
  const handleCreateBackup = async () => {
    setSaving(true);
    setSaveError('');
    setSaveSuccessMsg('');

    try {
      const backupData = await systemService.createBackup();

      // Format current date for filename
      const today = new Date().toISOString().slice(0, 10);
      const filename = `bank-account-backup-${today}.json`;

      // Convert JSON object to formatted string blob
      const jsonString = JSON.stringify(backupData, null, 2);
      const blob = new Blob([jsonString], { type: 'application/json;charset=utf-8;' });
      const url = URL.createObjectURL(blob);

      // Trigger browser download
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', filename);
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      URL.revokeObjectURL(url);

      const accountsCount = backupData?.accountsCount ?? backupData?.accounts?.length ?? 0;
      const txnCount = backupData?.transactionsCount ?? backupData?.transactions?.length ?? 0;

      setSaveSuccessMsg(
        `System backup exported successfully (${accountsCount} accounts, ${txnCount} transactions). Downloaded as '${filename}'.`
      );
    } catch (err) {
      console.error('Save backup error:', err);
      if (err.response?.status === 403) {
        setSaveError('Access denied: Administrator authorization is required to create a system backup.');
      } else {
        setSaveError(err.response?.data?.message || 'Failed to export system backup. Please try again.');
      }
    } finally {
      setSaving(false);
    }
  };

  // ─── 2. FILE SELECTION & JSON VALIDATION ────────────────────────────────────
  const handleFileChange = (e) => {
    const file = e.target.files?.[0];
    setRestoreError('');
    setRestoreSuccessMsg('');

    if (!file) {
      setSelectedFile(null);
      setFileContent(null);
      setParsedBackup(null);
      return;
    }

    if (!file.name.endsWith('.json') && file.type !== 'application/json') {
      setRestoreError('Invalid file extension: Please select a valid JSON (.json) backup file.');
      setSelectedFile(null);
      setFileContent(null);
      setParsedBackup(null);
      return;
    }

    setSelectedFile(file);

    const reader = new FileReader();
    reader.onload = (event) => {
      try {
        const text = event.target.result;
        if (!text || !text.trim()) {
          setRestoreError('Selected backup file is empty.');
          setParsedBackup(null);
          return;
        }

        const parsed = JSON.parse(text);

        // Basic structural validation
        if (typeof parsed !== 'object' || parsed === null) {
          setRestoreError('Invalid JSON structure: Root must be a valid JSON object.');
          setParsedBackup(null);
          return;
        }

        if (!Array.isArray(parsed.accounts) && !Array.isArray(parsed.Accounts)) {
          setRestoreError('Invalid backup payload: Missing accounts collection in backup JSON.');
          setParsedBackup(null);
          return;
        }

        setFileContent(text);
        setParsedBackup(parsed);
      } catch (parseErr) {
        console.error('JSON parse error:', parseErr);
        setRestoreError('Failed to parse file: Selected file is not valid JSON format.');
        setParsedBackup(null);
      }
    };

    reader.onerror = () => {
      setRestoreError('Error reading file from disk.');
      setParsedBackup(null);
    };

    reader.readAsText(file);
  };

  const handleClearSelectedFile = () => {
    setSelectedFile(null);
    setFileContent(null);
    setParsedBackup(null);
    setRestoreError('');
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  // ─── 3. TRIGGER RESTORE CONFIRMATION ────────────────────────────────────────
  const handleOpenRestoreModal = (e) => {
    e.preventDefault();
    setRestoreError('');
    setRestoreSuccessMsg('');

    if (!selectedFile || !parsedBackup) {
      setRestoreError('Please select a valid JSON backup file first.');
      return;
    }

    setShowConfirmModal(true);
  };

  // ─── 4. EXECUTE RESTORE API CALL ────────────────────────────────────────────
  const handleConfirmRestore = async () => {
    setShowConfirmModal(false);
    setRestoring(true);
    setRestoreError('');
    setRestoreSuccessMsg('');

    try {
      const response = await systemService.restoreBackup(parsedBackup);

      if (response && response.success === false) {
        setRestoreError(response.message || 'Restore failed.');
      } else {
        const msg = response?.message ||
          `System restore completed successfully! Restored ${response?.accountsRestored ?? 0} accounts and ${response?.transactionsRestored ?? 0} transactions.`;
        setRestoreSuccessMsg(msg);
        handleClearSelectedFile();
      }
    } catch (err) {
      console.error('Restore backup error:', err);
      if (err.response?.status === 403) {
        setRestoreError('Access denied: Administrator authorization is required to restore system backups.');
      } else if (err.response?.status === 400) {
        setRestoreError(err.response?.data?.message || 'Invalid backup payload structure or validation failure.');
      } else {
        setRestoreError(err.response?.data?.message || 'An error occurred while restoring system backup.');
      }
    } finally {
      setRestoring(false);
    }
  };

  return (
    <div className={styles.pageContent}>

        <div className={styles.pageHeader}>
          <div className={styles.titleGroup}>
            <div className={styles.headerIcon}><Database size={26} /></div>
            <div>
              <h1 className={styles.pageTitle}>System Save & Restore</h1>
              <p className={styles.pageSubtitle}>
                Create full system backup snapshots or restore banking data from previous backups
              </p>
            </div>
          </div>
        </div>

        {/* Non-Admin Warning if applicable */}
        {!isAdmin && (
          <div className={styles.warningBanner} role="alert">
            <AlertTriangle size={20} className={styles.bannerIcon} />
            <div>
              <strong className={styles.bannerTitle}>Administrator Privileges Required</strong>
              <p className={styles.bannerBody}>
                System backup export and database restore operations are restricted to Admin accounts. Your current role is '{user?.role || 'User'}'. Attempting system operations will return 403 Forbidden from the backend server.
              </p>
            </div>
          </div>
        )}

        <div className={styles.cardsGrid}>

          {/* ── CARD 1: BACKUP / SAVE ────────────────────────────────────────── */}
          <section className={styles.card}>
            <div className={styles.cardHeader}>
              <div className={`${styles.cardIconBox} ${styles.saveIconBox}`}>
                <Download size={22} />
              </div>
              <div>
                <h2 className={styles.cardTitle}>Backup Banking Data</h2>
                <p className={styles.cardSub}>Export complete system state as a JSON file</p>
              </div>
            </div>

            <p className={styles.cardDesc}>
              Create a complete backup of the banking system including all active bank accounts, account types, balances, and historical transaction ledgers.
            </p>

            {saveSuccessMsg && (
              <div className={styles.successBanner} role="status">
                <CheckCircle2 size={16} />
                <span>{saveSuccessMsg}</span>
              </div>
            )}

            {saveError && (
              <div className={styles.errorBanner} role="alert">
                <AlertCircle size={16} />
                <span>{saveError}</span>
              </div>
            )}

            <div className={styles.cardFooter}>
              <button
                onClick={handleCreateBackup}
                disabled={saving}
                className={styles.primarySaveBtn}
              >
                {saving ? (
                  <>
                    <Loader2 size={16} className={styles.spinner} />
                    Generating Backup...
                  </>
                ) : (
                  <>
                    <Download size={16} />
                    Create Backup
                  </>
                )}
              </button>
            </div>
          </section>

          {/* ── CARD 2: RESTORE ──────────────────────────────────────────────── */}
          <section className={styles.card}>
            <div className={styles.cardHeader}>
              <div className={`${styles.cardIconBox} ${styles.restoreIconBox}`}>
                <Upload size={22} />
              </div>
              <div>
                <h2 className={styles.cardTitle}>Restore Banking Data</h2>
                <p className={styles.cardSub}>Import and restore accounts and transactions from backup JSON</p>
              </div>
            </div>

            <p className={styles.cardDesc}>
              Upload a previously exported JSON backup file to restore accounts and financial transactions. All existing user credentials remain intact.
            </p>

            {restoreSuccessMsg && (
              <div className={styles.successBanner} role="status">
                <CheckCircle2 size={16} />
                <span>{restoreSuccessMsg}</span>
              </div>
            )}

            {restoreError && (
              <div className={styles.errorBanner} role="alert">
                <AlertCircle size={16} />
                <span>{restoreError}</span>
              </div>
            )}

            {/* File Upload Box */}
            <div className={styles.fileUploadArea}>
              <input
                type="file"
                ref={fileInputRef}
                accept=".json,application/json"
                onChange={handleFileChange}
                className={styles.fileInputHidden}
                id="backupFileInput"
              />

              {selectedFile ? (
                <div className={styles.fileSelectedBox}>
                  <div className={styles.fileInfoGroup}>
                    <FileJson size={24} className={styles.fileIcon} />
                    <div>
                      <p className={styles.fileName}>{selectedFile.name}</p>
                      <p className={styles.fileSize}>
                        {(selectedFile.size / 1024).toFixed(1)} KB • JSON Backup
                      </p>
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={handleClearSelectedFile}
                    className={styles.removeFileBtn}
                    title="Remove file"
                  >
                    <X size={16} />
                  </button>
                </div>
              ) : (
                <label htmlFor="backupFileInput" className={styles.fileUploadLabel}>
                  <Upload size={24} className={styles.uploadIcon} />
                  <span className={styles.uploadText}>Click to select JSON backup file</span>
                  <span className={styles.uploadSubtext}>Supports .json exported files</span>
                </label>
              )}
            </div>

            <div className={styles.cardFooter}>
              <button
                type="button"
                onClick={handleOpenRestoreModal}
                disabled={restoring || !parsedBackup}
                className={styles.primaryRestoreBtn}
              >
                {restoring ? (
                  <>
                    <Loader2 size={16} className={styles.spinner} />
                    Restoring Data...
                  </>
                ) : (
                  <>
                    <Upload size={16} />
                    Restore Backup
                  </>
                )}
              </button>
            </div>
          </section>

        </div>

        {/* ── CONFIRMATION MODAL ────────────────────────────────────────────── */}
        {showConfirmModal && (
          <div className={styles.modalOverlay} role="dialog" aria-modal="true">
            <div className={styles.modalCard}>
              <div className={styles.modalWarnIcon}><AlertTriangle size={32} /></div>
              <h3 className={styles.modalTitle}>Restore Banking Data?</h3>
              <p className={styles.modalBody}>
                Restore this backup? This operation will replace the current banking data with the contents of <strong>'{selectedFile?.name}'</strong>.
              </p>
              <p className={styles.modalSubBody}>
                Existing account balances and transaction ledgers will be updated to match the backup state. User accounts and login credentials will not be affected.
              </p>
              <div className={styles.modalActions}>
                <button
                  type="button"
                  onClick={() => setShowConfirmModal(false)}
                  className={styles.cancelBtn}
                  disabled={restoring}
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={handleConfirmRestore}
                  className={styles.confirmRestoreBtn}
                  disabled={restoring}
                >
                  {restoring ? <Loader2 size={16} className={styles.spinner} /> : <Upload size={16} />}
                  Restore
                </button>
              </div>
            </div>
          </div>
        )}

    </div>
  );
}
