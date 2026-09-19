import { Link } from 'react-router-dom'

// The marketing site is a separate Worker, so its pages are absolute links. Set
// VITE_MARKETING_URL at build time once the custom domain is in use.
const MARKETING = import.meta.env.VITE_MARKETING_URL || 'https://learncloud.jeremichaeljunior.workers.dev'
// Digits only, no plus. VITE_WHATSAPP_NUMBER overrides it without a code change.
const WHATSAPP = import.meta.env.VITE_WHATSAPP_NUMBER || '263786233766'

const navLinks = [
  { label: 'Features', href: `${MARKETING}/features` },
  { label: 'Pricing', href: `${MARKETING}/pricing` },
  { label: 'News', href: `${MARKETING}/blog` },
]

const cards = [
  {
    title: 'About us',
    line: 'Built in Bulawayo',
    href: `${MARKETING}/about`,
    className: 'bg-primary-900/70 text-white border border-white/25 hover:bg-primary-900/85',
  },
  {
    title: 'Pricing',
    line: '14-day free trial',
    href: `${MARKETING}/pricing`,
    className: 'bg-gradient-to-br from-[#145ced] to-[#7933e8] text-white border border-white/10 hover:brightness-110',
  },
  {
    title: 'WhatsApp',
    line: 'Chat with us',
    href: `https://wa.me/${WHATSAPP}`,
    className: 'bg-white/90 text-primary-800 border border-white hover:bg-white',
  },
]

export default function Home() {
  return (
    <div className="relative min-h-screen overflow-hidden">
      {/* Graduation at sunset, under the logo's navy-to-purple gradient so white text stays
          readable. Decorative, so it carries no alt text. */}
      <img
        src="/hero-graduates-1600.jpg"
        srcSet="/hero-graduates-900.jpg 900w, /hero-graduates-1600.jpg 1600w"
        sizes="100vw"
        alt=""
        aria-hidden="true"
        fetchPriority="high"
        className="absolute inset-0 h-full w-full object-cover"
      />
      {/* Light enough to keep the sunset visible; the vertical wash underneath holds contrast
          for the nav and footer without flattening the photo. */}
      <div aria-hidden="true" className="absolute inset-0 bg-gradient-to-br from-[#031b65]/80 via-[#08338d]/55 to-[#7933e8]/65" />
      <div aria-hidden="true" className="absolute inset-0 bg-gradient-to-b from-primary-950/45 via-primary-950/10 to-primary-950/55" />

      <div className="relative flex min-h-screen flex-col">
        <header className="px-4 sm:px-6 lg:px-10">
          <nav className="mx-auto flex h-20 max-w-6xl items-center justify-between gap-4" aria-label="Main">
            <Link to="/" className="flex items-center gap-2.5" aria-label="LearnCloud home">
              <img src="/logo-mark.png" alt="" className="h-10 w-10 rounded-xl bg-white object-contain p-1" />
              <span className="text-[19px] font-bold tracking-tight text-white">LearnCloud</span>
            </Link>

            <div className="hidden items-center gap-8 md:flex">
              {navLinks.map(link => (
                <a key={link.label} href={link.href} className="text-[15px] font-semibold text-white/85 transition hover:text-white">
                  {link.label}
                </a>
              ))}
            </div>

            <Link
              to="/login?mode=register"
              className="inline-flex min-h-touch items-center rounded-full bg-gradient-to-r from-[#145ced] to-[#7933e8] px-6 text-[15px] font-semibold text-white shadow-lg transition hover:brightness-110"
            >
              Get started
            </Link>
          </nav>
        </header>

        <main className="flex flex-1 items-center px-4 pb-16 pt-6 sm:px-6 lg:px-10">
          <div className="mx-auto w-full max-w-4xl text-center">
            <h1 className="text-[34px] font-extrabold leading-[1.1] tracking-[-0.02em] text-white sm:text-[48px] lg:text-[58px]">
              School management system for Zimbabwean schools
            </h1>
            <p className="mt-5 text-[14px] font-medium uppercase tracking-[0.35em] text-white/75 sm:text-[16px]">
              Students · Fees · Results
            </p>
            <p className="mx-auto mt-5 max-w-2xl text-[16px] leading-relaxed text-white/90 sm:text-[18px]">
              One secure platform for student records, fees, attendance and reports — built in
              Bulawayo for schools of 150 to 2,000 learners.
            </p>

            <div className="mt-10 flex flex-col items-center justify-center gap-5 sm:flex-row sm:items-start sm:gap-4">
              <Link
                to="/login"
                className="inline-flex min-h-touch w-full max-w-[260px] items-center justify-center rounded-xl bg-gradient-to-r from-[#145ced] to-[#7933e8] px-8 py-3.5 text-[16px] font-semibold text-white shadow-xl transition hover:brightness-110 sm:w-auto"
              >
                Login
              </Link>
              <div className="flex w-full max-w-[260px] flex-col items-center gap-2 sm:w-auto">
                <Link
                  to="/login?mode=register"
                  className="inline-flex min-h-touch w-full items-center justify-center rounded-xl border-2 border-white/80 px-8 py-3.5 text-[16px] font-semibold text-white transition hover:bg-white hover:text-primary-800 sm:w-auto"
                >
                  Register
                </Link>
                <span className="whitespace-nowrap rounded-full bg-gradient-to-r from-[#145ced] to-[#7933e8] px-3 py-1 text-[12px] font-semibold italic text-white shadow-md">
                  Free trial — no card
                </span>
              </div>
            </div>

            <div className="mt-20 grid gap-4 sm:grid-cols-3">
              {cards.map(card => (
                <a key={card.title} href={card.href} className={`rounded-2xl px-5 py-6 text-center shadow-lg backdrop-blur-sm transition ${card.className}`}>
                  <div className="text-[15px] font-bold uppercase tracking-wide">{card.title}</div>
                  <div className="mt-1.5 text-[14px] opacity-90">{card.line}</div>
                </a>
              ))}
            </div>
          </div>
        </main>

        <footer className="px-4 pb-6 text-center text-[13px] text-white/60 sm:px-6">
          © {new Date().getFullYear()} LearnCloud · Bulawayo, Zimbabwe
        </footer>
      </div>
    </div>
  )
}
