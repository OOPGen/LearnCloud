import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'

// The marketing site is a separate Worker, so its pages are absolute links. Set
// VITE_MARKETING_URL at build time once the custom domain is in use.
const MARKETING = import.meta.env.VITE_MARKETING_URL || 'https://learncloud.jeremichaeljunior.workers.dev'
// Digits only, no plus. VITE_WHATSAPP_NUMBER overrides it without a code change.
const WHATSAPP = import.meta.env.VITE_WHATSAPP_NUMBER || '263786233766'
const PHONE = import.meta.env.VITE_PHONE_NUMBER || '+263 71 862 1427'

// Sections of this page, in order. "Home" is first so the nav always offers the way back.
const sections = [
  { id: 'home', label: 'Home' },
  { id: 'features', label: 'Features' },
  { id: 'about', label: 'About' },
  { id: 'contact', label: 'Contact' },
]

const features = [
  { title: 'Students and guardians', body: 'Every learner in one place: enrolment, class, guardians and who may collect them.' },
  { title: 'Fees and arrears', body: 'See who owes what, and since when, without asking the bursar to open a spreadsheet.' },
  { title: 'Attendance', body: 'Mark a register quickly, and keep the term totals that follow from it.' },
  { title: 'Marks and reports', body: 'Assemble term reports from marks already captured, instead of retyping them.' },
  { title: 'Messages to parents', body: 'Reach a class or the whole school, with a record of what was sent and to whom.' },
  { title: 'Staff and payroll export', body: 'Staff records, and a payroll-ready export for the product that pays them.' },
]

const promises = [
  { title: 'Your data is never deleted', body: 'An unpaid account becomes read-only: you can still open and export everything, and nothing is thrown away.' },
  { title: 'One school cannot see another', body: 'Every record is scoped to its school, and that isolation is held in place by automated tests.' },
  { title: '14-day trial, no card', body: 'Start with a trial. We email before it ends, rather than letting it lapse quietly.' },
]

export default function Home() {
  const [menuOpen, setMenuOpen] = useState(false)
  const [scrolled, setScrolled] = useState(false)

  useEffect(() => {
    // Anchor links glide rather than jump, unless the reader asked for less motion.
    const calm = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    if (!calm) document.documentElement.classList.add('scroll-smooth')
    const onScroll = () => setScrolled(window.scrollY > 500)
    onScroll()
    window.addEventListener('scroll', onScroll, { passive: true })

    // The page renders after the browser has looked for the anchor, so a shared link such
    // as /#about would otherwise stay at the top.
    const target = window.location.hash && document.querySelector(window.location.hash)
    if (target) requestAnimationFrame(() => target.scrollIntoView(calm ? undefined : { behavior: 'smooth' }))
    return () => {
      window.removeEventListener('scroll', onScroll)
      document.documentElement.classList.remove('scroll-smooth')
    }
  }, [])

  const navLink = 'text-[15px] font-semibold text-white/85 transition hover:text-white focus-visible:text-white'

  return (
    <div className="relative bg-primary-950">
      {/* Fixed so the way back is always one click away, however far down the reader is. */}
      <header className={`fixed inset-x-0 top-0 z-50 transition ${scrolled ? 'bg-primary-950/90 shadow-lg backdrop-blur' : 'bg-transparent'}`}>
        <nav className="mx-auto flex h-20 max-w-6xl items-center justify-between gap-4 px-4 sm:px-6 lg:px-10" aria-label="Main">
          <a href="#home" className="flex items-center gap-2.5" onClick={() => setMenuOpen(false)}>
            <img src="/logo-mark.png" alt="" className="h-10 w-10 rounded-xl bg-white object-contain p-1" />
            <span className="text-[19px] font-bold tracking-tight text-white">LearnCloud</span>
          </a>

          <div className="hidden items-center gap-8 md:flex">
            {sections.map(s => (
              <a key={s.id} href={`#${s.id}`} className={navLink}>{s.label}</a>
            ))}
            <a href={`${MARKETING}/pricing`} className={navLink}>Pricing</a>
          </div>

          <div className="flex items-center gap-2">
            <Link
              to="/login?mode=register"
              className="hidden min-h-touch items-center rounded-full bg-gradient-to-r from-[#145ced] to-[#7933e8] px-6 text-[15px] font-semibold text-white shadow-lg transition hover:brightness-110 sm:inline-flex"
            >
              Get started
            </Link>
            <button
              type="button"
              onClick={() => setMenuOpen(o => !o)}
              aria-expanded={menuOpen}
              aria-controls="mobile-menu"
              className="grid h-11 w-11 place-items-center rounded-xl border border-white/30 text-white md:hidden"
            >
              <span className="sr-only">{menuOpen ? 'Close menu' : 'Open menu'}</span>
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
                {menuOpen
                  ? <><line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" /></>
                  : <><line x1="3" y1="7" x2="21" y2="7" /><line x1="3" y1="12" x2="21" y2="12" /><line x1="3" y1="17" x2="21" y2="17" /></>}
              </svg>
            </button>
          </div>
        </nav>

        {menuOpen && (
          <div id="mobile-menu" className="border-t border-white/10 bg-primary-950/95 px-4 pb-4 backdrop-blur md:hidden">
            {sections.map(s => (
              <a key={s.id} href={`#${s.id}`} onClick={() => setMenuOpen(false)} className="block border-b border-white/10 py-3 text-[15px] font-semibold text-white/90">
                {s.label}
              </a>
            ))}
            <a href={`${MARKETING}/pricing`} className="block border-b border-white/10 py-3 text-[15px] font-semibold text-white/90">Pricing</a>
            <Link to="/login?mode=register" className="mt-4 flex min-h-touch items-center justify-center rounded-xl bg-gradient-to-r from-[#145ced] to-[#7933e8] px-6 font-semibold text-white">
              Get started
            </Link>
          </div>
        )}
      </header>

      {/* Hero */}
      <section id="home" className="relative min-h-screen overflow-hidden scroll-mt-20">
        <img
          src="/hero-graduates-1600.jpg"
          srcSet="/hero-graduates-900.jpg 900w, /hero-graduates-1600.jpg 1600w"
          sizes="100vw"
          alt=""
          aria-hidden="true"
          fetchPriority="high"
          className="absolute inset-0 h-full w-full object-cover"
        />
        <div aria-hidden="true" className="absolute inset-0 bg-gradient-to-br from-[#031b65]/80 via-[#08338d]/55 to-[#7933e8]/65" />
        <div aria-hidden="true" className="absolute inset-0 bg-gradient-to-b from-primary-950/45 via-primary-950/10 to-primary-950/55" />

        <div className="relative flex min-h-screen flex-col justify-center px-4 pb-16 pt-28 sm:px-6 lg:px-10">
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

            <a href="#features" className="mt-16 inline-flex flex-col items-center gap-1 text-[13px] font-medium text-white/70 transition hover:text-white">
              See what it does
              <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
                <polyline points="6 9 12 15 18 9" />
              </svg>
            </a>
          </div>
        </div>
      </section>

      {/* Features */}
      <section id="features" className="scroll-mt-20 bg-white px-4 py-20 sm:px-6 lg:px-10">
        <div className="mx-auto max-w-6xl">
          <h2 className="text-[28px] font-extrabold tracking-[-0.02em] text-primary-800 sm:text-[36px]">What LearnCloud does</h2>
          <p className="mt-3 max-w-2xl text-[16px] leading-relaxed text-neutral-600">
            The work a school repeats every term, in one place instead of five spreadsheets.
          </p>
          <div className="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {features.map(f => (
              <div key={f.title} className="rounded-2xl border border-neutral-200 bg-neutral-50 p-6 transition hover:border-[#145ced]/40 hover:shadow-md">
                <div className="h-1.5 w-10 rounded-full bg-gradient-to-r from-[#145ced] to-[#7933e8]" />
                <h3 className="mt-4 text-[17px] font-bold text-neutral-900">{f.title}</h3>
                <p className="mt-2 text-[15px] leading-relaxed text-neutral-600">{f.body}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* About */}
      <section id="about" className="scroll-mt-20 bg-primary-950 px-4 py-20 text-white sm:px-6 lg:px-10">
        <div className="mx-auto grid max-w-6xl gap-12 lg:grid-cols-2">
          <div>
            <h2 className="text-[28px] font-extrabold tracking-[-0.02em] sm:text-[36px]">About LearnCloud</h2>
            <p className="mt-5 text-[16px] leading-relaxed text-white/80">
              LearnCloud is built in Bulawayo for independent schools that cannot afford an
              enterprise system but need something steadier than registers, spreadsheets and
              WhatsApp groups.
            </p>
            <p className="mt-4 text-[16px] leading-relaxed text-white/80">
              It is made for schools of 150 to 2,000 learners, on the hardware and connections
              those schools actually have: modest laptops, and days when the power is out.
            </p>
            <div className="mt-8 flex flex-wrap gap-3">
              <a href={`${MARKETING}/about`} className="inline-flex min-h-touch items-center rounded-xl border-2 border-white/70 px-6 font-semibold text-white transition hover:bg-white hover:text-primary-900">
                More about us
              </a>
              <a href={`${MARKETING}/security`} className="inline-flex min-h-touch items-center rounded-xl border-2 border-white/70 px-6 font-semibold text-white transition hover:bg-white hover:text-primary-900">
                How we handle data
              </a>
            </div>
          </div>
          <div className="space-y-4">
            {promises.map(p => (
              <div key={p.title} className="rounded-2xl border border-white/15 bg-white/5 p-5">
                <h3 className="text-[16px] font-bold">{p.title}</h3>
                <p className="mt-1.5 text-[15px] leading-relaxed text-white/75">{p.body}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* Contact */}
      <section id="contact" className="scroll-mt-20 bg-gradient-to-br from-[#145ced] to-[#7933e8] px-4 py-20 text-white sm:px-6 lg:px-10">
        <div className="mx-auto max-w-4xl text-center">
          <h2 className="text-[28px] font-extrabold tracking-[-0.02em] sm:text-[36px]">Talk to someone who knows schools</h2>
          <p className="mx-auto mt-4 max-w-2xl text-[16px] leading-relaxed text-white/90">
            Tell us your learner count and what hurts at term-end. We answer on working days,
            in your time zone.
          </p>
          <div className="mt-9 flex flex-col items-center justify-center gap-3 sm:flex-row">
            <a href={`https://wa.me/${WHATSAPP}`} className="inline-flex min-h-touch w-full max-w-[280px] items-center justify-center rounded-xl bg-white px-7 py-3.5 font-semibold text-primary-800 shadow-lg transition hover:bg-white/90 sm:w-auto">
              WhatsApp us
            </a>
            <a href={`tel:${PHONE.replace(/\s/g, '')}`} className="inline-flex min-h-touch w-full max-w-[280px] items-center justify-center rounded-xl border-2 border-white/80 px-7 py-3.5 font-semibold text-white transition hover:bg-white/10 sm:w-auto">
              Call {PHONE}
            </a>
          </div>
          <p className="mt-6 text-[15px] text-white/85">
            Or email <a href="mailto:hello@learncloud.co.zw" className="underline underline-offset-4">hello@learncloud.co.zw</a>
            {' '}· <a href={`${MARKETING}/book-a-demo`} className="underline underline-offset-4">book a 20-minute demo</a>
          </p>
        </div>
      </section>

      <footer className="bg-primary-950 px-4 py-10 text-center text-[13px] text-white/60 sm:px-6">
        <a href="#home" className="font-semibold text-white/80 underline underline-offset-4 hover:text-white">Back to top</a>
        <p className="mt-3">© {new Date().getFullYear()} LearnCloud · Bulawayo, Zimbabwe</p>
      </footer>

      {/* Appears once the hero is behind you, so the way home is never more than one tap. */}
      <a
        href="#home"
        className={`fixed bottom-6 right-6 z-50 inline-flex min-h-touch min-w-touch items-center gap-2 rounded-full bg-gradient-to-r from-[#145ced] to-[#7933e8] px-5 text-[14px] font-semibold text-white shadow-2xl transition ${scrolled ? 'opacity-100' : 'pointer-events-none opacity-0'}`}
      >
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" aria-hidden="true">
          <polyline points="18 15 12 9 6 15" />
        </svg>
        Home
      </a>
    </div>
  )
}
