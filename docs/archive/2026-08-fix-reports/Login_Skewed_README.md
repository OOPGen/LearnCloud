# LearnCloud Login Screen — Diagonal Skewed Divider, Sliding Gradient Panel

**Spec:** Single centred card on near-black background (#070A0F), split into two halves with **diagonal (skewed) divider**, not straight vertical. One half solid brand-gradient panel with welcome heading and short line; other half holds form. Toggling Login/Register **slides gradient panel horizontally to opposite side over 600ms ease-in-out**, while forms crossfade. Card has soft glowing border in accent colour #844CAD. Inputs underline-only (bottom border, no box) with trailing icon, submit button full-width pill.

## File

- `LearnCloud_Login_Skewed.html` — self-contained React + Tailwind CDN, 1 file, no build step, 44px min touch, focus rings, accessible labels, near-black background with subtle blurred blobs.

## How Diagonal Divider Works

- Gradient panel width 55% (slightly more than 50% to cover diagonal overlap) absolute positioned
- Left state (Login): `left:0` + `clip-path: polygon(0 0, 100% 0, 86% 100%, 0 100%)` — diagonal slant to right at bottom
- Right state (Register): `left:45%` (45% + 55% = 100%) + `clip-path: polygon(14% 0, 100% 0, 100% 100%, 0 100%)` — diagonal slant to left at top
- Transition: `transition-all duration-[600ms] ease-in-out` with cubic-bezier(0.65,0,0.35,1) — slides horizontally opposite side
- Card overflow-hidden keeps diagonal clean

## Glowing Border

- `.glow-border { box-shadow: 0 0 0 1px rgba(188,146,205,0.25), 0 0 30px rgba(132,76,173,0.35), 0 0 70px rgba(48,126,192,0.25), 0 20px 60px rgba(0,0,0,0.5) }`
- Accent colour #844CAD medium purple, #BC92CD light lavender, #307EC0 bright blue from brand palette
- Border radius 20px

## Inputs — Underline-only

- Class `.underline-input { background: transparent; border: none; border-bottom: 1.5px solid #E9ECEF; border-radius:0; transition border-color }`
- Focus border-bottom #5F3F96 secondary-600
- Trailing icon absolute right, group-focus-within:text-secondary-600, pointer-events-none, 18px SVG mail/lock/user/school
- Placeholder text-sm neutral-400, h-48px min touch

## Submit Button — Full-width Pill

- `w-full h-12 rounded-full bg-primary-800 #0F153A text-white text-sm font-semibold tracking-wide hover:bg-primary-900 active:scale-[0.98] shadow-lg shadow-primary-800/20 min-h-touch 44px`
- Full-width pill per spec

## Toggling & Crossfade

- isLogin boolean state
- Gradient panel slides left 0 ↔ left 45% over 600ms ease-in-out
- Forms crossfade: two divs absolute inset, opacity 100% vs 0% with pointer-events-auto vs none, transition-opacity duration 600ms ease-in-out
- Left side holds Login when isLogin, Register when !isLogin; right side holds opposite for mirrored crossfade, so both sides crossfade
- Gradient panel inner content also crossfades: Welcome back! vs Hello, friend! with short line, and button to toggle

## Content

- Login panel: "Welcome back!" + "Sign in to continue to LearnCloud — fees collected, reports ready, parents informed before lunch. Built in Bulawayo for schools 150–2,000 learners." + button Create school account
- Register panel: "Hello, friend!" + "Enter your school details and start your 14-day trial — no card, clear pricing, data never deleted. Setup wizard 9 steps, time-to-first-invoice 45 min." + button Sign in

- Login form: Email + Password, forgot link, Login pill button, Don't have account? Register link toggles
- Register form: School name, Contact name, Email, Password, Create account pill button

## Accessibility & Performance

- Near-black background #070A0F with subtle radial blobs opacity 10-20% no heavy images
- Contrast: gradient panel white text on #0F153A #5F3F96 #307EC0 passes AAA, form neutral-800 #171725 on white 15.8:1
- Focus rings 3px rgba(132,76,173,.35)
- Min touch 44px, keyboard Tab/Enter, labels, autocomplete email current-password new-password
- Fast: Tailwind CDN, React UMD production 42KB gz, no heavy libs, 1 file

## Integration

- For real app, replace alert with fetch to /api/auth/login and /api/auth/register-tenant
- Use same component as src/LearnCloud.Auth/Frontend/LoginSkewed.jsx with Tailwind already configured primary #0F153A secondary #5F3F96
- Same screens reachable from Settings? No, login is entry point.

## Marketing Website

Full site already built in `/marketing-site/index.html` 68KB SPA:

- Pages: home, features (sections per module real screenshots CSS mock windows), pricing (three plans Starter $0.50 min $99 300, Growth $1.00 min $149 800 most chosen, Scale $2.00 min $199 2000 + comparison table + FAQ billing with JSON-LD FAQPage), about, contact, book-a-demo, blog index + post template, privacy, terms, security, help centre
- Headlines outcome not feature, pricing clear what school pays how billing works trial explained, demo booking form school name contact role learner count current system posting to CRM/inbox endpoint configurable, fast on slow connection optimized images CSS mocks no heavy libs, SEO unique title/meta per page semantic headings OG Twitter sitemap robots.txt structured data organization + FAQ, accessible contrast focus keyboard alt text, analytics conversion tracking gtag event demo_booking, copy confident plain non-hyped avoid revolutionary cutting-edge seamless empower.

Both deliverables ready.

