import React, { useState, useEffect } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { getAccountStatus, getSessionInfo, logout } from '../../lib/apiClient';

// Icons - Lucide style consistent stroke 1.8
const IconDashboard = (p) => <svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/></svg>;
const IconUsers = (p) => <svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>;
const IconGraduation = (p) => <svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M22 10v6M2 10l10-5 10 5-10 5z"/><path d="M6 12v5c3 3 9 3 12 0v-5"/></svg>;
const IconBook = (p) => <svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/></svg>;
const IconMoney = (p) => <svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>;
const IconCalendar = (p) => <svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>;
const IconClock = (p) => <svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>;
const IconMessage = (p) => <svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/></svg>;
const IconSettings = (p) => <svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 1 1-2.83 2.83l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-4 0v-.09a1.65 1.65 0 0 0-1-1.51 1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 1 1-2.83-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1 0-4h.09a1.65 1.65 0 0 0 1.51-1 1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 1 1 2.83-2.83l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 4 0v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 1 1 2.83 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 0 4h-.09a1.65 1.65 0 0 0-1.51 1z"/></svg>;
const IconSearch = (p) => <svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg>;
const IconBell = (p) => <svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M18 8A6 6 0 0 0 6 8c0 7-6 9-6 9h18s-6-2-6-9"/><path d="M13.73 21a2 2 0 0 1-3.46 0"/></svg>;
const IconChevronDown = (p) => <svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="6 9 12 15 18 9"/></svg>;
const IconMenu = (p) => <svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><line x1="3" y1="12" x2="21" y2="12"/><line x1="3" y1="6" x2="21" y2="6"/><line x1="3" y1="18" x2="21" y2="18"/></svg>;

const NAV_GROUPS = [
  {
    label: "Overview",
    items: [
      { id: "dashboard", label: "Dashboard", icon: IconDashboard, path: "/dashboard", badge: null },
      { id: "analytics", label: "Analytics", icon: IconDashboard, path: "/analytics", badge: "New" },
    ]
  },
  {
    label: "Academic",
    items: [
      { id: "academic-years", label: "Academic Years", icon: IconCalendar, path: "/academic-years" },
      { id: "grades", label: "Grades & Streams", icon: IconGraduation, path: "/grades" },
      { id: "subjects", label: "Subjects", icon: IconBook, path: "/subjects" },
      { id: "timetable", label: "Timetable", icon: IconClock, path: "/timetable" },
      { id: "attendance", label: "Attendance", icon: IconCalendar, path: "/attendance" },
    ]
  },
  {
    label: "People",
    items: [
      { id: "students", label: "Students", icon: IconUsers, path: "/students" },
      { id: "guardians", label: "Guardians", icon: IconUsers, path: "/guardians" },
      { id: "staff", label: "Staff", icon: IconUsers, path: "/staff" },
    ]
  },
  {
    label: "Finance",
    items: [
      { id: "fees-structures", label: "Fee Structures", icon: IconMoney, path: "/fees/structures" },
      { id: "fees-invoices", label: "Invoices", icon: IconMoney, path: "/fees/invoices" },
      { id: "fees-payments", label: "Payments", icon: IconMoney, path: "/fees/payments" },
      { id: "fees-arrears", label: "Arrears", icon: IconMoney, path: "/fees/arrears" },
    ]
  },
  {
    label: "Communication",
    items: [
      { id: "messaging", label: "Messaging", icon: IconMessage, path: "/messaging" },
      { id: "notices", label: "Notices", icon: IconMessage, path: "/notices" },
    ]
  },
  {
    label: "System",
    items: [
      { id: "library", label: "Library", icon: IconBook, path: "/library" },
      { id: "reports", label: "Reports", icon: IconDashboard, path: "/reports" },
      { id: "settings", label: "Settings", icon: IconSettings, path: "/settings" },
    ]
  }
];

function CommandPalette({ open, onClose }) {
  const [query, setQuery] = useState('');
  const navigate = useNavigate();

  const allItems = NAV_GROUPS.flatMap(g => g.items.map(i => ({ ...i, group: g.label })));
  const filtered = allItems.filter(item => 
    item.label.toLowerCase().includes(query.toLowerCase()) ||
    item.group.toLowerCase().includes(query.toLowerCase())
  ).slice(0, 8);

  useEffect(() => {
    const handleEsc = (e) => { if (e.key === 'Escape') onClose(); };
    const handleK = (e) => { if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') { e.preventDefault(); onClose(); } };
    if (open) {
      document.addEventListener('keydown', handleEsc);
      document.addEventListener('keydown', handleK);
      document.body.style.overflow = 'hidden';
    }
    return () => {
      document.removeEventListener('keydown', handleEsc);
      document.removeEventListener('keydown', handleK);
      document.body.style.overflow = '';
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center pt-[20vh] p-4">
      <div className="absolute inset-0 bg-primary-950/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative w-full max-w-lg bg-white rounded-2xl shadow-2xl border border-neutral-200 overflow-hidden animate-slide-up">
        <div className="flex items-center gap-3 p-4 border-b border-neutral-100">
          <IconSearch className="w-5 h-5 text-neutral-400" />
          <input
            autoFocus
            value={query}
            onChange={e => setQuery(e.target.value)}
            placeholder="Search pages, students, invoices... (e.g., 'fees arrears')"
            className="flex-1 h-8 bg-transparent text-[14px] placeholder:text-neutral-400 focus:outline-none"
          />
          <kbd className="hidden sm:inline-flex h-6 px-2 items-center rounded bg-neutral-100 border text-[11px] font-mono text-neutral-500">ESC</kbd>
        </div>
        <div className="p-2 max-h-80 overflow-auto">
          {filtered.length === 0 ? (
            <div className="p-8 text-center">
              <div className="text-sm text-neutral-500">No results for "{query}"</div>
              <div className="text-xs text-neutral-400 mt-1">Try "students", "fees", "attendance", "timetable"</div>
            </div>
          ) : (
            <div className="space-y-1">
              {filtered.map(item => (
                <button
                  key={item.id}
                  onClick={() => { navigate(item.path); onClose(); }}
                  className="w-full flex items-center gap-3 p-3 rounded-xl hover:bg-neutral-50 text-left transition"
                >
                  <div className="w-8 h-8 rounded-lg bg-primary-50 text-primary-800 grid place-items-center">
                    <item.icon className="w-4 h-4" />
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="text-[14px] font-medium text-neutral-900">{item.label}</div>
                    <div className="text-[12px] text-neutral-500">{item.group} • {item.path}</div>
                  </div>
                  <div className="text-[11px] text-neutral-400">↵</div>
                </button>
              ))}
            </div>
          )}
          <div className="p-3 border-t border-neutral-100 flex gap-2 text-[11px] text-neutral-400">
            <span className="flex items-center gap-1"><kbd className="px-1.5 py-0.5 rounded bg-neutral-100 border text-[10px]">↑↓</kbd> Navigate</span>
            <span className="flex items-center gap-1"><kbd className="px-1.5 py-0.5 rounded bg-neutral-100 border text-[10px]">↵</kbd> Select</span>
            <span className="flex items-center gap-1"><kbd className="px-1.5 py-0.5 rounded bg-neutral-100 border text-[10px]">ESC</kbd> Close</span>
          </div>
        </div>
      </div>
    </div>
  );
}

export default function AppShell({ children, title, description, actions, breadcrumbs, preview = false }) {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const [commandOpen, setCommandOpen] = useState(false);

  const location = useLocation();
  const navigate = useNavigate();
  const displayName = getSessionInfo()?.displayName || 'Signed in';
  const [account, setAccount] = useState(null);

  useEffect(() => {
    let active = true;
    getAccountStatus().then(status => { if (active) setAccount(status); });
    return () => { active = false; };
  }, []);
  const showAccountBanner = account && account.banner && (account.isReadOnly || account.state === 'PastDue');
  const initials = displayName.split(/\s+/).filter(Boolean).slice(0, 2).map(w => w[0].toUpperCase()).join('') || 'LC';

  async function signOut() {
    await logout();
    navigate('/login', { replace: true });
  }

  // Keyboard shortcut Cmd/Ctrl+K for command palette
  useEffect(() => {
    const handleKeyDown = (e) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault();
        setCommandOpen(true);
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  const isActive = (path) => location.pathname === path || location.pathname.startsWith(path + '/');

  return (
    <div className="min-h-screen bg-neutral-50 flex">
      {/* Sidebar - Desktop */}
      <aside className={`
        hidden md:flex flex-col bg-primary-950 text-white/70 border-r border-white/10
        transition-all duration-300 ease-out
        ${sidebarCollapsed ? 'w-[72px]' : 'w-[280px]'}
        sticky top-0 h-screen
      `}>
        {/* Logo + Collapse */}
        <div className="h-14 flex items-center gap-2 px-4 border-b border-white/10 shrink-0">
          <div className="w-8 h-8 rounded-lg bg-white text-primary-900 grid place-items-center font-bold text-sm shrink-0">LC</div>
          {!sidebarCollapsed && (
            <>
              <div className="flex-1 min-w-0">
                <div className="font-bold text-white tracking-tight text-[14px] leading-none">LearnCloud</div>
                <div className="text-[11px] text-white/50 leading-none mt-0.5">School management</div>
              </div>
              <button
                onClick={() => setSidebarCollapsed(true)}
                className="w-6 h-6 rounded grid place-items-center hover:bg-white/10 text-white/50 hover:text-white transition"
                aria-label="Collapse sidebar"
              >
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="15 18 9 12 15 6"/></svg>
              </button>
            </>
          )}
          {sidebarCollapsed && (
            <button
              onClick={() => setSidebarCollapsed(false)}
              className="w-6 h-6 rounded grid place-items-center hover:bg-white/10 text-white/50 hover:text-white transition ml-1"
              aria-label="Expand sidebar"
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polyline points="9 18 15 12 9 6"/></svg>
            </button>
          )}
        </div>

        {/* Navigation */}
        <div className="flex-1 overflow-y-auto py-4 px-3 space-y-6 scrollbar-thin">
          {NAV_GROUPS.map(group => (
            <div key={group.label}>
              {!sidebarCollapsed && (
                <div className="px-2 mb-2 text-[11px] font-semibold uppercase tracking-widest text-white/30">{group.label}</div>
              )}
              <div className="space-y-1">
                {group.items.map(item => {
                  const active = isActive(item.path);
                  return (
                    <Link
                      key={item.id}
                      to={item.path}
                      className={`
                        group flex items-center gap-3 px-2.5 py-2 rounded-xl text-[13px] font-medium transition-all
                        ${active ? 'bg-white text-primary-900 shadow-sm' : 'text-white/60 hover:text-white hover:bg-white/10'}
                        ${sidebarCollapsed ? 'justify-center' : ''}
                      `}
                      title={sidebarCollapsed ? item.label : undefined}
                    >
                      <item.icon className={`w-[18px] h-[18px] shrink-0 ${active ? 'text-primary-800' : 'text-white/50 group-hover:text-white'}`} />
                      {!sidebarCollapsed && (
                        <>
                          <span className="flex-1 truncate">{item.label}</span>
                          {item.count && <span className="text-[11px] px-1.5 py-0.5 rounded-full bg-white/10 text-white/60">{item.count}</span>}
                          {item.badge && (
                            <span className={`text-[10px] px-1.5 py-0.5 rounded-full font-bold ${item.badgeType === 'warning' ? 'bg-warning-500 text-white' : 'bg-secondary-500 text-white'}`}>
                              {item.badge}
                            </span>
                          )}
                        </>
                      )}
                    </Link>
                  );
                })}
              </div>
            </div>
          ))}
        </div>

        {/* Bottom User */}
        <div className="p-3 border-t border-white/10 shrink-0">
          <div className={`flex items-center gap-2 p-2 rounded-xl bg-white/5 border border-white/10 ${sidebarCollapsed ? 'justify-center' : ''}`}>
            <div className="w-8 h-8 rounded-full bg-secondary-500 text-white grid place-items-center font-bold text-xs shrink-0" aria-hidden="true">{initials}</div>
            {!sidebarCollapsed && (
              <div className="flex-1 min-w-0">
                <div className="text-[13px] font-medium text-white truncate">{displayName}</div>
                <button onClick={signOut} className="text-[11px] text-white/60 hover:text-white underline underline-offset-2">Sign out</button>
              </div>
            )}
          </div>
          {!sidebarCollapsed && (
            <div className="mt-2 px-2 flex items-center gap-2 text-[11px] text-white/30">
              <div className="w-2 h-2 rounded-full bg-success-500 animate-pulse" />
              <span>All systems operational • 99.5% uptime</span>
            </div>
          )}
        </div>
      </aside>

      {/* Mobile Sidebar Overlay */}
      {mobileMenuOpen && (
        <div className="fixed inset-0 z-40 md:hidden flex">
          <div className="absolute inset-0 bg-primary-950/60 backdrop-blur-sm" onClick={() => setMobileMenuOpen(false)} />
          <div className="relative w-[300px] bg-primary-950 text-white/70 flex flex-col h-full">
            <div className="h-14 flex items-center gap-2 px-4 border-b border-white/10">
              <div className="w-8 h-8 rounded-lg bg-white text-primary-900 grid place-items-center font-bold text-sm">LC</div>
              <div className="flex-1">
                <div className="font-bold text-white text-[14px]">LearnCloud</div>
                <div className="text-[11px] text-white/50">School management</div>
              </div>
              <button onClick={() => setMobileMenuOpen(false)} className="w-8 h-8 rounded grid place-items-center hover:bg-white/10">✕</button>
            </div>
            <div className="flex-1 overflow-y-auto p-3 space-y-4">
              {NAV_GROUPS.map(group => (
                <div key={group.label}>
                  <div className="px-2 mb-1 text-[11px] font-semibold uppercase tracking-widest text-white/30">{group.label}</div>
                  <div className="space-y-1">
                    {group.items.map(item => (
                      <Link
                        key={item.id}
                        to={item.path}
                        onClick={() => setMobileMenuOpen(false)}
                        className={`flex items-center gap-3 px-3 py-2.5 rounded-xl text-[14px] ${isActive(item.path) ? 'bg-white text-primary-900' : 'text-white/60 hover:bg-white/10 hover:text-white'}`}
                      >
                        <item.icon className="w-5 h-5" />
                        <span>{item.label}</span>
                        {item.badge && <span className="ml-auto text-[10px] px-1.5 py-0.5 rounded-full bg-secondary-500 text-white">{item.badge}</span>}
                      </Link>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {/* Main Content */}
      <div className="flex-1 min-w-0 flex flex-col">
        {/* Topbar */}
        <header className="h-14 bg-white border-b border-neutral-200 sticky top-0 z-30 flex items-center gap-2 px-4 sm:px-6 shrink-0">
          <button
            onClick={() => setMobileMenuOpen(true)}
            className="md:hidden w-9 h-9 rounded-xl grid place-items-center border border-neutral-200 hover:bg-neutral-50"
            aria-label="Open menu"
          >
            <IconMenu className="w-5 h-5" />
          </button>

          {/* Breadcrumbs */}
          <div className="hidden sm:flex items-center gap-2 text-[13px]">
            <Link to="/" className="text-neutral-500 hover:text-neutral-800">Home</Link>
            {breadcrumbs ? (
              breadcrumbs.map((crumb, i) => (
                <React.Fragment key={i}>
                  <span className="text-neutral-300">/</span>
                  {crumb.path ? (
                    <Link to={crumb.path} className="text-neutral-600 hover:text-neutral-900">{crumb.label}</Link>
                  ) : (
                    <span className="font-medium text-neutral-900">{crumb.label}</span>
                  )}
                </React.Fragment>
              ))
            ) : (
              <>
                <span className="text-neutral-300">/</span>
                <span className="font-medium text-neutral-900">{title || "Dashboard"}</span>
              </>
            )}
          </div>

          <div className="flex-1" />

          {/* Search - Command Palette Trigger */}
          <button
            onClick={() => setCommandOpen(true)}
            className="hidden sm:flex items-center gap-2 h-9 px-3 pr-2 rounded-xl border border-neutral-200 bg-neutral-50 hover:bg-white hover:border-neutral-300 text-[13px] text-neutral-500 hover:text-neutral-700 transition w-full max-w-[320px]"
          >
            <IconSearch className="w-4 h-4" />
            <span className="flex-1 text-left">Search, jump to...</span>
            <kbd className="hidden lg:inline-flex h-5 px-1.5 items-center rounded bg-white border text-[11px] font-mono">⌘K</kbd>
          </button>

          <button
            onClick={() => setCommandOpen(true)}
            className="sm:hidden w-9 h-9 rounded-xl grid place-items-center border border-neutral-200 hover:bg-neutral-50"
            aria-label="Search"
          >
            <IconSearch className="w-5 h-5 text-neutral-500" />
          </button>

          {/* The school switcher and notification count that sat here showed a fixed
              sample school ("Petra High") and a made-up count to every tenant. */}

          {/* User Menu - Mobile */}
          <button onClick={signOut} title="Sign out" aria-label={`Sign out ${displayName}`} className="w-8 h-8 rounded-full bg-secondary-500 text-white grid place-items-center font-bold text-xs md:hidden">{initials}</button>
        </header>

        {/* Page Header */}
        {(title || description || actions) && (
          <div className="bg-white border-b border-neutral-200 px-4 sm:px-6 py-5">
            <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4">
              <div className="min-w-0">
                {title && <h1 className="text-[22px] font-bold tracking-[-0.02em] text-neutral-900">{title}</h1>}
                {description && <p className="mt-1 text-[14px] text-neutral-600 leading-relaxed max-w-2xl">{description}</p>}
              </div>
              {actions && <div className="flex items-center gap-2 shrink-0">{actions}</div>}
            </div>
          </div>
        )}

        {/* Content */}
        <main className="flex-1 p-4 sm:p-6">
          <div className="max-w-7xl mx-auto">
            {showAccountBanner && (
              <div role="alert" className={`mb-4 p-3 rounded-xl border text-[13px] text-neutral-800 ${account.isReadOnly ? 'border-danger-100 bg-danger-50' : 'border-warning-100 bg-warning-50'}`}>
                <strong>{account.isReadOnly ? 'Read-only. ' : 'Payment overdue. '}</strong>{account.banner}
              </div>
            )}
            {preview && (
              <div role="note" className="mb-4 p-3 rounded-xl border border-warning-100 bg-warning-50 text-[13px] text-neutral-800">
                <strong>Preview with sample data.</strong> This page is not connected to your school&apos;s records yet.
              </div>
            )}
            {children}
          </div>
        </main>

        {/* Footer */}
        <footer className="border-t border-neutral-200 bg-white px-4 sm:px-6 py-3 flex flex-col sm:flex-row items-center justify-between gap-2 text-[11px] text-neutral-500">
          <div className="flex items-center gap-3">
            <span>© 2026 LearnCloud • HQ Bulawayo, ZW • 150-2000 learners</span>
            <span className="hidden sm:inline-flex items-center gap-1.5">
              <span className="w-2 h-2 rounded-full bg-success-500 animate-pulse" />
              All systems operational
            </span>
          </div>
          <div className="flex items-center gap-3">
            <span className="hidden md:inline">Fast on slow connection • &lt;150KB • Tenant isolation • Audit logs</span>
            <span>v1.0 • #0F153A</span>
          </div>
        </footer>
      </div>

      {/* Command Palette */}
      <CommandPalette open={commandOpen} onClose={() => setCommandOpen(false)} />

      {/* Mobile Bottom Nav - Thumb Zone (For Teacher/Parent portals, but also here for quick access) */}
      <nav className="md:hidden fixed bottom-0 left-0 right-0 bg-white border-t border-neutral-200 flex justify-around py-1 z-30 safe-bottom">
        {[
          { id: "dashboard", label: "Home", icon: IconDashboard, path: "/dashboard" },
          { id: "students", label: "Students", icon: IconUsers, path: "/students" },
          { id: "fees", label: "Fees", icon: IconMoney, path: "/fees/invoices" },
          { id: "attendance", label: "Attend", icon: IconCalendar, path: "/attendance" },
          { id: "more", label: "More", icon: IconMenu, path: "/settings" },
        ].map(item => (
          <Link
            key={item.id}
            to={item.path}
            className={`flex flex-col items-center min-w-touch min-h-touch px-3 py-1 rounded-xl ${isActive(item.path) ? "text-primary-800 bg-primary-50" : "text-neutral-500"}`}
          >
            <item.icon className="w-5 h-5" />
            <span className="text-[10px] mt-0.5">{item.label}</span>
            {item.badge && <span className="absolute top-1 right-3 text-[8px] px-1 rounded-full bg-danger-500 text-white">{item.badge}</span>}
          </Link>
        ))}
      </nav>
    </div>
  );
}
