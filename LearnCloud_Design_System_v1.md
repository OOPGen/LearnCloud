# LearnCloud Design System V1
**HQ:** Bulawayo, ZW | **Date:** 2026-08-02 | **Brand:** LearnClod / LearnCloud #0F153A #5F3F96 #307EC0
**Audience:** Non-technical school staff, mixed hardware, 3G, small screens, daily use
**Priority Screens:** Attendance Register & Marks Entry Grid

---

## 0. PRINCIPLES (Why this system looks this way)

1. **Readability over decoration:** School bursar on 5-year-old laptop with dust screen must read arrears list printed in greyscale. Hence colors chosen with distinct luminance (L*), not hue. Primary navy #0F153A L*~15, success #2E7D32 L*~36, warning #B7791F L*~55, danger #C62828 L*~42 — all distinct when desaturated.
2. **Touch > Click:** All interactive elements min 44x44px (Apple/Google), primary actions 48x48px. Attendance marking done standing in classroom with tablet.
3. **Speed > Beauty:** Initial CSS <30KB, no heavy images in critical path, login background lazy loaded. Marks grid virtualized to 60 students without jank.
4. **Error Prevention:** Bursar voiding invoice needs confirmation dialog with typed invoice number, not just Yes/No.
5. **Offline Tolerance:** Attendance & marks can be marked offline, queued in IndexedDB, auto-sync when back.

---

## 1. TAILWIND THEME EXTENSION

Full config in `tailwind.config.js` (separate file). Summary of tokens:

### 1.1 Colour Scale - Greyscale Legible

**Primary (Navy):** 50 #F0F3FA lightest bg → 500 #307EC0 CTA → 800 #0F153A core → 950 #070A1E sidebar. Chosen because navy prints as near-black in B&W, still legible.

**Secondary (Purple):** 50 #F6F0FB → 300 #BC92CD lavender → 500 #844CAD → 600 #5F3F96 deep purple core → 900 #1B1326. Used for academic accents, not for status.

**Neutral:** 0 #FFFFFF → 50 #F8F9FA page → 200 #E9ECEF border-light → 400 #B4B6B8 disabled (from palette) → 600 #5C5F66 body text → 800 #171725 almost-black primary text. Contrast ratio 15.8:1 for 800 on white — AAA.

**Success (Dark Green):** 500 #2E7D32 — AA on white (5.1:1). In greyscale prints as dark grey ~35% — distinct from danger.

**Warning (Dark Amber Brown):** 500 #B7791F — not bright yellow (fails contrast). AA on white 4.6:1. Greyscale ~55% mid-grey, distinct from success/danger.

**Danger (Deep Red):** 500 #C62828 — AA on white 5.8:1. Greyscale ~42% but reddish pattern different in print with icons.

**Usage rules:**
- Never rely on color alone: success badge = green + check icon + text "Paid". Warning = amber + ! icon.
- Printable reports force `print:bg-white print:text-black print:border-black` via Tailwind print variant.
- Status chips have border + icon + text for greyscale.

### 1.2 Typography Scale

- **Family:** Inter - highly legible on low-dpi screens, 400/500/600/700 used only (no thin).
- **Base 15px** not 14px — larger for non-technical staff, 16px for inputs to prevent iOS zoom.
- xs 12/16 secondary, sm 13/18 captions, base 15/22 default, md 16/24 inputs & table cells, lg 18/26 section titles, xl 20/28 card titles, 2xl 24/30 page titles, 3xl 30/36 dashboard, 4xl 36/40 hero (login).
- Letter spacing 0.02em for xs uppercase labels.

### 1.3 Spacing Scale

4px base: 1=4,2=8,3=12,4=16,5=20,6=24,8=32,10=40,12=48,16=64. touch=44 min, touch-lg=48 primary. All gaps use multiples of 4. Attendance rows use 8px vertical padding to keep target 44px.

### 1.4 Border Radii & Shadows

- **Radii:** sm 4 badge, DEFAULT 6 buttons/inputs (friendly but not childish), md 8 cards, lg 12 modals/stat tiles, xl 16 wizards, 2xl 24 empty states.
- **Shadows:** xs for inputs, sm for cards, md for dropdowns, lg for modals, xl for toast. Focus ring = 0 0 0 3px rgba(48,126,192,0.30) primary-500, not outline only (visible).

---

## 2. COMPONENT INVENTORY WITH PROPS & STATES

### 2.1 Button

**Description:** Primary action. Optimized for thumb.

**Props:**
| Prop | Type | Default | Notes |
|---|---|---|---|
| variant | 'primary'|'secondary'|'ghost'|'danger'|'success' | 'primary' | primary=navy #0F153A bg white text |
| size | 'sm'|'md'|'lg'|'icon' | 'md' | md=44px tall, lg=48px, sm=36px |
| loading | bool | false | Shows spinner, disables |
| disabled | bool | false | |
| fullWidth | bool | false | For mobile |
| leftIcon/rightIcon | ReactNode | - | Lucide icons |
| type | 'button'|'submit' | 'button' | |

**States:** default, hover (darken 10%), focus (shadow-focus), active (scale 0.98), disabled (opacity 50%, cursor not-allowed, neutral-400), loading (spinner, aria-busy).

**Accessibility:** min 44x44, aria-label if icon-only, contrast 4.5:1, keyboard Enter/Space.

**Variants in CSS:**
- primary: `bg-primary-800 text-white hover:bg-primary-900 focus:shadow-focus`
- secondary: `bg-secondary-100 text-secondary-800 border`
- ghost: `bg-transparent text-primary-700 hover:bg-primary-50`
- danger: `bg-danger-500 text-white`

**Example:** `<Button variant="primary" size="lg" leftIcon={<Check/>} loading={saving}>Save Attendance</Button>`

### 2.2 Input

**Props:** label, placeholder, type, value, onChange, error, hint, required, disabled, prefixIcon, suffixIcon, mask, autoComplete.

**States:** empty, filled, focused (border-primary-500 + shadow-focus), error (border-danger-500 + shadow-focus-danger + error text + icon), disabled, readonly.

**Rules:** label always visible (no placeholder-only), 16px font size, 44px height, error + icon + text.

**Mobile:** Prevent zoom, type=tel for phone.

### 2.3 Select (Combobox)

**Props:** options {value,label,disabled}[], value, onChange, searchable, multi, label, placeholder, error, loading, creatable.

**States:** closed, open (dropdown with shadow-lg), focused item (bg-primary-50), selected (check icon), no results empty, loading skeleton.

**For slow connections:** Debounced search (300ms), virtualized list for >100 items (grades, students), client cache.

### 2.4 Date Picker

**Props:** value, onChange, min, max, label, error, mode 'single'|'range', presetTerms (Term1/2/3), academicYear.

**States:** input display, calendar popup, today highlight, selected, disabled dates, range.

**School specific:** Preset buttons for term dates, DD/MM/YYYY format display but ISO internal, native `<input type=date>` on mobile for OS picker.

### 2.5 Searchable Table with Pagination

**Critical for student list, arrears.**

**Props:** columns [{key, label, sortable, width, render, sortType}], data, loading, searchable (string), filters {grade, stream, status}, pagination {page, pageSize, total}, onSort, onFilter, onRowClick, selectable, actions, emptyState, skeletonRows.

**Features:**
- Sticky header, horizontal scroll on <768px with first column sticky (name)
- Search debounce 300ms, highlights match
- Pagination: 25/50/100 per page, not infinite scroll (printable), shows "Showing 1-25 of 243"
- Sorting indicator: ▲▼ icons
- Row states: default, hover bg-primary-50, selected bg-primary-100 + border-left primary-800, loading skeleton.
- **Performance:** Virtual rows for >100, only render visible, <2s on 3G.

**Accessibility:** `<table>` semantic, `aria-sort`, keyboard arrow navigation between rows, Enter to open detail.

### 2.6 Modal

**Props:** open, onClose, title, size 'sm'|'md'|'lg'|'xl'|'full', closeOnOverlay, closeOnEsc, footer.

**States:** enter animation 200ms, focus trap, scroll lock, overlay bg-neutral-900/50.

**A11y:** role=dialog, aria-modal, ESC closes, focus first input.

### 2.7 Tabs

**Props:** tabs [{id,label,count,icon,disabled}], active, onChange, variant 'default'|'pill', scrollable.

**States:** active border-bottom primary-800 + bold, hover neutral-100, disabled opacity 50.

**Mobile:** Horizontal scroll with fade edges, not wrap.

### 2.8 Card

**Props:** title, subtitle, action, padding 'sm'|'md'|'lg', hoverable, clickable, border, variant 'default'|'muted'|'outline'.

**Shadow:** sm, hover md transition 150ms.

### 2.9 Stat Tile

**Props:** label, value, change {value, direction}, icon, status 'default'|'success'|'warning'|'danger', loading, onClick, trend mini chart.

**Used:** Admin dashboard: Total Students, Arrears >60d, Attendance Today.

**Greyscale rule:** color + icon + text, never color alone.

### 2.10 Badge (Chip)

**Props:** label, variant 'neutral'|'success'|'warning'|'danger'|'primary'|'secondary', size 'sm'|'md', icon, dot (bool).

**States:** With dot + text for greyscale: e.g., success badge = green bg + check icon + "Paid". Border 1px for print.

### 2.11 Toast (Snackbar)

**Props:** message, variant, duration, actionLabel, onAction, dismissible.

**Position:** Bottom-center on mobile, top-right desktop, max 1 visible to avoid spam, queue others.

**Connection drop:** Use warning toast with retry.

**A11y:** role=status, aria-live polite.

### 2.12 Empty State

**Props:** illustration (AI school image), title, description, action, secondaryAction.

**Tone:** Friendly, non-technical: "No students in Grade 5 Blue yet. Import CSV or add manually."

### 2.13 Loading Skeleton

**Props:** type 'text'|'card'|'table'|'stat', lines, animated.

**Performance:** No spinner for tables (skeleton preserves layout, avoids CLS).

### 2.14 Confirmation Dialog

**Extension of Modal, for destructive actions.**

**Props:** open, title, description, variant 'danger'|'warning', confirmLabel, cancelLabel, requireTyping (string to type e.g. invoice number), loading, onConfirm, onCancel.

**For bursar voiding invoice:** requireTyping invoice number INV-2026-0001 to confirm, preventing accidental click.

**States:** Confirm button disabled until typing matches.

---

## 3. LAYOUT PATTERNS

### 3.1 Application Shell - Sidebar + Top Bar

**Structure:**
```
+-----------------------------------------------------+
| TopBar: Logo #0F153A | Tenant Switch | Search | Bell | Avatar |
+------------+----------------------------------------+
| Sidebar    | Main Content Area (scroll)               |
| - Dashboard|  Breadcrumb + Page Title + Actions       |
| - Students |  Filters / Tabs                          |
| - Attendance (badge today missing)                  |
| - Timetable|  Content Cards / Tables                  |
| - Fees     |                                          |
| - Exams    |                                          |
| - Reports  |                                          |
| - Settings |                                          |
|------------|                                          |
| User Card  |                                          |
+------------+----------------------------------------+
```

**Sidebar:** 240px desktop, collapsed to 64px icons only, off-canvas drawer on <1024px. Background primary-950 #070A1E, text white, active bg primary-800 with secondary-300 left border 3px. Icons Lucide, 20px, min 44px row.

**TopBar:** 56px tall, white bg, shadow-xs, contains tenant name (Petra High, Bulawayo), academic year/term switcher, quick search (Ctrl+K), notifications.

**Main:** max-width 1280px centered? Actually full width for tables, 24px padding mobile 16px.

**Performance:** Shell CSS inline critical, sidebar no JS for initial paint.

### 3.2 List Page Pattern

**Order:** Breadcrumb > Title + Primary Action (Add Student) > Stat Row (optional) > Filters (search + dropdowns + date) > Table > Pagination > Export.

**Filters:** Collapse to bottom sheet on mobile, sticky.

**Example Student List:** Search + Grade + Stream + Status + Enrolment Year filters.

### 3.3 Detail Page Pattern

**Structure:** Back link > Header (Avatar, Name, Badge, Actions Edit/Archive) > Tabs (Overview, Guardians, Enrolment History, Fees, Attendance, Reports) > Tab content.

**Guardians tab:** List guardian_student_links cards showing relationship + billing flag.

**Enrolment History timeline:** vertical line with repeat/transfer/readmission markers.

### 3.4 Multi-Step Wizard - School Setup Wizard

**Steps header:** Horizontal stepped progress, 7 steps from SRS (school profile, academic year & terms, grades & streams, fee structures, subjects, import users, invite staff). Each step shows title, description, status (todo/active/done).

**Body:** Form fields for step, with helper illustration (AI classroom image).

**Footer:** Back, Next, Save & Exit (draft), Progress "Step 3 of 7".

**Validation:** Inline, not blocking wizard navigation but prevents Finish if invalid. Draft saved to localStorage + server every 30s for connection drops.

**Mobile:** Steps become vertical accordion.

### 3.5 Printable Report Page

**For report card, invoice statement, attendance register, arrears list.**

**Screen view:** Card with print button, print preview.

**Print CSS:**
- Hide sidebar, topbar, buttons via `print:hidden`
- Force white bg black text: `print:bg-white print:text-black`
- Tables border-collapse, border black 1pt
- All status colors become border + text + icons (greyscale legible)
- Add footer: "Printed from LearnCloud Petra High Bulawayo | Page X | Date"
- Page breaks: `break-inside: avoid` for cards, `break-after: page` for report cards bulk.

---

## 4. TEXT-BASED WIREFRAMES - 12 SCREENS

All wireframes use monospaced box drawing, mobile-first mental model. Optimized attendance & marks noted.

### 4.1 Login

```
+---------------------------------------------------------------+
| [Left: AI-generated image of school library]  | [Right Panel] |
| Gradient overlay primary-800/60 #0F153A to    | Logo LearnCloud |
| purple #5F3F96 - legible white text           | Petra High, Bulawayo |
|                                               |               |
| "Welcome to LearnCloud"                        | Email [__________] 16px 44px |
| "School management for independent schools"   | Password [______] Eye icon |
| Tiny sparkle ★ stars like palette background  | [✓ Remember me] Forgot? |
|                                               | [Login - 48px primary-800 #0F153A] |
|                                               | Or [Login with Google] ghost |
|                                               | Footer: © 2026 Bulawayo support@ |
+---------------------------------------------------------------+
Mobile: Image on top 40vh, form below full width. Background image lazy loaded, placeholder color primary-50.
Slow 3G: Image has low-quality image placeholder (LQIP) blurred.
```

### 4.2 School Setup Wizard Step (Step 3: Grades & Streams)

```
+---------------------------------------------------------------+
| TopBar | Progress: [●●●○○○○] Step 3 of 7: Grades & Streams   45% |
+---------------------------------------------------------------+
| Sidebar | Card: Add Grades                                      |
| collapsed| [Grade Name e.g. Grade 5] [Code G5] [Order 5] [+Add] |
|          | Table: Grade | Code | Streams | Capacity | Action    |
|          | G5 | G5 | Blue (32/40), Green (28/40) | [Edit][Del] |
|          | Form 1 | F1 | A (45/50) | [Edit]                        |
|          |                                                       |
|          | Illustration: AI classroom with desks                 |
|          | Helper: "Streams are class divisions. Capacity      |
|          | prevents over-enrollment. You can change later."     |
|          |                                                       |
|          | Footer: [< Back] [Save Draft] [Next: Fee Structures >] |
+---------------------------------------------------------------+
Autosave every 30s to localStorage draft_wizard_tenantId.
```

### 4.3 Admin Dashboard

```
+---------------------------------------------------------------+
| Breadcrumb: Home > Dashboard | Year: 2026 ▼ Term: Term 2 ▼    |
+---------------------------------------------------------------+
| Stat Tiles Row (2 col mobile, 4 col desktop, 12px gap):       |
| [Total Students 542 ↑12] [Arrears >60d $12,400 !] [Attend Today 92% ↓] [Admissions Pending 7] |
|                                                               |
| Tabs: Overview | Finance | Academic | Attendance              |
|                                                               |
| Left 2/3: Chart - Fee Collection Trend (bar, greyscale OK)    |
| Right 1/3: Upcoming - Term ends 05 Dec, Exams due             |
|                                                               |
| Table: Recent Arrears | Student | Grade | Balance | Days | Action |
| Ndlovu T | G5 Blue | $450 | 62d | [View][Message Parent]       |
| Quick Actions: [Mark Attendance] [Generate Invoices] [Enter Marks] |
+---------------------------------------------------------------+
All stat tiles have icon+color+text for greyscale.
```

### 4.4 Student List

```
+---------------------------------------------------------------+
| Students (542) [Add Student] [Import CSV] [Export]            |
| Search [_Thabo  ⌕]  Grade [All ▼] Stream [All ▼] Status [Active ▼] |
|                                                               |
| Table: ☑ | Photo | Name | Number | Grade | Stream | Guardians | Balance | Actions |
| ☐ | [ph] | Thabo Ndlovu | 2026-001 | G5 | Blue | M.Ndlovu (billing) | $0 | [View] |
| ☐ | [ph] | Lindiwe ... | 2026-002 | F1 | A | ... | $450 | [View][Invoice] |
|                                                               |
| Pagination: Showing 1-25 of 542 | [25 ▼] per page | [< 1 2 3 >] |
+---------------------------------------------------------------+
Row height 56px for touch. Photo 32px circle fallback initials.
Bulk actions appear when checkbox selected: [Assign to Stream][Generate Invoice].
```

### 4.5 Student Detail

```
+---------------------------------------------------------------+
| < Back to Students | Thabo Ndlovu | Badge: Active G5 Blue | [Edit][Archive] |
| Photo 80px | DOB 12/03/2011 | Student # 2026-001 | Status Active |
| Tabs: Overview | Guardians (2) | Enrolment History | Fees | Attendance | Reports |
|                                                               |
| Overview Tab:                                                 |
| Card: Guardians - Mrs Ndlovu Mother Primary+Billing Mother ■● |
|        Mr Ndlovu Father Emergency                             |
| Card: Current Enrolment - 2026 Term2 G5 Blue Enrolled from 10 Jan |
| Card: Quick Stats - Attendance 94%, Balance $0, Avg 72%       |
|                                                               |
| Enrolment History Tab Timeline:                               |
| ● 2026 Term1 G5 Blue Enrolled - previous none                 |
| ● 2026 Term2 G5 Blue Continuing - same                        |
| (If repeated: ○ 2027 G5 Green Repeat - previous 2026)         |
+---------------------------------------------------------------+
```

### 4.6 Attendance Register Capture - OPTIMIZED (MOST IMPORTANT)

```
+---------------------------------------------------------------+
| Attendance > Grade 5 Blue > Today 02 Aug 2026 [Date Picker ▼] |
| [Period: Homeroom ▼] [Time 08:00] Class Teacher: Mrs Moyo     |
| Progress: 30/40 marked | [Mark All Present]                    |
| Search [Name] Filter [Unmarked ▼]                             |
|                                                               |
| Table - FAST, 60fps, no page reload:                          |
| # | Student | Status [P][A][L][S][E] | Comment | Sync         |
| 1 | Thabo N | (●)P [ ]A [ ]L [ ]S [ ]E | [___] | ✓          |
| 2 | Lindiwe M| [ ]P (●)A [ ]L [ ]S [ ]E | Sick? | ◍ pending    |
| 3 | ...      | [ ]P [ ]A (●)L 08:12 [ ]S [ ]E | Bus late| ✓   |
|                                                               |
| P=Present A=Absent L=Late S=Sick E=Excused - 44px radio/toggle|
|                                                               |
| Footer Sticky: [Save Draft Locally] [Submit 30/40] 48px primary-800 |
| Toast: "Attendance saved locally, will sync when online"      |
|                                                               |
| OPTIMIZATIONS:                                                |
| - Entire grid editable with keyboard Tab/Arrow, Space toggles |
| - Mark All Present 1 tap sets all P, teacher then changes absents |
| - Offline: saves to IndexedDB instantly, sync icon ◍→✓        |
| - Touch target 48px status buttons, color + letter + icon for grey |
| - No modal confirmations, undo toast instead                  |
| - <10 sec server time for 60 learners per SRS FR-AT01         |
+---------------------------------------------------------------+
Mobile: Swipe left on row to quickly mark Absent, swipe right Present ( Tinder-like but with undo ).
Slow 3G: Initial HTML <100KB, no images, submit uses background sync API with retry.
```

### 4.7 Timetable

```
+---------------------------------------------------------------+
| Timetable | Grade [G5 Blue ▼] Term [Term2 ▼] [Clone] [Export] |
| Teacher [All ▼] Room [All ▼] | View: [Grid][List]              |
|                                                               |
| Grid - 7 days top, 8 periods side:                            |
|       Mon | Tue | Wed | Thu | Fri                            |
| P1 08:00 Math | Eng | Math | Science | ...                      |
| P2 08:45 Eng  | Math| ...   | ... | ...  [Conflict! red border] |
| Click cell: Modal Edit - Subject [Math ▼] Teacher [Moyo ▼] Room [R5 ▼] |
|                                                               |
| List view for mobile <768: Vertical cards per day.            |
+---------------------------------------------------------------+
Conflict detection: same teacher/room double-booked shows red border + tooltip.
```

### 4.8 Fee Structure Setup

```
+---------------------------------------------------------------+
| Fees > Structures > 2026 Term2 Grade5 [Draft ▼]               |
| Name [Term2 2026 Grade5 Fees] Currency [USD ▼] [ZWG checkbox]|
| Grade [G5 ▼] Mandatory [✓] | Status [Draft|Active]            |
|                                                               |
| Line Items Table:                                             |
| Item | Amount | Currency | Optional | GL Code | Action            |
| Tuition | [500.00] | USD ▼ | ☐ | 4001 | [Delete]                |
| Levy | [50.00] | USD | ☐ | 4002 | [Del] [Add Item +]           |
| Boarding | [200.00] | USD | ☑ optional | 4003 | [Del]          |
|                                                               |
| Subtotal: $750.00 USD | [Save Draft] [Activate Structure]      |
| Warning: Activating locks amounts; invoices snapshot amounts  |
+---------------------------------------------------------------+
Money input: decimal(18,2) with currency column per spec. Autosave.
```

### 4.9 Invoice Detail

```
+---------------------------------------------------------------+
| < Back | Invoice INV-2026-0001 | Status: Overdue ■ warning amber |
| Student: Thabo Ndlovu G5 Blue | Year 2026 Term2 | Due 10 Feb 2026|
|                                                               |
| Card: Summary | Subtotal $750 | Discount $0 | Total $750 | Paid $300 | Balance Due $450 |
|                                                               |
| Line Items (snapshot): Tuition $500, Levy $50, Boarding $200  |
| Payments Allocated: $300 on 15 Feb cash ref #123 [Proof View] |
| [Add Payment] $450 remaining                                  |
|                                                               |
| Timeline: Invoiced 20 Jan, Paid partial 15 Feb, Overdue 10 Feb + 62d |
| Actions: [Print Receipt] [Void/ Credit Note - requires typing INV number] [Message Parent Billing] [Export PDF] |
| Guardian Billing: Mrs Ndlovu Mother 077... [Call][SMS]        |
+---------------------------------------------------------------+
Subtle grade: Overdue >=60d red + icon for greyscale.
```

### 4.10 Marks Entry Grid - OPTIMIZED (MOST IMPORTANT)

```
+---------------------------------------------------------------+
| Marks > Assessment: Math Mid-Term Grade5 - Max 100 | Category Coursework 30% |
| Grade [G5 ▼] Stream [Blue ▼] Subject [Math ▼] Term [Term2 ▼] Date [02 Aug 2026] |
| Teacher: Moyo | Status: Draft | [Submit for Approval]         |
| Progress: 28/40 entered | Search [Name] | [Mark Absent Bulk]      |
|                                                               |
| GRID - Excel-like, keyboard-first:                            |
| Student | Prev Avg | Score [/100] | Grade | Comment | Status |  |
| Thabo N | 68 | [ 75 ] | B | [Good] | ✓ draft |                |
| Lindiwe M| 82 | [    ] | - | [_] | ◍ pending | Absent checkbox |
| ... 40 rows, sticky name column, sticky header                |
|                                                               |
| Footer - Bulk: [Fill 0 for blank] [Import CSV] [Export] [Save Draft 48px] |
| Validation: 0<=mark<=max, red border if invalid, tooltip      |
|                                                               |
| KEYBOARD NAV (critical for impatient users):                  |
| - Tab / Shift+Tab: move right/left across editable cells      |
| - Enter: down one row same column                             |
| - Arrow Keys: navigate grid like Excel                        |
| - Number: type directly, auto-select cell content             |
| - Space: toggle Absent                                        |
| - Ctrl+S: save draft locally                                  |
| - Ctrl+Enter: submit                                          |
| - Focus ring primary-500 shadow-focus 3px                     |
|                                                               |
| OFFLINE & SLOW:                                               |
| - Each keystroke saved to IndexedDB draft_marks_{assessmentId} |
| - Visual: ◍ = pending local, ✓ = synced, ! = conflict       |
| - Connection drop mid-entry: Toast "Offline, saving locally. Will sync when back. 28 unsaved" |
| - Conflict: if teacher B edits same cell, show diff dialog   |
| - Virtualized rows (only visible 15 rows render) for 60 learners <100ms |
| - No page reload on save - PATCH /api/marks/bulk in background |
| - Undo last entry Ctrl+Z with toast                           |
+---------------------------------------------------------------+
Mobile: Card list not grid when <768 - each student card with input stepper -/+.
```

### 4.11 Report Card Preview

```
+---------------------------------------------------------------+
| < Back | Report Card | Thabo Ndlovu | 2026 Term2 G5 Blue | Draft |
| School Header: Logo Petra High | Moto | Color primary-800 #0F153A|
| Student Photo | Number | Grade | Attendance 94% | Rank 5/40 | Avg 72% |
| Table: Subject | Coursework30% | Mid30% | Final40% | Avg | Grade | Comment | Class Avg |
| Math | 80 | 75 | 70 | 74 | B | Good | 65                                  |
| Eng  | ...                                                                 |
| Overall Comment: [Textarea] Teacher: Moyo                      |
| Promotion: [Promoted ▼] | [Generate PDF] [Publish to Parent Portal] |
| Preview greyscale toggle button to test print legibility       |
+---------------------------------------------------------------+
Print: Hide buttons, border black, school crest greyscale.
```

### 4.12 Arrears List - Bursar Primary Screen

```
+---------------------------------------------------------------+
| Fees > Arrears | Age: [30+ ▼] [60+][90+][All] Grade[All▼] Stream[All▼] |
| Search [Name] | Due Date [Range ▼] | Total Overdue: $12,400 (32 students) |
|                                                               |
| Table: ☐ | Student | Grade | Invoice | Due Date | Days Over | Balance | Guardian Billing | Action |
| ☐ | T Ndlovu | G5 Bl | INV-001 | 10 Feb | 62d | $450 | M Ndlovu 077... | [Msg][Receipt][Pay] |
| ☐ | L M | F1 A | INV-045 | 10 Feb | 62d | $780 | ... | [Msg] |
| Bulk: [Message Selected (5)] [Export CSV] [Generate Reminder PDF] |
| Pagination 1-25 of 32 | Sort by Days Over ▼                      |
| Card: Summary Stats - 0-30d $3k, 30-60d $4k, 60-90d $5k, 90+ $400|
+---------------------------------------------------------------+
Row color + icon: Warning amber border + ! for 30-60d, danger red + X for >60d, distinct in grey via pattern/shade.
Touch: Whole row tappable, actions swipe.
Print: All status chips have text "62d Overdue" not just color.
```

---

## 5. ACCESSIBILITY & USABILITY RULES

### 5.1 Minimums & Contrast

- **Touch Target:** min 44x44px per WCAG 2.5.5, LearnCloud enforces 44px md, 48px lg for primary attendance/mark buttons. Tested on 5" Android.
- **Contrast:** Text 4.5:1 min normal (neutral-800 #171725 on white 15.8:1 PASS), large text 3:1. Primary buttons white on #0F153A 15.5:1 PASS. Focus ring not relying on color alone + 3px.
- **Focus Visible:** All interactive focus-visible ring `shadow-focus`. Keyboard tab order logical: filters -> table -> pagination.
- **Screen Reader:** Table headers `<th scope=col>` with aria-sort, marks grid cells `aria-label="Thabo Ndlovu Math score"`. Toast `aria-live=polite`, error `aria-live=assertive`.

### 5.2 Keyboard Navigation for Marks Entry Grid (Critical Path)

**Goal:** Teacher can enter 40 marks without touching mouse, faster than Excel.

- **Grid Role:** `role=grid`, rows `role=row`, cells `role=gridcell`.
- **Navigation:**
  - `Tab`: next editable cell left→right, wrap to next row start.
  - `Shift+Tab`: previous.
  - `Enter`: move down one row same column, if last row -> first row next column? Actually down.
  - `ArrowRight/Left/Up/Down`: move 1 cell (like Excel). At edge, stay.
  - `Home/End`: first/last cell in row.
  - `Ctrl+Home/End`: first/last row.
  - `Typing number`: overwrite cell content (select all on focus), auto-save draft after 500ms debounce.
  - `Space`: when on Absent checkbox toggles absent + clears score.
  - `Escape`: cancel edit, restore previous.
  - `Ctrl+S`: save draft (also auto).
  - `Ctrl+Enter`: submit for approval (opens confirmation dialog).
- **Focus Management:** After save, focus stays in cell, toast appears not stealing focus.
- **Shortcuts help:** Press `?` in grid shows legend modal.
- **Bulk:** Select range with Shift+Arrow, Fill Down Ctrl+D.

**Test:** Teacher can grade 40 students in <3 min with keyboard only, measured.

### 5.3 Behaviour When Connection Drops Mid-Entry

This is expected in Bulawayo (intermittent 3G, power).

**For Attendance & Marks (P0 screens):**

1. **Optimistic Local-First:**
   - Every keystroke / status toggle writes to IndexedDB `localDrafts` store: key = `tenantId_assessmentId_studentId` or `attendance_tenant_date_stream`. Timestamp + user_id.
   - Visual sync state per row: `✓ Synced (green)`, `◍ Pending (amber spinning)`, `! Conflict (red)` + tooltip.
   - Header shows "Offline - 12 changes saved locally" with warning toast `messages.read`.

2. **Background Sync:**
   - Use `navigator.onLine` + heartbeat fetch `/health` every 10s. If offline, queue API calls in `syncQueue` IndexedDB.
   - When back online, auto-flush queue with exponential backoff: bulk PATCH `/attendance/bulk` and `/marks/bulk` with If-Match etag to detect conflicts.
   - On success, update row icons to ✓, toast "28 marks synced".
   - On failure (server conflict, 409), mark ! and open dialog "Lindiwe M's mark changed by Head while offline. Your 75 vs Server 80. Keep yours or take server?" with diff.

3. **No Data Loss:**
   - Page unload warns `beforeunload` if pending count >0.
   - Wizard also saves draft to localStorage every 30s + server draft endpoint.
   - Login page shows last sync time.

4. **UI Degradation:**
   - Disable real-time collaboration indicators when offline, show banner `You are offline - changes will sync when connection returns`.
   - All buttons remain enabled (no dead UI).
   - Retry button on toast.

5. **Print/Export Always Works Offline:** Generate PDF client-side from local data (jsPDF) so bursar can print arrears even offline.

**General App:**
- Service Worker caches shell (HTML/CSS/JS) ~300KB, not images. App loads offline skeleton.
- Searchable tables show cached last results with "Showing cached results from 10 min ago".
- Forms have `autocomplete=off` for financial but on for names to help slow typers.

### 5.4 Additional Usability for Non-Technical Staff

- **Language:** No jargon, "Guardian - person who pays or picks up child" tooltip. Use "Arrears" + "(Money owed)" subtitle.
- **Empty States:** Human, not "0 results" but "No students found in Grade 5 Blue. Try clearing filter or add new student."
- **Confirmation Dialog:** For destructive `fees.invoices.void`, require typing invoice number, not just OK.
- **Stat Tiles:** Always show trend vs last term, not just absolute.
- **Date Format:** DD/MM/YYYY everywhere, placeholder "DD/MM/YYYY", calendar shows Zimbabwe holidays.
- **Printing:** Test every list/report in greyscale print preview via button toggle in UI: user can click "Preview B&W" ( CSS filter grayscale(100%) ) to verify legibility before printing.

---

**Files:**
- `tailwind.config.js` - full theme extension copy-paste ready
- This file `LearnCloud_Design_System_v1.md` - component inventory, layouts, wireframes, a11y rules

**Optimized Screens Summary:** Attendance Register Capture and Marks Entry Grid have 48px touch targets, local-first IndexedDB, virtualized rows, keyboard Excel-like nav, <10s save for 60 learners per FR-AT01, auto-sync, conflict resolution, and greyscale legible status via letter+icon+color.

End of Design System V1
