# UI/UX Audit - LearnCloud School Management System
**Auditor:** Senior UI/UX Designer (Enterprise SaaS)
**Date:** 2026-08-09 (Africa/Harare)
**Scope:** Every page - Navigation, Spacing, Typography, Colors, Consistency, Icons, Buttons, Cards, Tables, Forms, Dialogs, Loading, Empty, Animations, Responsive, Accessibility, Dark Mode, Professional Appearance
**Goal:** Premium enterprise software look, Bulawayo HQ, 150-2,000 learners, trusted for fees/reports/parents

---

## Executive Summary

System is **functional but inconsistent - feels like 12 prototypes stitched, not one premium product**.

**Strengths to keep:**
- Login skewed diagonal with `clip-path` + glow border `accent #844CAD` is distinctive, memorable, premium attempt - should be signature
- Setup wizard 9-steps with resume, autosave every step, progress saved, sensible defaults - excellent UX for time-to-first-invoice 45min
- Attendance register capture: 9-second 40 learners, one-handed 44-48px targets, bottom thumb zone, autosave 5s, unsaved indicator - good mobile pattern
- Fees: decimal(18,2)+currency handled, oldest-first allocation preview - clear mental model
- Marketing site: outcome headlines "Fees collected. Reports ready. Parents informed. Before lunch." + CSS mock screenshots (no heavy images) - strong
- Color palette defined: #0F153A primary must meet WCAG AA - generally used

**Overall Verdict:** NOT PREMIUM YET. Feels MVP. Needs design system tokens + component library before scaling to 50+ screens.

---

## Global Design System Assessment

### Colors - Medium Issue
**Palette spec:** #BC92CD, #844CAD, #5F3F96, #3C2C59, #0F153A, #195699, #307EC0 + secondary #171725, #073A69, #5A94C1, #77A6C0, #B4B6B8. Primary #0F153A must WCAG AA.

**Findings:**
- ✅ Primary #0F153A on white contrast 17.8:1 passes AA/AAA. Used as `bg-primary-800`.
- ❌ Secondary #BC92CD (light lavender) on white contrast 2.2:1 FAILS AA - used in glow-border, but text never uses it (good). However needs warning in tokens.
- ❌ Inconsistent primary shades: marketing uses `#0F153A`, login uses `#070A0F` near-black background, `#0B102C` 900 shade, `#070A1E` mock-topbar. 4 different darks - should be 1.
- ❌ Tailwind configs differ per file: 
  - `marketing-site/index.html`: primary 50:#F0F3FA, 100:#D9E0F2, 400:#5A94C1, 500:#307EC0, 600:#195699, 700:#073A69, 800:#0F153A, 900:#0B102C, 950:#070A1E
  - `LearnCloud.Web/tailwind.config.js`: only primary 50 and 800/900, secondary 500/600 - missing.
  - `Login_Skewed.html`: primary same but secondary 300:#BC92CD etc.
  - `Setup_Wizard.html`: only primary 50/800/900, secondary 500/600 - incomplete.
  - No single source of truth. `tailwind.config.js` should be canonical.
- ❌ Hardcoded colors: `style="background: radial-gradient(... #5F3F96 ...)"`, `bg-[#070A0F]`, `bg-warning-500` (no warning defined in some configs) - 20+ places.

**Recommendation (High):** Create `src/LearnCloud.Web/src/tokens/colors.js` + update `tailwind.config.js` to full palette per spec, enforce via lint rule no hardcoded hex except tokens.

### Typography - High Issue
- No type scale. Uses `text-2xl font-bold`, `text-3xl font-extrabold`, `text-xl font-bold`, `text-sm`, `text-xs`, `text-[10px]`, `text-[11px]` arbitrarily.
- Font family: marketing `system-ui, -apple-system, Segoe UI, Roboto, Inter, sans-serif` vs Web `system-ui, -apple-system, Segoe UI, Roboto` vs some pages no font defined.
- No line-height tokens: `leading-tight`, `leading-relaxed`, `leading-[1.05]` mixed.
- No font weight tokens: mixes `font-bold`, `font-semibold`, `font-extrabold`, `font-medium` without rule (enterprise uses 400, 500, 600, 700).
- Headings not semantic: Marketing uses h1 for hero, good, but fees module uses h2 for section then h3 for subsection but no h1 page title consistency. Teacher portal uses plain div for title.

**Recommendation:** Define `text-display (5xl extrabold)`, `h1 (3xl bold)`, `h2 (xl semibold)`, `body (sm regular)`, `caption (xs)`. Enforce.

### Spacing - Medium
- 8pt system claimed but p-4 (16px), p-3 (12px), p-2 (8px) mixed, but also `p-8 md:p-10` inconsistent. Gap 2,3,6 random.
- Touch target: `min-h-touch` (44px) defined as custom but used only in some mobile screens (attendance, teacher portal, parent portal). Marketing buttons use `min-h-touch` but header hamburger uses `w-11 h-11` (44px) good, but tables use `h-9` (36px) below touch.
- No layout grid: marketing `max-w-6xl px-4 sm:px-6 lg:px-8` good, but fees uses `p-4` only no max-width.

**Recommendation:** Enforce `spacing: 4=16px base, gap-4 grid`, `min-h-11` for all interactive.

### Consistency - Critical
**Worst offender:** No shared component library. Each module's Frontend/*.jsx imports React independently via UMD or Vite, defines own Button style.
- Example Button:
  - Login: `w-full h-12 rounded-full bg-primary-800 text-white text-sm font-semibold tracking-wide hover:bg-primary-900 active:scale-[0.98] shadow-lg shadow-primary-800/20 min-h-touch`
  - Fees: `px-6 py-3 rounded-lg bg-primary-800 text-white font-semibold`
  - Marketing: `inline-flex min-h-touch h-9 px-4 items-center rounded-md bg-primary-800 text-white text-sm font-medium`
  - Subjects: `px-4 py-2 rounded-lg bg-primary-800 text-white text-sm`
  - Teacher portal: `text-primary-800 bg-primary-50` etc.
  - 5 different heights, 3 radius, 3 font sizes for same primary action.

Same for Inputs, Cards, Tables.

**Recommendation (Critical):** Create `src/LearnCloud.Web/src/components/ui/{Button, Input, Card, Table, Badge, EmptyState, Skeleton}.jsx` with variants `primary | secondary | ghost`, sizes `sm | md | lg`.

### Icons - High
- Teacher portal bottom nav uses emoji: `🏠 👩‍🏫 ✓ 📝 📚 👤` - NOT premium, fails a11y, inconsistent rendering cross-platform.
- Parent portal empty state uses emoji `👨‍👩‍👧‍👦` - okay for illustration but not nav.
- Login uses custom inline SVG IconMail, IconLock, IconUser, IconSchool - decent but hand-drawn stroke-width 1.8 inconsistent with no library.
- Marketing uses no icons, only CSS dots.
- No icon library (Lucide, Heroicons, Phosphor) standardized.

**Recommendation:** Adopt Lucide React (small, consistent stroke 2). Replace emoji nav with `home`, `graduation-cap`, `clipboard-check`, `file-text`, `book-open`, `user`.

### Buttons - High
- No hierarchy: Every button is primary dark. No secondary (border), ghost (text), destructive (red).
- Destructive action (void invoice, remove grade) uses same primary style or `text-danger-500` small text - no confirmation dialog danger variant.
- Loading state: `disabled:opacity-60` or `disabled:opacity-50` or `disabled:bg-neutral-300` - 3 patterns. No spinner. Teacher portal shows "Saving..." text.
- No icon + label pattern.

**Recommendation:** Define Button variants per enterprise: Primary (solid #0F153A), Secondary (border), Ghost (text), Destructive (red 600), with loading spinner, disabled state, icon left/right.

### Cards - Medium
- Radius: `rounded-lg` (marketing, fees, subjects), `rounded-xl` (parent portal empty, platform admin), `rounded-[20px]` (login) - inconsistent.
- Shadow: `shadow-sm`, `shadow-md`, `shadow-lg`, `shadow-2xl`, `glow-border` custom - no elevation scale (0,1,2,3).
- Border: sometimes `border`, sometimes `border border-neutral-200`, sometimes no border.

**Recommendation:** Elevation scale: `card-default: bg-white border rounded-xl shadow-sm`, `card-hover: shadow-md`, `card-elevated: shadow-lg`.

### Tables - Critical
Pattern across SubjectsList, Fees Arrears, Transport Routes:
```jsx
<table className="w-full text-sm border-collapse">
  <thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1 text-left">...
```
**Issues:**
- `p-1` (4px) too tight for enterprise - should be `p-3` minimum.
- No column alignment tokens (left for text, right for money, center for status).
- No sortable header UI (SubjectsList has sort dropdown outside table, not clickable header).
- No pagination component (Prev/Next buttons plain border, no page size selector, no total info consistent).
- No sticky first column on mobile horizontal scroll.
- Money columns not right-aligned: Fees arrears balance `font-bold` but left? Should be right-aligned.
- No row hover/selected state consistent: some `hover:bg-primary-50`, some no hover.
- No empty state inside table: Subjects shows "Loading server-side..." as row, Fees shows nothing when no routes.
- No skeleton loading: plain text "Loading..."
- Horizontal scroll no hint: on mobile, table overflows but no shadow indicator.

**Recommendation (High):** Build Table component with `<Table><TableHeader><TableRow><TableHead sortable><TableBody><TableCell align="right">`, EmptyState, Skeleton rows.

### Forms - Critical
- **Input variants:**
  - Login: `underline-input` custom `border: none; border-bottom: 1.5px solid #E9ECEF` - unique, floating label missing, placeholder only (a11y fail, no label).
  - Others: `h-10 px-2 border rounded` or `h-11 px-3 rounded-md border` - 2 heights.
- **Labels:** Many use `<span className="text-sm">Name</span>` inside label but no `htmlFor`, no required indicator consistent (`*` red in some, not in others). Setup wizard marks required with red `*` via `<span className="text-red-600">*</span>` good, but fees forms have no required.
- **Validation:** No error UI, only `alert("Fill required")` or `alert("Failed: "+await res.text())` - uses browser alert (not premium).
- **Help text:** Some have `text-xs text-neutral-500` help, some not.
- **No form grouping:** Fees form 5 inputs in grid but no fieldset, no section headings.

**Recommendation (Critical):** Form component: Label + Input + Error + Help, with `aria-describedby`, required *, validation states.

### Dialogs/Modals - High
- **None exist.** All confirmations via `alert()` or `confirm()`? Code shows `alert("Login: "+JSON.stringify(...))`, `alert("Fee structure created")`, `alert("Payment recorded...")`, `alert("Message sent")`.
- No modal, no drawer, no confirmation dialog for destructive (remove grade, skip wizard step, void invoice).
- Enterprise needs Dialog with overlay, focus trap, Esc close, ARIA.

**Recommendation:** Create Dialog component `Dialog, DialogContent, DialogHeader, DialogFooter` with variants.

### Loading Screens - Medium
- Many `if(loading) return <div className="p-8 text-center">Loading...</div>` plain text.
- Attendance shows saving overlay? No, only button text "Saving...".
- Setup wizard: "Loading wizard progress... (resume support)" - not skeleton.
- No global loading bar (NProgress) or skeleton for dashboard.

**Recommendation:** Skeleton component: `SkeletonCard`, `SkeletonTable`, `SkeletonList`. Use brand color pulse.

### Empty States - Medium
- Parent portal no children: good illustration (centered card, emoji, explanation, actionable contact) - best empty state in system.
- SubjectsList: no empty state, shows "Loading server-side..." even when zero.
- Fee structures: shows empty div, no call to action.
- Attendance: no students? Shows empty p-2 space.
- Transport: no empty, just empty table.

**Recommendation:** EmptyState component with illustration (SVG), title, description, primary CTA, secondary action.

### Animations - Low-Medium
- Login has `transition-opacity duration-[600ms] ease-in-out` + `cubic-bezier(0.65,0,0.35,1)` for diagonal slide - nice but 600ms borderline long for enterprise (should be 300-400ms). No `prefers-reduced-motion` media query.
- Attendance unsaved indicator `animate-pulse` - good but no reduced motion.
- Glow border has static shadow, no subtle hover.
- No page transition, no micro-interactions for buttons (only `active:scale-[0.98]` and `active:scale-95` mixed).

**Recommendation:** Motion tokens: duration 150, 250, 350ms, ease `ease-out`, respect `prefers-reduced-motion: reduce { animation: none }`.

### Responsive - High
- Marketing header: mobile hamburger `☰ ✕` text characters, not icon. Menu appears `md:hidden` but `md:flex` for nav - okay. However mobile menu uses `block py-2` not full width touch.
- Login diagonal: `@media (max-width: 768px){ .diagonal-left{clip-path: polygon(0 0, 100% 0, 100% 86%, 0 100%)} .diagonal-right{clip-path: polygon(0 14%, 100% 0, 100% 100%, 0 100%)}}` - changes diagonal to horizontal on mobile, okay but not tested for 320px width (forms may overflow).
- Attendance register: fixed bottom bar `fixed bottom-0 left-0 right-0 bg-white border-t p-3 flex gap-2 safe-bottom` good for thumb zone, but top sticky header has two rows of filters that may wrap on 320px.
- Teacher portal bottom nav: `fixed bottom-0 flex justify-around py-1 safe-bottom` with `min-w-touch min-h-touch` good, but 6 items on 320px may be cramped - need scroll or 4+more.
- Fees tables: `overflow-auto border rounded mt-2 max-h-96` but no horizontal scroll hint, no sticky column. On mobile, table will be unreadable.
- Subjects filter: `grid sm:grid-cols-4 gap-3` collapses to 1 col on mobile - good.
- Setup wizard: `grid lg:grid-cols-[200px_1fr]` and later `lg:grid-cols-[220px_1fr]` inconsistent, sidebar sticky `top-24` but on mobile becomes top, not collapsible.

**Recommendation:** Test 320, 375, 768, 1024, 1440. Add mobile table pattern: cards on mobile, table on desktop.

### Accessibility - Critical
- Focus: `:focus-visible{outline:none;box-shadow:0 0 0 3px rgba(132,76,173,.35)}` defined but Login `underline-input:focus{border-bottom-color:#5F3F96; outline:none; box-shadow:none}` overrides removing focus - fails WCAG 2.4.7.
- Labels: Login inputs have placeholder only, no `<label>` - fails 3.3.2.
- Icons: custom SVG icons have no `aria-hidden`, no title. Emoji nav has no `aria-label`.
- Color contrast: Primary #0F153A on white passes, but `text-neutral-500` (#5C5F66?) on white? Check: #B4B6B8 on white fails. Many `text-neutral-500` used for help - likely fails.
- Keyboard: Attendance cycle button has status letter P/A/L but screen reader reads "P present"? No `aria-label` with full status. Marks grid claims Tab/Enter/Arrows but not implemented in JSX (only note).
- No skip link.
- No `prefers-reduced-motion`.
- No `aria-live` for autosave "Unsaved changes — autosave in 5s" - should be live region.
- Forms: no `autocomplete` in many (fees studentId etc). Login has autocomplete good.

**Recommendation (Critical for public sector schools):** Fix focus-visible, add labels, aria-labels for status buttons, right contrast, keyboard nav for tables, live regions.

### Dark Mode Compatibility - Low (Future)
- Currently no dark mode. Login uses dark `#070A0F` background but forms white - mixed. Marketing is light only.
- Tokens include dark shades (#070A1E, #0B102C) but no `dark:` variant.
- Glow border assumes dark bg.

**Recommendation:** For now ensure light mode is perfect. Add CSS variable tokens for future dark mode, don't implement full dark yet - low priority.

### Professional Appearance - High
**Current perception:**
- Marketing 12-pages SPA with outcome headlines feels **premium-ish** but heavy inline JS (69010 lines single HTML) is not maintainable - violates separation of concerns from backend audit H1.
- Login skewed diagonal feels premium experimental, but crossfade duplication (left side has both login+register crossfading, right side duplicate) is confusing code, could be simplified.
- Setup wizard feels enterprise (progress bar, sidebar, resume, sensible defaults, next 3 actions) - best UX flow.
- Fees/Subjects/Attendance feel **internal tool** not enterprise: raw borders, `p-1`, no card elevation, alert() - looks like prototype.

**Gap to premium enterprise (Salesforce, Linear, Stripe Dashboard):**
- No app shell (sidebar navigation with collapsed, topbar with search, command palette ⌘K)
- No data visualization consistent (arrears, attendance %)
- No empty/skeleton polish
- No consistent radius/shadow
- No microcopy tone (some "Fees collected. Reports ready." good, but "Fill required" bad)

---

## Page-by-Page Audit

### 1. Marketing Landing (marketing-site/index.html - 12 routes in one 69K HTML)
- **Navigation:** Sticky header with backdrop-blur good, but mobile hamburger uses text ☰ ✕ not icon, no animation. Footer has 4 columns good. SPA routing via intercept click + pushState good but no focus management after navigation (a11y).
- **Spacing:** Hero grid `lg:grid-cols-[1.1fr_0.9fr] gap-8` good. But sections alternate bg-white / bg-neutral-50 with border-b but inconsistent padding `py-12 sm:py-20` vs `py-14`.
- **Typography:** Hero h1 `text-3xl sm:text-5xl font-extrabold tracking-tight leading-[1.05]` good, but features page h2 `text-2xl font-bold` smaller than expected hierarchy.
- **Colors:** Uses full token palette, but many inline `style:{background:"#F87171"}` for mock dots.
- **Tables:** Pricing comparison table has overflow-auto but no sticky header on mobile, no zebra.
- **Forms:** Book demo form has `text-[16px]` to prevent iOS zoom (good) but other inputs use default 14px.
- **Dialogs:** None.
- **Loading:** No skeleton, demo request shows loading via button disabled.
- **Empty:** Blog index has 3 posts hard-coded, no empty.
- **Responsive:** Grid responsive good, but mock screenshots `h-32 bg-neutral-100` may be too small on mobile.
- **Accessibility:** Focus ring defined, but interactive mock screenshots have `role="img" aria-label` good. However pricing table no `scope`.
- **Dark:** No.

**Priority fix:** Extract to Vite + component library, remove 69K inline HTML. **P1 - Foundation.**

### 2. Login Skewed (LearnCloud_Login_Skewed.html - signature page)
- **Navigation:** No nav, okay for login.
- **Spacing:** `w-full max-w-[900px]` container `h-[560px] md:h-[520px]` fixed height may clip on small viewport. Padding `p-8 md:p-10` okay but form inner `max-w-[300px]` narrow.
- **Typography:** `text-2xl font-bold tracking-tight` for Login/Register, good. But panel "Welcome back!" `text-3xl font-extrabold` larger than form title - intentional focal shift good.
- **Colors:** Near-black `#070A0F` bg with radial blobs `blur-[120px] opacity-20` premium, glow-border `box-shadow: 0 0 0 1px rgba(188,146,205,0.25), 0 0 30px ...` premium, gradient `from-[#0F153A] via-[#5F3F96] to-[#307EC0]` uses brand.
- **Consistency:** Unique design not reused elsewhere - inconsistency but intentional as marketing hook.
- **Icons:** Custom SVG 18x18 stroke 1.8 okay.
- **Buttons:** Pill `rounded-full` vs rest of system `rounded-lg` - inconsistent but premium for login.
- **Forms:** Underline-only inputs `border-bottom 1.5px` distinctive, but placeholder only no label (a11y fail), trailing icon `pointer-events-none` good but low contrast `text-neutral-400`.
- **Dialogs:** No error dialog, uses alert mock.
- **Loading:** `disabled:opacity-60` + text "Signing in..." no spinner.
- **Animations:** 600ms slide with `cubic-bezier(0.65,0,0.35,1)` smooth but long, crossfade 600ms opacity for forms good but duplicated DOM (both left and right have login+register) - over-engineered.
- **Responsive:** Clip-path changes to horizontal at 768px, but fixed height `h-[560px]` may cause scroll on 320px height.
- **Accessibility:** Focus removes box-shadow (bad), no label, no error announcement.
- **Dark:** Already dark bg, but form white - good contrast.

**Priority fix:** Keep visual, fix a11y (labels, focus), add error states, unify with design system tokens, reduce DOM duplication, make Vite component. **P0 - First impression, highest business impact.**

### 3. Setup Wizard (SetupWizard.jsx + Setup_Wizard.html)
- **Navigation:** Progress header sticky `top-0 z-20` with 9 dots `h-2 flex-1 rounded-full` good, sidebar `sticky top-24` steps nav good, but mobile not collapsible.
- **Spacing:** `max-w-6xl px-4 py-6 grid lg:grid-cols-[220px_1fr] gap-6` good.
- **Typography:** Step title `text-xl font-bold` good, but form labels `text-sm font-medium` inconsistent.
- **Forms:** Many inputs `h-11 px-3 rounded-md border` 44px touch good, but color inputs `h-11 rounded-md border` (color input needs larger). File input unstyled.
- **Cards:** Each step form not card, just space-y-4.
- **Tables:** Classes & Streams uses `inline-flex ... rounded-full bg-primary-50 border` pills for streams - creative but not table.
- **Loading/Empty:** Loading text, no skeleton.
- **Responsive:** Sidebar stacks to top on mobile? Grid `lg:` means 1 col mobile - okay but long scroll.
- **Accessibility:** No fieldset legend for groups, no required announcement.

**Priority:** Good UX, polish needed but functional. **P2.**

### 4. SubjectsList (SubjectsList.jsx)
- **See Tables section - critical.** `p-1` tight, no empty, no sort UI, pagination plain Prev/Next, export button small `text-xs`.
- **Priority:** P1 - core records foundation.

### 5. Fees (FeesScreens.jsx 4 tabs)
- **Navigation:** Tabs `structures/generate/payments/arrears` simple buttons `px-3 py-1.5 rounded border` with active `bg-primary-800 text-white` but no underline indicator, no icon.
- **Forms:** Fee item add row grid `sm:grid-cols-5 gap-2` with 5 inputs + button but no labels on mobile, placeholder only.
- **Tables:** Arrears tables `max-h-96 overflow-auto` with `text-xs` too small for money, `bg-danger-50` for >60days good visual but no legend.
- **Loading:** Batch polling every 2s with progress % good.
- **Empty:** No.

**Priority:** Money module - highest risk, needs premium. **P1.**

### 6. AttendanceRegisterCapture
- **Strengths:** Bottom thumb zone, large tap targets `w-12 h-12`, status color mapping `present: bg-success-500` etc good, unsaved pulse, backdated flag `bg-warning-100`.
- **Issues:** Color only for status (P/A/L) plus tiny `text-[10px]` - colorblind fails. Needs icon + text + color. Reason inputs `w-24` + `flex-1` but placeholder no label. No empty for filtered "unmarked" 0 results.
- **Priority:** Already good, refine for a11y. **P2.**

### 7. Teacher Portal (TeacherPortal.jsx)
- **Navigation:** Bottom nav 6 items with emoji icons - NOT PREMIUM, feels like prototype. No top search. Sticky top `bg-primary-800 text-white` good but no breadcrumbs.
- **Spacing:** `pb-20` for bottom nav good, but `p-3 space-y-3` tight.
- **Cards:** Dashboard cards not defined in snippet but likely similar.

**Priority:** Teacher daily use - high impact. **P1 - fix nav icons.**

### 8. Parent Portal (ParentPortal.jsx)
- **Navigation:** Child switcher select good, but bottom nav not shown snippet. Header `bg-white border-b` simple.
- **Empty:** Best empty state with illustration, title, description, actionable - keep pattern.
- **Cards:** Home per child: balance, attendance %, latest results etc - need cards.

**Priority:** Least technical users - P1.

### 9. Platform Admin Console (PlatformAdminConsole.jsx)
- **Navigation:** Tabs plus 2FA gate - good security UX. Impersonation banner needed.
- **Colors:** `bg-primary-950 text-white` dark for admin distinction good.

**Priority:** Internal, P3.

### 10. Transport, Library, Messaging etc
- Similar inconsistent tables/forms as fees.
- Library fast issue/return: barcode scanner detection (fast typing <50ms) clever, but input auto-focus only, no error dialog.

**Priority:** P3.

---

## Prioritized Improvement List (Most to Least Important)

### P0 - Critical, Blocks Premium Perception (Fix First)
1. **Design System Tokens & Component Library** - Create single source Tailwind config + `ui/` components: Button (primary/secondary/ghost/destructive + loading spinner), Input (label/error/help), Card (elevation), Table (sortable header, pagination, empty, skeleton), Badge, EmptyState, Skeleton, Dialog. Replace all ad-hoc button/table/input styles with library. Effort 2-3 days, impacts all pages.

2. **Login Skewed Page - A11y & Premium Polish** - Keep diagonal signature but: add `<label>` SR-only, fix focus-visible (restore box-shadow), add error state (inline + aria-live), add loading spinner in button, replace duplicated DOM crossfade with single source, convert to Vite React component `src/pages/LoginSkewed.jsx`, ensure 320px height no clip, add autocomplete, add "Show password" toggle, replace custom underline-input with token-based Input variant. First impression = trust for 14-day trial.

3. **App Shell - Premium Enterprise Navigation** - Build unified shell: sidebar collapsible (logo + nav groups: Academic, Fees, Attendance, et al, bottom user), topbar (search/command palette ⌘K, notifications, tenant switcher), breadcrumbs, page header (title + actions). Replace per-module ad-hoc headers. Applies to Subjects, Fees, Attendance etc.

4. **Forms Unified + Validation + Error Dialogs** - Replace alert() with Dialog + inline errors. Add Form component with label, required *, error, help, autocomplete. Implement Zod/FluentValidation frontend.

### P1 - High, Direct Usability / Money Risk
5. **Tables Enterprise Pattern** - For SubjectsList, Fees Arrears, Transport Routes: padding p-3, right align money, sortable headers with arrow, pagination with size selector, empty state with CTA, skeleton loading, horizontal scroll shadow, sticky header, row hover, selected.

6. **Teacher Portal Navigation Icons** - Replace emoji with Lucide: `home, users, clipboard-check, file-text, book-open, user`. Add labels, active state `bg-primary-50 text-primary-800` kept but with SVG.

7. **Fees Module Polish (Money = Trust)** - Money right-aligned, currency badge, subtotal calculation display note "calculated via service", allocation preview with credit held as orange badge, print styles, confirmation dialog for void, receipt dialog.

8. **Parent Portal Cards + Readability at Arm's Length** - Ensure 16px base, 20px headings, big tap targets, child switcher prominent, balance as large number, status colors with icons.

9. **Accessibility Pass** - Focus rings everywhere, labels, aria-label for P/A/L buttons `aria-label="Mark Thabo Ndlovu as Present"`, live regions for autosave, contrast check for neutral-400/500 on white, keyboard nav for table.

### P2 - Medium, Polish
10. **Setup Wizard Polish** - Sidebar mobile collapsible drawer, file upload styled, color picker preview, progress steps clickable with checkmark, completion confetti? Keep sensible defaults.

11. **Attendance A11y + Colorblind** - Add icon + text beyond color: P with check, A with x, etc. Add pattern or border.

12. **Loading Skeletons** - Replace "Loading..." with SkeletonTable 5 rows, SkeletonCard.

13. **Empty States Standardized** - Use Parent Portal pattern across all modules.

14. **Spacing & Typography Scale** - Enforce 8pt, type scale, remove `text-[10px]`.

### P3 - Low, Future
15. **Animations & Reduced Motion** - Motion tokens, respect prefers-reduced-motion.
16. **Dialogs for Destructive** - Replace all alert() with Dialog.
17. **Dark Mode Strategy** - Define tokens, not implement now.
18. **Icons Library** - Lucide across.
19. **Responsive Tables -> Cards on Mobile** - For Fees Arrears, show cards on <640px.

---

## Recommendation: Fix Order - One Page at a Time

**Do NOT redesign everything.**

**Fix Order:**
1. **Login Skewed Page** (P0) - signature, first impression, 14-day trial conversion
2. **Design System Foundations + App Shell** (P0) - enables other pages
3. **Subjects List** (P1) - core records template for all list pages
4. **Fees Screens - Arrears List** (P1) - money trust
5. **Attendance Register Capture** (P2) - a11y polish
6. **Teacher Portal** (P1) - daily use
7. **Parent Portal** (P1) - least technical
8. **Setup Wizard** (P2) - onboarding
9. **Remaining modules** (P3)

**For this audit cycle, I will fix ONE page: Login Skewed Diagonal.**

Proposed fix for Login:
- Keep 900px max, glow border, radial blobs, diagonal-left/right slide, gradient from #0F153A via #5F3F96 to #307EC0
- Create React component `LoginSkewed.jsx` in Vite with:
  - Proper <label> SR-only + placeholder + autocomplete
  - Focus-visible restored (3px ring secondary-500)
  - Error state inline with aria-live
  - Loading spinner (svg spin) in button, disabled state
  - Show password toggle
  - Replace duplicated crossfade DOM with single state-driven panel + CSS transform (not duplicate forms)
  - Replace custom underline-input with Input component using tokens
  - Add "Forgot password?" dialog (not alert)
  - Ensure 320px width/height responsive, no fixed h-[560px] clip
  - Replace custom Icon* with Lucide icons (Mail, Lock, User, School)
  - Add prefers-reduced-motion support
  - Add WCAG AA contrast: placeholder neutral-400 (#B4B6B8) fails, use neutral-600 (#5C5F66) for placeholder? compromise.
  - Add form validation (email format, password min 8)

If approved, I will implement this single page and present for review before next page.

---

## Appendix: Professional Appearance Checklist (Enterprise)

- [x] Logo placeholder LC
- [ ] Logo real SVG
- [x] Primary #0F153A meets AA
- [ ] No hardcoded colors
- [ ] Button hierarchy
- [ ] Table enterprise pattern
- [ ] Empty states
- [ ] Skeletons
- [ ] Dialogs not alert
- [ ] Icons SVG not emoji
- [ ] App shell sidebar+topbar
- [ ] Breadcrumbs
- [ ] Page header with actions
- [ ] Search/command palette
- [ ] Focus visible
- [ ] Labels
- [ ] 44px touch targets everywhere
- [ ] Print styles A4 greyscale
- [ ] No alert()

**Score: 4/18 enterprise readiness - needs work but good bones.**

---
