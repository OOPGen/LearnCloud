import { useEffect, useRef, useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import AuthCard from '../components/layout/AuthCard';
import { verifyEmail } from '../lib/apiClient';

// Opened from the verification email: /verify-email?school=&email=&token=
export default function VerifyEmailPage() {
  const [params] = useSearchParams();
  const email = params.get('email') || '';
  const token = params.get('token') || '';
  const school = params.get('school') || '';
  const [state, setState] = useState(email && token ? 'checking' : 'incomplete');
  const started = useRef(false);

  useEffect(() => {
    // Once only: in development React runs effects twice, and a token can be used once.
    if (state !== 'checking' || started.current) return;
    started.current = true;
    verifyEmail(email, token)
      .then(() => setState('verified'))
      .catch(error => setState(/invalid|expired/i.test(error.message) ? 'expired' : 'failed'));
  }, [state, email, token]);

  const signInLink = `/login?${new URLSearchParams({ ...(school && { school }), ...(email && { email }) })}`;
  const content = {
    checking: ['Confirming your email...', 'One moment.'],
    verified: ['Email confirmed', 'Thank you. You can sign in now.'],
    expired: ['Link expired', 'This confirmation link has expired or was already used. If you can sign in, your email may already be confirmed.'],
    failed: ['Something went wrong', 'We could not confirm your email just now. Please try the link again in a few minutes.'],
    incomplete: ['Link incomplete', 'This link is missing information. Open the link from the email again.'],
  }[state];

  return (
    <AuthCard title={content[0]}>
      <p className="text-[14px] text-neutral-700" role="status" aria-live="polite">{content[1]}</p>
      {state !== 'checking' && (
        <Link to={signInLink} className="mt-5 inline-flex h-11 px-4 items-center rounded-xl bg-primary-800 text-white font-medium">Go to sign in</Link>
      )}
    </AuthCard>
  );
}
