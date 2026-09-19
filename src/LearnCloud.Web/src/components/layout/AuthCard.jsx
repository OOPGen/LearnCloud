import { Link } from 'react-router-dom';

// Centered card for signed-out pages reached from email links.
export default function AuthCard({ title, children }) {
  return (
    <div className="min-h-screen bg-neutral-50 flex items-center justify-center px-4 py-10">
      <main className="w-full max-w-md rounded-2xl bg-white border border-neutral-200 shadow-sm p-6 sm:p-8">
        <Link to="/login" className="inline-flex items-center gap-2 mb-6">
          <span className="w-8 h-8 rounded-lg bg-primary-900 text-white grid place-items-center font-bold text-sm">LC</span>
          <span className="font-bold text-neutral-900">LearnCloud</span>
        </Link>
        <h1 className="text-[22px] font-bold tracking-[-0.02em] text-neutral-900 mb-4">{title}</h1>
        {children}
      </main>
    </div>
  );
}
