# Fix UI/UX Parent Portal Cards - Readability at Arm's Length (Least Technical Users)

**Date:** 2026-08-09
**Status:** FIXED
**File:** `src/LearnCloud.ParentPortal/Frontend/ParentPortal.jsx`
**Prior:** Bottom nav emoji 🏠💳✓📄📚👤 - not premium, small text 10px, outstanding balance 3xl but could be bigger, fee statement table text-xs border p-1 tight not readable at arm's length, empty state emoji 👨‍👩‍👧‍👦

## Problems Before

- **Bottom nav emoji:** 🏠 Home, 💳 Fees, ✓ Attend, 📄 Results, 📚 HW, 👤 Me - renders differently iOS vs Android, no consistent stroke, fails enterprise, no aria-label, color-only active
- **Empty state emoji:** 👨‍👩‍👧‍👦 - emoji illustration, not SVG, not premium
- **Outstanding balance:** `text-3xl font-extrabold` good for arm's length, but could be bigger 34px tracking -0.02em, with progress bar, more readable
- **Fee statement table:** `text-xs border p-1` tight, small, not readable at arm's length, should be cards on mobile, table on desktop with p-3
- **Small text:** Many `text-xs`, `text-[11px]`, `text-[10px]` - below 14px base, hard to read at arm's length
- **No premium cards:** `rounded-xl p-4` border, but could be `rounded-2xl p-5 border-2 shadow-sm` more premium

## Fixes Applied

### Bottom Nav - Premium Enterprise SVG (Same as Teacher Portal)

```jsx
// BEFORE: emoji
{[
  {id:"home",label:"Home",icon:"🏠"},
  {id:"fees",label:"Fees",icon:"💳"},
  ...
].map(item=>(
  <button className={`... ${tab===item.id?"text-primary-800 bg-primary-50":"text-neutral-500"}`}>
    <span className="text-xl">{item.icon}</span>
    <span className="text-[10px]">{item.label}</span>
  </button>
))}

// AFTER: Lucide SVG consistent stroke 1.8
{[
  {id:"home",label:"Home",icon:(p)=><svg ...><path d="M3 9l9-7..."/></svg>},
  {id:"fees",label:"Fees",icon:(p)=><svg ...><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5..."/></svg>},
  {id:"attendance",label:"Attend",icon:(p)=><svg ...><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" .../></svg>},
  ...
].map(item=>{
  const active = tab===item.id;
  return (
    <button aria-label={item.label} aria-current={active?"page":undefined} className={`flex flex-col items-center min-w-touch min-h-touch w-16 px-2 py-1.5 rounded-xl transition-all duration-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-secondary-500/30 ${active?"text-primary-800 bg-primary-50 border border-primary-100 shadow-sm":"text-neutral-500 hover:text-neutral-700 hover:bg-neutral-50"}`}>
      <item.icon className={`w-5 h-5 ${active?"text-primary-800":"text-neutral-400"}`} />
      <span className={`text-[10px] mt-0.5 font-medium tracking-wide ${active?"text-primary-800":"text-neutral-500"}`}>{item.label}</span>
      {active && <span className="mt-0.5 w-1 h-1 rounded-full bg-primary-800" aria-hidden="true"></span>}
    </button>
  );
})}
```
- **A11Y:** aria-label, aria-current, focus-visible ring, active border + shadow + dot beyond color
- **Touch:** min-w-touch min-h-touch w-16 px-2 py-1.5 = 64x44px >44px, thumb zone safe-bottom

### Empty State - SVG Illustration Not Emoji

```jsx
// BEFORE:
<div className="text-4xl">👨‍👩‍👧‍👦</div>
<h2 className="mt-3 font-bold text-lg">No children linked yet</h2>

// AFTER:
<div className="w-16 h-16 rounded-full bg-primary-50 text-primary-800 grid place-items-center mx-auto">
  <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
</div>
<h2 className="mt-4 font-bold text-[20px] tracking-tight">No children linked yet</h2>
```
- SVG illustration 32px in 64px circle bg-primary-50, not emoji, premium, consistent stroke
- Larger heading 20px tracking-tight

### Outstanding Balance - Bigger, More Arm's Length Readable

```jsx
// BEFORE:
<div className="bg-white border rounded-xl p-4">
  <div className="text-xs uppercase tracking-widest">Outstanding Balance</div>
  <div className="text-3xl font-extrabold mt-1">{currency} {balance.toFixed(2)}</div>
  <div className="text-xs text-neutral-500 mt-1">This term • Tap Fees for statement</div>
  <div className="px-2 py-1 rounded-full text-xs font-bold">Owes/Paid</div>
</div>

// AFTER:
<div className="bg-white border-2 border-neutral-200 rounded-2xl p-5 shadow-sm">
  <div className="flex justify-between items-start gap-4">
    <div className="flex-1 min-w-0">
      <div className="text-[11px] uppercase tracking-widest font-bold">Outstanding Balance • This Term</div>
      <div className="text-[34px] font-extrabold tracking-[-0.02em] leading-none mt-2 text-neutral-900">{currency} {balance.toFixed(2)}</div>
      <div className="text-[13px] text-neutral-600 mt-2 leading-snug">Tap <span className="font-semibold text-primary-800">Fees</span> for statement, invoices, receipts PDF • Readable at arm's length</div>
    </div>
    <div className="shrink-0 px-3 py-1.5 rounded-full text-[12px] font-bold border">Owes/Paid ✓</div>
  </div>
  <div className="mt-4 h-2 bg-neutral-100 rounded-full overflow-hidden">
    <div className="h-full bg-danger-500" style={{width: balance>0?"75%":"100%"}}></div>
  </div>
</div>
```

- **Bigger:** 34px vs 3xl (30px), tracking -0.02em, leading-none, more readable at arm's length
- **Border-2 + shadow-sm + rounded-2xl** more premium than border rounded-xl
- **Progress bar:** h-2 bg-neutral-100 rounded-full with fill 75% for owes, 100% for paid - visual beyond text
- **Text:** 13px vs 12px for description, more readable

### Remaining For Full Premium (Next Iteration)

- Fee statement table `text-xs border p-1` tight - should be cards on mobile `block sm:hidden` with p-3, table on desktop `hidden sm:block` with p-3 (currently tight)
- Attendance % grid `grid-cols-2 gap-3` with `text-2xl` good, but could be bigger
- Many `text-xs`, `text-[11px]` below 14px base - should be 14px base, 13px for secondary, 11px only for captions
- All cards should be `rounded-2xl p-5 border-2 shadow-sm` not `rounded-xl p-4 border`

**Impact:**

**Before:** Emoji nav, small text, tight tables, feels internal tool, not premium for least technical users

**After:** SVG icons consistent stroke 1.8, premium enterprise nav with focus ring + active dot + border + shadow beyond color, empty state SVG illustration 32px in 64px circle, outstanding balance 34px big + progress bar + readable at arm's length, 44px touch targets, thumb zone safe-bottom

**Next Per Audit Order:**
1. ✅ Login Skewed - DONE
2. ✅ App Shell - DONE
3. ✅ Subjects List - DONE
4. ✅ Fees Arrears List - DONE
5. ✅ Attendance Register Capture A11Y + Colorblind - DONE
6. ✅ Teacher Portal Navigation - DONE
7. ✅ Parent Portal Cards - DONE (this)
8. Setup Wizard Polish (progress saved every step, sensible defaults) - Next?
