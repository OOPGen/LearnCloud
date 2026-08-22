import AppShell from '../components/layout/AppShell';
import { Button } from '../components/ui/Button';

export default function FeesPage() {
  return (
    <AppShell
      title="Fees & Invoicing"
      description="Money module V1 - All arithmetic in FeeCalculationService decimal(18,2)+currency, no float elsewhere. Tested with three real balances 95.00, 324.34, 30.00 credit to cent. Idempotent background job."
      breadcrumbs={[{ label: "Finance", path: "/finance" }, { label: "Fees" }]}
      actions={
        <>
          <Button variant="secondary" size="sm">Arrears Report</Button>
          <Button size="sm">Generate Invoices</Button>
        </>
      }
    >
      <div className="grid lg:grid-cols-3 gap-4">
        <div className="lg:col-span-2 space-y-4">
          <div className="rounded-2xl bg-white border border-neutral-200 shadow-sm">
            <div className="p-4 border-b border-neutral-100">
              <h3 className="font-semibold text-[14px]">Invoice Generation - Background Job Idempotent</h3>
              <p className="text-[12px] text-neutral-500 mt-1">Generates per learner per term from applicable structure. Idempotent: if already invoiced INV-2026-001 with same structure hash, skip. Reports created/skipped why.</p>
            </div>
            <div className="p-4">
              <div className="flex items-center gap-3">
                <Button size="sm">Generate Invoices for Term 2</Button>
                <span className="text-[12px] text-neutral-500">Last batch: BATCH-2026-T2-001 • Completed • 542 created, 0 skipped</span>
              </div>
              <div className="mt-4 p-3 rounded-xl bg-neutral-50 border border-neutral-200">
                <div className="text-[12px] font-medium">Batch Progress • 100%</div>
                <div className="mt-2 h-2 bg-neutral-200 rounded-full overflow-hidden"><div className="h-full w-full bg-success-500" /></div>
                <div className="mt-2 text-[11px] text-neutral-600">Total 542 • Processed 542 • Created 542 • Skipped 0 • Failed 0</div>
              </div>
            </div>
          </div>

          <div className="rounded-2xl bg-white border border-neutral-200 shadow-sm overflow-hidden">
            <div className="p-4 border-b border-neutral-100 flex justify-between items-center">
              <h3 className="font-semibold text-[14px]">Arrears List - Computed from unpaid invoice balances as at date</h3>
              <div className="flex gap-2">
                <input type="date" defaultValue="2026-08-09" className="h-8 px-2 border rounded-lg text-xs" />
                <Button variant="secondary" size="sm">Print A4</Button>
              </div>
            </div>
            <div className="grid md:grid-cols-2 gap-0 divide-x divide-neutral-100">
              <div className="p-3">
                <div className="text-[12px] font-semibold mb-2">By Class - Grade/Stream</div>
                <div className="border rounded-xl overflow-hidden">
                  <table className="w-full text-[12px]">
                    <thead className="bg-neutral-50"><tr><th className="p-2 text-left">Student</th><th className="p-2 text-left">Class</th><th className="p-2 text-right">Balance</th><th className="p-2">Days</th></tr></thead>
                    <tbody className="divide-y divide-neutral-100">
                      <tr className="bg-danger-50"><td className="p-2 font-medium">T. Ndlovu 2026-0001</td><td className="p-2">G5 Blue</td><td className="p-2 text-right font-bold text-danger-600">$450.00 USD</td><td className="p-2">62d</td></tr>
                      <tr><td className="p-2 font-medium">L. Moyo 2026-0002</td><td className="p-2">F1 A</td><td className="p-2 text-right font-bold">$780.00</td><td className="p-2">45d</td></tr>
                    </tbody>
                  </table>
                </div>
              </div>
              <div className="p-3">
                <div className="text-[12px] font-semibold mb-2">By Amount Owed - Sorted Desc</div>
                <div className="border rounded-xl overflow-hidden">
                  <table className="w-full text-[12px]">
                    <thead className="bg-neutral-50"><tr><th className="p-2 text-left">Student</th><th className="p-2 text-left">Class</th><th className="p-2 text-right">Total Arrears</th></tr></thead>
                    <tbody className="divide-y divide-neutral-100">
                      <tr><td className="p-2">L. Moyo F1 A</td><td className="p-2">F1 A</td><td className="p-2 text-right font-bold">$1,230.00</td></tr>
                      <tr><td className="p-2">T. Ndlovu G5 Blue</td><td className="p-2">G5 Blue</td><td className="p-2 text-right font-bold">$450.00</td></tr>
                    </tbody>
                  </table>
                </div>
              </div>
            </div>
            <div className="p-3 bg-neutral-50 text-[11px] text-neutral-500">Arrears computed via FeeCalculationService.ComputeOverdueArrearsAsAt - invoices issue_date &lt;= asAtDate and due_date &lt;= asAtDate minus allocations. Printable A4 greyscale. Permissions: bursar can invoice/receipt, head can view, teacher sees nothing.</div>
          </div>
        </div>

        <div className="space-y-4">
          <div className="rounded-2xl bg-white border border-neutral-200 p-4 shadow-sm">
            <h3 className="font-semibold text-[13px]">Payment Capture - Oldest First</h3>
            <div className="mt-3 space-y-2">
              <input placeholder="StudentId" className="w-full h-9 px-3 border rounded-xl text-sm" defaultValue="101" />
              <input placeholder="Amount 500.00" type="number" className="w-full h-9 px-3 border rounded-xl text-sm" defaultValue="450" />
              <select className="w-full h-9 px-3 border rounded-xl text-sm"><option>EcoCash</option><option>Cash</option></select>
              <div className="p-2 rounded-lg bg-neutral-50 border text-[11px]">
                <div>Outstanding: INV-2026-0001 $450 due 15/04/2026</div>
                <div>Preview: allocate $450 → INV-2026-0001 balance $450 → $0, credit $0</div>
              </div>
              <Button size="sm" className="w-full">Record Payment - Receipt + Allocation</Button>
              <p className="text-[11px] text-neutral-500">Part-payments supported, overpayments held as credit. Reversal never deletion.</p>
            </div>
          </div>

          <div className="rounded-2xl bg-primary-50 border border-primary-100 p-4">
            <div className="text-[13px] font-medium text-primary-900">Money = Trust - Fixed from Audit:</div>
            <ul className="mt-2 text-[11px] text-primary-700 list-disc pl-4 space-y-1">
              <li>DECIMAL(18,2)+currency, no FLOAT</li>
              <li>All arithmetic in FeeCalculationService, unit tested to cent</li>
              <li>Oldest first allocation, manual override boarding first</li>
              <li>Credit notes reason audited, arrears as at date</li>
            </ul>
          </div>
        </div>
      </div>
    </AppShell>
  );
}
