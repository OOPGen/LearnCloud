// API client for the LearnCloud web app.
//
// - The access token lives in memory only, never in localStorage.
// - The refresh token is an HttpOnly, Secure, SameSite=Strict cookie set by the API.
// - All calls use relative /api paths. Locally Vite proxies them to the API; on
//   Cloudflare Pages the function in functions/api/[[path]].js does. Keeping the API
//   same-origin is what lets the SameSite=Strict refresh cookie work at all.

let accessToken = null;
let sessionInfo = null; // { userId, tenantId, displayName } from the last login or refresh
let refreshPromise = null;

// Required by the API for cookie-based refresh (CSRF defence). Without it every refresh
// was rejected and sessions ended when the 15-minute access token expired.
const CSRF_HEADER = { 'X-Requested-With': 'XMLHttpRequest' };

export function setAccessToken(token) {
  accessToken = token;
}

export function getAccessToken() {
  return accessToken;
}

export function clearTokens() {
  accessToken = null;
  sessionInfo = null;
}

/** Who is signed in, or null. Kept in memory with the access token. */
export function getSessionInfo() {
  return sessionInfo;
}

function rememberSession(data) {
  setAccessToken(data.accessToken);
  sessionInfo = { userId: data.userId, tenantId: data.tenantId, displayName: data.displayName };
}

/** Error carrying the API's status and a message fit to show a user. */
export class ApiError extends Error {
  constructor(status, message, fieldErrors = {}) {
    super(message);
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

/**
 * The school slug from the host name, e.g. "petra" for petra.learncloud.co.zw.
 * VITE_ROOT_DOMAIN names the platform domain. Returns null on the bare domain,
 * reserved subdomains, localhost and preview hosts such as *.pages.dev, where the
 * user types the school instead.
 */
export function getTenantSlugFromHost(hostname = window.location.hostname) {
  const root = (import.meta.env.VITE_ROOT_DOMAIN || '').toLowerCase();
  const host = hostname.toLowerCase();
  if (!root || !host.endsWith(`.${root}`)) return null;
  const slug = host.slice(0, -(root.length + 1));
  const reserved = ['www', 'app', 'api', 'admin', 'platform'];
  return /^[a-z0-9-]{3,50}$/.test(slug) && !reserved.includes(slug) ? slug : null;
}

async function toApiError(response) {
  let body = null;
  try { body = await response.json(); } catch { /* not JSON */ }
  const fieldErrors = {};
  if (body && body.errors && typeof body.errors === 'object' && !Array.isArray(body.errors)) {
    for (const [field, messages] of Object.entries(body.errors)) {
      const key = field.charAt(0).toLowerCase() + field.slice(1);
      fieldErrors[key] = Array.isArray(messages) ? messages[0] : String(messages);
    }
  }
  const firstFieldError = Object.values(fieldErrors)[0];
  const fallback = response.status === 429
    ? 'Too many attempts. Please wait a minute and try again.'
    : response.status >= 500 ? 'Something went wrong on our side. Please try again.' : 'Request failed.';
  const message = (body && (body.detail || body.message)) || firstFieldError || (body && body.title) || fallback;
  return new ApiError(response.status, message, fieldErrors);
}

async function refreshAccessToken() {
  if (!refreshPromise) {
    refreshPromise = (async () => {
      try {
        const res = await fetch('/api/auth/refresh', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', ...CSRF_HEADER },
          credentials: 'include',
          body: JSON.stringify({}),
        });
        if (!res.ok) throw await toApiError(res);
        const data = await res.json();
        if (!data.accessToken) throw new ApiError(res.status, 'No access token in refresh response');
        rememberSession(data);
        return data;
      } catch (e) {
        clearTokens();
        throw e;
      } finally {
        refreshPromise = null;
      }
    })();
  }
  return refreshPromise;
}

/**
 * Restores a session after a page load using the refresh cookie.
 * Resolves to the session data, or null when the user must sign in.
 */
export async function restoreSession() {
  if (accessToken) return { accessToken };
  try {
    return await refreshAccessToken();
  } catch {
    return null;
  }
}

/**
 * fetch with the in-memory bearer token, the refresh cookie, and one automatic
 * refresh-and-retry on 401.
 */
export async function apiFetch(url, options = {}) {
  const withAuth = () => {
    const headers = { ...(options.headers || {}) };
    if (accessToken) headers.Authorization = `Bearer ${accessToken}`;
    return { ...options, headers, credentials: 'include' };
  };

  let response = await fetch(url, withAuth());
  const isAuthCall = url.startsWith('/api/auth/login') || url.startsWith('/api/auth/refresh');
  if (response.status !== 401 || isAuthCall) return response;

  try {
    await refreshAccessToken();
  } catch {
    return response; // caller decides: usually send the user to /login
  }
  response = await fetch(url, withAuth());
  return response;
}

/** apiFetch for JSON APIs: sends a JSON body, returns parsed JSON, throws ApiError. */
export async function apiJson(url, { method = 'GET', body } = {}) {
  const response = await apiFetch(url, {
    method,
    headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });
  if (!response.ok) throw await toApiError(response);
  if (response.status === 204) return null;
  const text = await response.text();
  return text ? JSON.parse(text) : null;
}

/** Signs in. The API sets the refresh cookie; the access token stays in memory. */
export async function login(email, password, tenantSlug) {
  const res = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ email: email.trim().toLowerCase(), password, tenantSlug: tenantSlug || null, device: navigator.userAgent.slice(0, 100) }),
  });
  if (!res.ok) throw await toApiError(res);
  const data = await res.json();
  rememberSession(data);
  return data;
}

/** Registers a school and its first administrator. */
export async function registerSchool(request) {
  const res = await fetch('/api/auth/register-tenant', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify(request),
  });
  if (!res.ok) throw await toApiError(res);
  return res.json();
}

/** Starts a password reset. The API answers the same way whether or not the account exists. */
export async function forgotPassword(email, tenantSlug) {
  const res = await fetch('/api/auth/forgot-password', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ email: email.trim().toLowerCase(), tenantSlug: tenantSlug || null }),
  });
  if (!res.ok) throw await toApiError(res);
  return res.json();
}

/** Signs out: revokes refresh tokens server-side and clears the in-memory token. */
export async function logout() {
  try {
    if (accessToken) await apiFetch('/api/auth/logout', { method: 'POST' });
  } catch {
    // Signing out locally still matters if the API is unreachable.
  } finally {
    clearTokens();
  }
}

// Tokens were once kept in localStorage; remove any leftovers.
try {
  localStorage.removeItem('access_token');
  localStorage.removeItem('refresh_token');
} catch { /* storage unavailable */ }
