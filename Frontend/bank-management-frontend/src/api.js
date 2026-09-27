import axios from 'axios';

// ─── Base Axios instance ──────────────────────────────────────────────────────
// Uses relative '/api/v1' path so Vite proxy forwards requests to https://localhost:7225
// without CORS or self-signed SSL certificate issues in local development.
const api = axios.create({
  baseURL: '/api/v1',
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 15000,
});

// ─── Request interceptor — attach JWT & safe dev logging ──────────────────────
// NEVER logs passwords, JWTs, password hashes, or sensitive credentials.
api.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem('bams_token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    if (import.meta.env.DEV) {
      console.log(`[API Request] ${config.method?.toUpperCase()} ${config.baseURL || ''}${config.url}`);
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// ─── Response interceptor — safe dev logging & 401 handler ───────────────────
api.interceptors.response.use(
  (response) => {
    if (import.meta.env.DEV) {
      console.log(`[API Response] ${response.status} ${response.config?.url}`);
    }
    return response;
  },
  (error) => {
    if (import.meta.env.DEV) {
      console.error(`[API Error] ${error.response?.status || 'Network/Proxy Error'} ${error.config?.url}`);
    }
    if (error.response?.status === 401) {
      localStorage.removeItem('bams_token');
      localStorage.removeItem('bams_user');
      window.location.href = '/login';
    }
    return Promise.reject(error);
  }
);

export default api;
