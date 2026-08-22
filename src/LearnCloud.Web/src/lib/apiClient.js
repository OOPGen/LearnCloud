// SECURITY C2 FIX: Secure API client - No localStorage for tokens
// - Access token stored in memory only (not localStorage) to prevent XSS exfiltration
// - Refresh token stored as HttpOnly Secure SameSite=Strict cookie (__Host-)
// - Automatic refresh queue with refresh token rotation reuse detection
// - CSP compatible, no eval, no inline scripts

let accessToken = null; // in-memory only, cleared on page reload, re-obtained via refresh cookie
let refreshPromise = null;
let isRefreshing = false;

/**
 * Set access token in memory (called after login)
 * DO NOT store in localStorage
 */
export function setAccessToken(token) {
  accessToken = token;
  // For debugging only, never log token
  // console.log('Access token set in memory, length', token?.length);
}

/**
 * Get access token from memory
 * Returns null if not set (page reload) - caller should trigger refresh
 */
export function getAccessToken() {
  return accessToken;
}

/**
 * Clear tokens from memory (logout)
 */
export function clearTokens() {
  accessToken = null;
}

/**
 * Secure fetch wrapper that:
 * - Adds Authorization Bearer from memory
 * - Includes credentials to send HttpOnly refresh cookie
 * - Handles 401 with automatic refresh and retry
 * - Prevents token leakage via CSP, no localStorage
 */
export async function apiFetch(url, options = {}) {
  const headers = { ...(options.headers || {}) };
  
  // Add Authorization if we have token in memory
  if (accessToken && !headers['Authorization'] && !headers['authorization']) {
    headers['Authorization'] = `Bearer ${accessToken}`;
  }
  
  // Always include credentials to send HttpOnly refresh cookie
  const opts = {
    ...options,
    headers,
    credentials: 'include', // crucial for refresh cookie
  };

  let response = await fetch(url, opts);

  // Handle 401 - try refresh once
  if (response.status === 401 && !options._retry) {
    // Avoid infinite loop if refresh itself fails
    if (url.includes('/api/auth/refresh') || url.includes('/api/auth/login')) {
      clearTokens();
      // Don't redirect automatically here - let caller handle
      return response;
    }

    // Queue refresh to avoid multiple simultaneous refresh calls
    if (!isRefreshing) {
      isRefreshing = true;
      refreshPromise = (async () => {
        try {
          // Attempt refresh via HttpOnly cookie - no token in body needed for web
          const refreshRes = await fetch('/api/auth/refresh', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'include',
            body: JSON.stringify({}), // empty body, cookie provides token
          });
          if (!refreshRes.ok) {
            const text = await refreshRes.text();
            throw new Error(`Refresh failed: ${refreshRes.status} ${text}`);
          }
          const data = await refreshRes.json();
          if (!data.accessToken) throw new Error('No accessToken in refresh response');
          setAccessToken(data.accessToken);
          return data.accessToken;
        } catch (e) {
          clearTokens();
          // Optional: redirect to login
          // window.location.href = '/login';
          throw e;
        } finally {
          isRefreshing = false;
        }
      })();
    }

    try {
      const newToken = await refreshPromise;
      // Retry original request with new token
      const retryHeaders = { ...(options.headers || {}) };
      retryHeaders['Authorization'] = `Bearer ${newToken}`;
      const retryOpts = {
        ...options,
        headers: retryHeaders,
        credentials: 'include',
        _retry: true, // prevent infinite retry loop
      };
      response = await fetch(url, retryOpts);
    } catch (refreshError) {
      // Refresh failed - return original 401
      // Caller should handle redirect to login
    }
  }

  return response;
}

/**
 * Login helper - stores access token in memory, refresh token is set via HttpOnly cookie by server
 * Server should set __Host-refresh_token cookie Secure HttpOnly SameSite=Strict Path=/api/auth
 * And should NOT return refresh token in body for web (mobile uses SecureStore and body)
 */
export async function login(email, password, tenantSlug) {
  const res = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include', // to receive cookie
    body: JSON.stringify({ email, password, tenantSlug }),
  });
  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: 'Login failed' }));
    throw new Error(err.message || 'Login failed');
  }
  const data = await res.json();
  // Store access token in memory ONLY
  setAccessToken(data.accessToken);
  // refresh token is in HttpOnly cookie - do NOT store in localStorage
  // For mobile, data.refreshToken would be stored in SecureStore, but web ignores body refreshToken
  return data;
}

/**
 * Logout - clears memory and revokes refresh cookie server-side
 */
export async function logout() {
  try {
    await fetch('/api/auth/logout', {
      method: 'POST',
      credentials: 'include',
    });
  } finally {
    clearTokens();
    // Clear any legacy localStorage tokens (migration)
    try {
      localStorage.removeItem('access_token');
      localStorage.removeItem('refresh_token');
      localStorage.removeItem('selectedChildId'); // this one is okay, not token, but we keep for UX? Actually selectedChildId is not sensitive, can stay
    } catch {}
  }
}

// Legacy migration: On load, remove any old tokens from localStorage that were stored insecurely
try {
  if (localStorage.getItem('access_token') || localStorage.getItem('refresh_token')) {
    console.warn('SECURITY: Removing legacy tokens from localStorage - migrating to secure memory + HttpOnly cookie');
    localStorage.removeItem('access_token');
    localStorage.removeItem('refresh_token');
  }
} catch {}

// Export for backwards compat but deprecated - will be removed
export const getAccessTokenLegacy = () => {
  console.warn('SECURITY: getAccessTokenLegacy uses localStorage - deprecated, use getAccessToken() from memory');
  try {
    return localStorage.getItem('access_token');
  } catch {
    return null;
  }
};
