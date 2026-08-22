import AppShell from '../components/layout/AppShell';
import { Button } from '../components/ui/Button';

export default function Dashboard() {
  return (
    <AppShell
      title="Dashboard"
      description="Fees collected. Reports ready. Parents informed. Before lunch. Built in Bulawayo for schools 150-2,000 learners."
      breadcrumbs={[{ label: "Dashboard" }]}
      actions={
        <>
          <Button variant="secondary" size="sm">Export CSV</Button>
          <Button size="sm">Book a demo</Button>
        </>
      }
    >
      <div className="grid lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2 space-y-4">
          <div className="grid sm:grid-cols-3 gap-3">
            <div className="rounded-2xl bg-white border border-neutral-200 p-4 shadow-sm">
              <div className="text-[11px] font-semibold uppercase tracking-widest text-neutral-500">Total Students</div>
              <div className="mt-1 text-[28px] font-bold tracking-tight">542</div>
              <div className="mt-1 text-[12px] text-success-600">↑ 12 this term • 92% attendance</div>
            </div>
            <div className="rounded-2xl bg-white border border-neutral-200 p-4 shadow-sm">
              <div className="text-[11px] font-semibold uppercase tracking-widest text-neutral-500">Arrears &gt;60d</div>
              <div className="mt-1 text-[28px] font-bold tracking-tight text-warning-500">$12.4k</div>
              <div className="mt-1 text-[12px] text-neutral-500">42 learners • 8% of total</div>
            </div>
            <div className="rounded-2xl bg-white border border-neutral-200 p-4 shadow-sm">
              <div className="text-[11px] font-semibold uppercase tracking-widest text-neutral-500">Attendance Today</div>
              <div className="mt-1 text-[28px] font-bold tracking-tight text-success-600">92%</div>
              <div className="mt-1 text-[12px] text-neutral-500">498 present • 12 absent • 32 late</div>
            </div>
          </div>

          <div className="rounded-2xl bg-white border border-neutral-200 shadow-sm overflow-hidden">
            <div className="p-4 border-b border-neutral-100 flex items-center justify-between">
              <h3 className="font-semibold text-[14px]">Fee Arrears by Class • Overdue &gt;60 days</h3>
              <span className="text-[11px] px-2 py-0.5 rounded-full bg-warning-50 text-warning-600 border border-warning-200">42 learners</span>
            </div>
            <div className="divide-y divide-neutral-100">
              {[
                { name: "T. Ndlovu", class: "G5 Blue", amount: "$450", days: "62d", phone: "+263 77 123 4567" },
                { name: "L. Moyo", class: "F1 A", amount: "$780", days: "45d", phone: "+263 77 234 5678" },
                { name: "K. Dube", class: "F2 B", amount: "$320", days: "78d", phone: "+263 77 345 6789" },
              ].map((row, i) => (
                <div key={i} className="p-3 flex items-center gap-3 hover:bg-neutral-50 transition">
                  <div className="w-8 h-8 rounded-full bg-primary-50 text-primary-800 grid place-items-center font-bold text-xs">{row.name[0]}</div>
                  <div className="flex-1 min-w-0">
                    <div className="text-[13px] font-medium truncate">{row.name} • {row.class}</div>
                    <div className="text-[11px] text-neutral-500">{row.phone} • Billing: Mother</div>
                  </div>
                  <div className="text-right">
                    <div className="text-[13px] font-bold text-danger-600">{row.amount}</div>
                    <div className="text-[11px] text-neutral-500">{row.days} overdue</div>
                  </div>
                </div>
              ))}
            </div>
            <div className="p-3 bg-neutral-50 border-t border-neutral-100 flex justify-between items-center">
              <span className="text-[12px] text-neutral-600">Total 42 learners • $12.4k • Prints in greyscale with icons</span>
              <Button variant="link" size="sm">View all arrears →</Button>
            </div>
          </div>
        </div>

        <div className="space-y-4">
          <div className="rounded-2xl bg-primary-950 text-white p-4">
            <div className="text-[11px] font-semibold uppercase tracking-widest text-white/50">Setup Progress</div>
            <div className="mt-2 text-[22px] font-bold leading-tight">Petra High is 78% set up</div>
            <div className="mt-2 h-2 bg-white/10 rounded-full overflow-hidden">
              <div className="h-full w-[78%] bg-white rounded-full" />
            </div>
            <div className="mt-3 space-y-2 text-[13px]">
              <div className="flex items-center gap-2"><span className="w-5 h-5 rounded-full bg-success-500 grid place-items-center text-white text-[10px]">✓</span> School profile, branding #0F153A</div>
              <div className="flex items-center gap-2"><span className="w-5 h-5 rounded-full bg-success-500 grid place-items-center text-white text-[10px]">✓</span> Academic year, terms, classes</div>
              <div className="flex items-center gap-2"><span className="w-5 h-5 rounded-full bg-white/20 grid place-items-center text-white text-[10px]">3</span> Import learners (next)</div>
            </div>
            <Button variant="secondary" size="sm" className="mt-4 w-full bg-white text-primary-900">Import learners CSV →</Button>
          </div>

          <div className="rounded-2xl bg-white border border-neutral-200 p-4 shadow-sm">
            <h3 className="font-semibold text-[13px]">Today's Registers to Mark • 3</h3>
            <div className="mt-3 space-y-2">
              {["G5 Blue • Daily", "F1 A • Period 2 Math", "F2 B • Period 4 English"].map((r, i) => (
                <div key={i} className="flex items-center justify-between p-2.5 rounded-xl border border-neutral-200 hover:border-primary-200 hover:bg-primary-50 transition">
                  <span className="text-[13px] font-medium">{r}</span>
                  <span className="text-[11px] px-2 py-0.5 rounded-full bg-warning-500 text-white">Due</span>
                </div>
              ))}
            </div>
          </div>

          <div className="rounded-2xl bg-white border border-neutral-200 p-4 shadow-sm">
            <h3 className="font-semibold text-[13px]">Recent Audit • Security Trail</h3>
            <div className="mt-3 space-y-2 text-[12px]">
              <div className="flex gap-2"><span className="text-neutral-400">08:42</span><span>T. Ndlovu marked G5 Blue attendance • 30/40 present</span></div>
              <div className="flex gap-2"><span className="text-neutral-400">08:15</span><span>Bursar issued invoice INV-2026-001 for $450 • Fee calc decimal(18,2)</span></div>
              <div className="flex gap-2"><span className="text-neutral-400">07:30</span><span>Platform admin attempted subdomain/token mismatch → 403 + audit security_violation</span></div>
            </div>
          </div>
        </div>
      </div>
    </AppShell>
  );
}
