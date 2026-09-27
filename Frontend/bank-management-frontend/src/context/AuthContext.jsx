import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import api from '../api';

// ─── Context ──────────────────────────────────────────────────────────────────
const AuthContext = createContext(null);

// ─── Storage keys (centralised to avoid typos) ───────────────────────────────
const TOKEN_KEY = 'bams_token';
const USER_KEY  = 'bams_user';

// ─── Provider ─────────────────────────────────────────────────────────────────
export function AuthProvider({ children }) {
  const [token, setToken]   = useState(() => localStorage.getItem(TOKEN_KEY));
  const [user,  setUser]    = useState(() => {
    try {
      const stored = localStorage.getItem(USER_KEY);
      return stored ? JSON.parse(stored) : null;
    } catch {
      return null;
    }
  });
  const [loading, setLoading] = useState(false);

  // True only when we have both a token and user info
  const isAuthenticated = Boolean(token && user);

  // ── login ─────────────────────────────────────────────────────────────────
  // POST /api/v1/auth/login
  // LoginRequest:  { email, password }
  // LoginResponse: { success, message, token, user: { id, firstName, lastName, email, role } }
  const login = useCallback(async (email, password) => {
    setLoading(true);
    try {
      const { data } = await api.post('/auth/login', { email, password });

      if (!data.success) {
        return { success: false, message: data.message || 'Login failed.' };
      }

      // Persist token and user info
      localStorage.setItem(TOKEN_KEY, data.token);
      localStorage.setItem(USER_KEY,  JSON.stringify(data.user));

      setToken(data.token);
      setUser(data.user);

      return { success: true };
    } catch (err) {
      const status  = err.response?.status;
      const message = err.response?.data?.message ?? err.message;

      if (status === 429) {
        return { success: false, message: 'Too many login attempts. Please wait a minute and try again.' };
      }
      if (status === 401) {
        return { success: false, message: 'Invalid email or password.' };
      }
      return { success: false, message: message || 'Unable to connect to server.' };
    } finally {
      setLoading(false);
    }
  }, []);

  // ── logout ────────────────────────────────────────────────────────────────
  const logout = useCallback(() => {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    setToken(null);
    setUser(null);
  }, []);

  // ── Keep localStorage in sync with state ─────────────────────────────────
  // (Handles the case where the 401 interceptor in api.js clears localStorage
  //  but doesn't update React state — the next render will pick up the null.)
  useEffect(() => {
    const syncState = () => {
      const storedToken = localStorage.getItem(TOKEN_KEY);
      if (!storedToken && token) {
        setToken(null);
        setUser(null);
      }
    };
    window.addEventListener('storage', syncState);
    return () => window.removeEventListener('storage', syncState);
  }, [token]);

  const value = useMemo(
    () => ({ isAuthenticated, user, token, loading, login, logout }),
    [isAuthenticated, user, token, loading, login, logout]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

// ─── Hook ─────────────────────────────────────────────────────────────────────
export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>');
  return ctx;
}
