import { useEffect, useState } from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { getAccessToken, restoreSession } from './apiClient';

// Guards signed-in pages. The access token lives only in memory, so after a page load
// the session is restored from the HttpOnly refresh cookie before rendering; when that
// fails the user is sent to /login and returned to this page after signing in.
export default function RequireAuth({ children }) {
  const location = useLocation();
  const [status, setStatus] = useState(getAccessToken() ? 'ready' : 'checking');

  useEffect(() => {
    if (status !== 'checking') return undefined;
    let cancelled = false;
    restoreSession().then(session => {
      if (!cancelled) setStatus(session ? 'ready' : 'signed-out');
    });
    return () => { cancelled = true; };
  }, [status]);

  if (status === 'signed-out') {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />;
  }

  if (status === 'checking') {
    return (
      <div className="min-h-screen bg-neutral-50 flex items-center justify-center" role="status" aria-live="polite">
        <div className="text-center">
          <div className="w-8 h-8 border-4 border-primary-200 border-t-primary-800 rounded-full animate-spin mx-auto" />
          <p className="mt-3 text-sm text-neutral-600">Restoring your session...</p>
        </div>
      </div>
    );
  }

  return children;
}
