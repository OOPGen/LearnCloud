import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import AuthCard from '../components/layout/AuthCard';
import { Button } from '../components/ui/Button';
import { PasswordInput } from '../components/ui/Input';
import { resetPassword } from '../lib/apiClient';
import { passwordProblem } from '../lib/passwords';

// Opened from the password reset email: /reset-password?school=&email=&token=
export default function ResetPasswordPage() {
  const [params] = useSearchParams();
  const email = params.get('email') || '';
  const token = params.get('token') || '';
  const school = params.get('school') || '';
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [errors, setErrors] = useState({});
  const [saving, setSaving] = useState(false);
  const [done, setDone] = useState(false);

  const signInLink = `/login?${new URLSearchParams({ ...(school && { school }), ...(email && { email }) })}`;

  if (!email || !token) {
    return (
      <AuthCard title="Reset link incomplete">
        <p className="text-[14px] text-neutral-700">This link is missing information. Open the link from the email again, or ask for a new one on the sign-in page.</p>
        <Link to="/login" className="mt-4 inline-block text-secondary-600 underline underline-offset-4">Go to sign in</Link>
      </AuthCard>
    );
  }

  async function submit(event) {
    event.preventDefault();
    const next = {};
    const problem = passwordProblem(password);
    if (problem) next.password = problem;
    if (confirm !== password) next.confirm = 'The passwords do not match';
    setErrors(next);
    if (Object.keys(next).length) return;

    setSaving(true);
    try {
      await resetPassword(email, token, password, confirm);
      setDone(true);
    } catch (error) {
      setErrors({ form: /invalid|expired/i.test(error.message) ? 'This reset link has expired or was already used. Ask for a new one on the sign-in page.' : error.message });
    } finally {
      setSaving(false);
    }
  }

  if (done) {
    return (
      <AuthCard title="Password changed">
        <p className="text-[14px] text-neutral-700">Your password has been changed and you have been signed out everywhere. Sign in with your new password.</p>
        <Link to={signInLink} className="mt-5 inline-flex h-11 px-4 items-center rounded-xl bg-primary-800 text-white font-medium">Sign in</Link>
      </AuthCard>
    );
  }

  return (
    <AuthCard title="Choose a new password">
      <p className="text-[14px] text-neutral-600 mb-5">For <strong className="text-neutral-900">{email}</strong>{school ? <> at school <strong className="text-neutral-900">{school}</strong></> : null}.</p>
      <form onSubmit={submit} noValidate className="space-y-4">
        {errors.form && <p role="alert" className="text-[13px] text-danger-600">{errors.form}</p>}
        <PasswordInput label="New password" id="reset-password" autoComplete="new-password" required value={password} onChange={e => setPassword(e.target.value)} error={errors.password} helpText="At least 8 characters with upper and lower case letters, a number and a symbol." />
        <PasswordInput label="Confirm new password" id="reset-confirm" autoComplete="new-password" required value={confirm} onChange={e => setConfirm(e.target.value)} error={errors.confirm} />
        <Button type="submit" loading={saving} className="w-full">Change password</Button>
      </form>
    </AuthCard>
  );
}
