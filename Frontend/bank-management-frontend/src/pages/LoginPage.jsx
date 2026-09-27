import { useState } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { Eye, EyeOff, Lock, Mail, AlertCircle, Loader2, Building2, ShieldCheck } from 'lucide-react';
import { useAuth } from '../context/AuthContext';
import styles from './LoginPage.module.css';

export default function LoginPage() {
  const { login, isAuthenticated } = useAuth();
  const navigate  = useNavigate();
  const location  = useLocation();

  const [email,    setEmail]    = useState('');
  const [password, setPassword] = useState('');
  const [showPass, setShowPass] = useState(false);
  const [error,    setError]    = useState('');
  const [loading,  setLoading]  = useState(false);

  // If already logged in, go straight to dashboard
  const from = location.state?.from?.pathname || '/dashboard';
  if (isAuthenticated) {
    navigate(from, { replace: true });
    return null;
  }

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');

    if (!email.trim() || !password.trim()) {
      setError('Please enter your email and password.');
      return;
    }

    setLoading(true);
    const result = await login(email.trim(), password);
    setLoading(false);

    if (result.success) {
      navigate(from, { replace: true });
    } else {
      setError(result.message);
    }
  };

  return (
    <div className={styles.root}>
      {/* ── Animated background grid ── */}
      <div className={styles.grid} aria-hidden="true" />

      {/* ── Glowing orbs ── */}
      <div className={styles.orb1} aria-hidden="true" />
      <div className={styles.orb2} aria-hidden="true" />

      {/* ── Card ── */}
      <main className={styles.card}>

        {/* Logo */}
        <div className={styles.logoWrap}>
          <div className={styles.logoIcon}>
            <Building2 size={28} />
          </div>
          <div>
            <h1 className={styles.logoName}>NexaBank</h1>
            <p className={styles.logoTagline}>Banking Management System</p>
          </div>
        </div>

        {/* Divider */}
        <div className={styles.divider} />

        {/* Heading */}
        <div className={styles.heading}>
          <h2 className={styles.headingTitle}>Welcome back</h2>
          <p className={styles.headingSubtitle}>Sign in to access your secure banking portal</p>
        </div>

        {/* Error banner */}
        {error && (
          <div className={styles.errorBanner} role="alert">
            <AlertCircle size={16} />
            <span>{error}</span>
          </div>
        )}

        {/* Form */}
        <form onSubmit={handleSubmit} noValidate className={styles.form}>

          {/* Email field */}
          <div className={styles.field}>
            <label htmlFor="email" className={styles.label}>Email address</label>
            <div className={styles.inputWrap}>
              <Mail size={16} className={styles.inputIcon} />
              <input
                id="email"
                type="email"
                autoComplete="email"
                placeholder="admin@bankms.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className={styles.input}
                disabled={loading}
                required
              />
            </div>
          </div>

          {/* Password field */}
          <div className={styles.field}>
            <label htmlFor="password" className={styles.label}>Password</label>
            <div className={styles.inputWrap}>
              <Lock size={16} className={styles.inputIcon} />
              <input
                id="password"
                type={showPass ? 'text' : 'password'}
                autoComplete="current-password"
                placeholder="Enter your password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                className={`${styles.input} ${styles.inputPaddedRight}`}
                disabled={loading}
                required
              />
              <button
                type="button"
                onClick={() => setShowPass((v) => !v)}
                className={styles.toggleBtn}
                tabIndex={-1}
                aria-label={showPass ? 'Hide password' : 'Show password'}
              >
                {showPass ? <EyeOff size={16} /> : <Eye size={16} />}
              </button>
            </div>
          </div>

          {/* Submit */}
          <button
            type="submit"
            className={styles.submitBtn}
            disabled={loading}
          >
            {loading ? (
              <>
                <Loader2 size={18} className={styles.spinner} />
                Authenticating…
              </>
            ) : (
              <>
                <ShieldCheck size={18} />
                Sign In Securely
              </>
            )}
          </button>
        </form>

        {/* Footer */}
        <p className={styles.footer}>
          Protected by enterprise-grade JWT authentication
        </p>

        {/* Register link */}
        <p className={styles.registerLink}>
          Don&apos;t have an account?{' '}
          <Link to="/register" className={styles.link}>Create one</Link>
        </p>
      </main>
    </div>
  );
}
