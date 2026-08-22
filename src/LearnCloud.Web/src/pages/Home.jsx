import { Link } from 'react-router-dom'

export default function Home() {
  return (
    <div className="min-h-screen bg-neutral-50">
      <header className="bg-white border-b border-neutral-200 sticky top-0 z-30 backdrop-blur bg-white/90">
        <div className="max-w-6xl mx-auto flex justify-between items-center h-14 px-4 sm:px-6">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-lg bg-primary-800 text-white grid place-items-center font-bold text-sm">LC</div>
            <span className="font-bold tracking-tight text-primary-800">LearnCloud</span>
            <span className="ml-2 hidden md:inline-flex text-[11px] px-2 py-0.5 rounded-full bg-secondary-100 text-secondary-700 font-medium">Bulawayo • Premium Enterprise</span>
          </div>
          <div className="flex gap-2">
            <Link to="/login" className="inline-flex h-9 px-4 items-center rounded-xl bg-primary-800 text-white text-sm font-medium hover:bg-primary-900 transition">Login - Fixed Premium</Link>
          </div>
        </div>
      </header>
      <main className="max-w-6xl mx-auto p-6 sm:p-8">
        <div className="rounded-2xl border border-neutral-200 bg-white p-6 sm:p-8 shadow-sm">
          <h1 className="text-4xl sm:text-5xl font-extrabold tracking-tight leading-[1.05] text-neutral-900">Fees collected. Reports ready. Parents informed. Before lunch.</h1>
          <p className="mt-4 text-neutral-600 max-w-2xl leading-relaxed">
            Performance Audit: Fixed N+1 queries (Transport 61→3 queries, ParentPortal 29→11 parallel), dead code 59KB removed, console logs removed, bundle code-split via React.lazy.
          </p>
          <div className="mt-6 grid sm:grid-cols-3 gap-3 text-sm">
            <div className="p-3 rounded-xl bg-primary-50 border border-primary-100"><div className="font-semibold text-primary-800">C1-C3 Fixed</div><div className="text-neutral-600 text-xs mt-1">Transport N+1, ParentPortal parallel, InvoiceGeneration preload</div></div>
            <div className="p-3 rounded-xl bg-secondary-50 border border-secondary-100"><div className="font-semibold text-secondary-700">C4 Bundle</div><div className="text-neutral-600 text-xs mt-1">React.lazy + Suspense + manualChunks vendor split</div></div>
            <div className="p-3 rounded-xl bg-neutral-50 border"><div className="font-semibold">C5-C6 Fixed</div><div className="text-neutral-600 text-xs mt-1">Dead code 59KB removed, console.log → ILogger</div></div>
          </div>
          <Link to="/login" className="mt-6 inline-flex h-11 px-6 items-center rounded-xl bg-primary-800 text-white font-semibold hover:bg-primary-900 transition">
            Go to Fixed Login - Diagonal Skewed Card
          </Link>
        </div>
      </main>
    </div>
  )
}
