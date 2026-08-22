/**
 * Platform Admin Console - Tenant list with subscription state, revenue by month, trials converting, tenants at risk, manual override
 * Only PLATFORM_SUPERADMIN role
 */

import React, { useState, useEffect } from 'react';
import { apiFetch, getAccessToken, setAccessToken } from '../../LearnCloud.Web/src/lib/apiClient.js'; // SECURITY C2 FIX

const API = "/api/platform";

export default function AdminConsole() {
  const [tab,setTab]=useState("tenants");
  return (
    <div className="min-h-screen bg-neutral-50">
      <header className="bg-primary-950 text-white p-4">
        <h1 className="text-xl font-bold">LearnCloud Platform Admin — Revenue Console</h1>
        <p className="text-xs opacity-70">How schools pay you, not how learners pay school. Trial 14 days no card, reminders day7/12/expiry, 30-day read-only before archival. Never delete data.</p>
      </header>
      <nav className="bg-white border-b flex gap-1 p-2 overflow-auto">
        {[
          {id:"tenants",label:"Tenants + State"},
          {id:"revenue",label:"Revenue by Month"},
          {id:"trials",label:"Trials Converting"},
          {id:"at-risk",label:"Tenants at Risk"},
          {id:"overrides",label:"Manual Overrides - Trial Extend / Credit Invoice"},
        ].map(t=><button key={t.id} onClick={()=>setTab(t.id)} className={`px-3 py-1.5 rounded text-sm border ${tab===t.id?"bg-primary-800 text-white":"bg-white"}`}>{t.label}</button>)}
      </nav>
      <main className="p-4 max-w-7xl mx-auto">
        {tab==="tenants" && <TenantList />}
        {tab==="revenue" && <RevenueByMonth />}
        {tab==="trials" && <TrialsConverting />}
        {tab==="at-risk" && <TenantsAtRisk />}
        {tab==="overrides" && <ManualOverrides />}
      </main>
    </div>
  );
}

function TenantList() {
  const [tenants,setTenants]=useState([]);
  const [filter,setFilter]=useState("");
  useEffect(()=>{ apiFetch(`${API}/tenants?state=${filter}`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setTenants); },[filter]);
  return (
    <div className="bg-white border rounded-lg p-4">
      <div className="flex justify-between items-center">
        <h2 className="font-bold">Tenant List with Subscription State</h2>
        <select value={filter} onChange={e=>setFilter(e.target.value)} className="h-8 px-2 border rounded text-sm"><option value="">All</option><option value="Trialing">Trialing</option><option value="Active">Active</option><option value="PastDue">PastDue</option><option value="Suspended">Suspended</option><option value="Expired">Expired</option><option value="Cancelled">Cancelled</option></select>
      </div>
      <div className="mt-3 overflow-auto max-h-[600px]">
        <table className="w-full text-xs border-collapse">
          <thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1">Tenant</th><th className="border p-1">Slug</th><th className="border p-1">Plan</th><th className="border p-1">State</th><th className="border p-1">Billable</th><th className="border p-1">Current</th><th className="border p-1">Limit</th><th className="border p-1">Trial Ends</th><th className="border p-1">Period</th></tr></thead>
          <tbody>
            {tenants.map(t=>(
              <tr key={t.tenantId} className={`${t.state==="PastDue"?"bg-warning-50":t.state==="Suspended"?"bg-danger-50":t.state==="Trialing"?"bg-primary-50":""}`}>
                <td className="border p-1">{t.tenantName}</td>
                <td className="border p-1">{t.slug}</td>
                <td className="border p-1">{t.plan} ({t.planCode})</td>
                <td className="border p-1"><span className={`px-1.5 py-0.5 rounded text-[10px] ${t.state==="Active"?"bg-success-500 text-white":t.state==="PastDue"?"bg-warning-500 text-white":t.state==="Suspended"?"bg-danger-500 text-white":"bg-neutral-200"}`}>{t.state}</span></td>
                <td className="border p-1">{t.billableLearnerCount}</td>
                <td className="border p-1">{t.currentLearnerCount} {t.isOverLimit&&<span className="text-danger-600">OVER</span>}</td>
                <td className="border p-1">{t.learnerLimit}</td>
                <td className="border p-1">{t.trialEndsAt?new Date(t.trialEndsAt).toLocaleDateString():""}</td>
                <td className="border p-1">{new Date(t.currentPeriodStart).toLocaleDateString()} - {new Date(t.currentPeriodEnd).toLocaleDateString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function RevenueByMonth() {
  const [data,setData]=useState(null);
  const [year,setYear]=useState(new Date().getFullYear());
  useEffect(()=>{ apiFetch(`${API}/revenue/by-month?year=${year}`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setData); },[year]);
  if(!data) return <div className="p-4">Loading revenue...</div>;
  return (
    <div className="bg-white border rounded-lg p-4">
      <h2 className="font-bold">Revenue by Month {year} — Total {data.totalRevenue.toFixed(2)} USD</h2>
      <div className="mt-3 grid grid-cols-12 gap-1">
        {data.byMonth.map(m=>(
          <div key={m.month} className="border rounded p-2 text-center">
            <div className="text-xs font-medium">{m.monthName}</div>
            <div className="text-sm font-bold">${m.revenue.toFixed(0)}</div>
            <div className="text-[10px] text-neutral-500">{m.invoicesPaid} paid</div>
            <div className="w-full bg-neutral-200 h-1 mt-1 rounded"><div className="bg-primary-800 h-1 rounded" style={{width:`${Math.min(100, m.revenue/500*100)}%`}}></div></div>
          </div>
        ))}
      </div>
      <div className="mt-4 text-xs text-neutral-600">Platform invoices to school, numbering PLAT-2026-00001, line items learner count snapshot at period start, due date, manual capture. Dunning job moves overdue to past_due, escalating reminders, then suspends after grace.</div>
    </div>
  );
}

function TrialsConverting() {
  const [data,setData]=useState(null);
  useEffect(()=>{ apiFetch(`${API}/trials/converting`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setData); },[]);
  if(!data) return <div className="p-4">Loading trials...</div>;
  return (
    <div className="bg-white border rounded-lg p-4">
      <h2 className="font-bold">Trials Converting — 14 days no card, reminders day7,12,expiry, 30-day read-only window before archival</h2>
      <div className="mt-2 flex gap-3">
        <div className="p-2 rounded bg-primary-50 border">Total Trials: {data.totalTrials}</div>
        <div className="p-2 rounded bg-success-50 border">Converting (7+ days): {data.converting}</div>
        <div className="p-2 rounded bg-warning-50 border">At Risk ≤2 days: {data.atRisk}</div>
      </div>
      <div className="mt-3 max-h-96 overflow-auto border rounded">
        <table className="w-full text-xs border-collapse">
          <thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1">School</th><th className="border p-1">Slug</th><th className="border p-1">Started</th><th className="border p-1">Ends</th><th className="border p-1">Days Left</th><th className="border p-1">Plan</th></tr></thead>
          <tbody>{data.trials.map(t=><tr key={t.tenantId} className={t.isAtRisk?"bg-warning-50":""}><td className="border p-1">{t.tenantName}</td><td className="border p-1">{t.slug}</td><td className="border p-1">{new Date(t.trialStartedAt).toLocaleDateString()}</td><td className="border p-1">{new Date(t.trialEndsAt).toLocaleDateString()}</td><td className="border p-1">{t.daysLeft}</td><td className="border p-1">{t.plan}</td></tr>)}</tbody>
        </table>
      </div>
      <div className="mt-2 text-xs text-neutral-500">Trial reminders: day7 email, day12 urgency, expiry notice with 30-day read-only window and payment link. Never delete data, never lock out entirely — win-back chance.</div>
    </div>
  );
}

function TenantsAtRisk() {
  const [data,setData]=useState(null);
  useEffect(()=>{ apiFetch(`${API}/tenants/at-risk`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setData); },[]);
  if(!data) return <div className="p-4">Loading at-risk...</div>;
  return (
    <div className="bg-white border rounded-lg p-4">
      <h2 className="font-bold">Tenants at Risk — PastDue, Suspended, Expired, Over Learner Limit</h2>
      <p className="text-xs text-neutral-600">{data.count} tenants at risk</p>
      <div className="mt-3 max-h-96 overflow-auto border rounded">
        <table className="w-full text-xs border-collapse">
          <thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1">School</th><th className="border p-1">State</th><th className="border p-1">Billable/Current/Limit</th><th className="border p-1">Reason</th></tr></thead>
          <tbody>{data.tenants.map(t=><tr key={t.tenantId} className={t.state==="PastDue"?"bg-warning-50":t.state==="Suspended"?"bg-danger-50":"bg-neutral-50"}><td className="border p-1">{t.tenantName}</td><td className="border p-1">{t.state}</td><td className="border p-1">{t.billable}/{t.current}/{t.limit} {t.overLimit&&<span className="text-danger-600">OVER</span>}</td><td className="border p-1">{t.reason}</td></tr>)}</tbody>
        </table>
      </div>
    </div>
  );
}

function ManualOverrides() {
  const [tenantId,setTenantId]=useState("");
  const [newDate,setNewDate]=useState(new Date(Date.now()+7*24*3600*1000).toISOString().slice(0,10));
  const [reason,setReason]=useState("");
  const [invoiceId,setInvoiceId]=useState("");
  const [creditAmount,setCreditAmount]=useState("");
  const [overrides,setOverrides]=useState([]);

  async function load(){ apiFetch(`${API}/overrides`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setOverrides); }
  useEffect(()=>{ load(); },[]);

  async function extendTrial(){
    const res=await apiFetch(`${API}/overrides/extend-trial`,{method:"POST", headers:{"Content-Type":"application/json", Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}, body: JSON.stringify({tenantId:parseInt(tenantId), newTrialEndsAt:newDate, reason})});
    if(res.ok){ alert("Trial extended — audited"); load(); } else alert("Failed: "+await res.text());
  }

  async function creditInvoice(){
    const res=await apiFetch(`${API}/overrides/credit-invoice`,{method:"POST", headers:{"Content-Type":"application/json", Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}, body: JSON.stringify({tenantId:parseInt(tenantId), invoiceId:parseInt(invoiceId), amount:parseFloat(creditAmount), reason})});
    if(res.ok){ alert("Invoice credited — audited"); load(); } else alert("Failed: "+await res.text());
  }

  return (
    <div className="bg-white border rounded-lg p-4 space-y-6">
      <div>
        <h2 className="font-bold">Manual Override — Extend Trial or Credit Invoice, Fully Audited, Never Delete Data</h2>
        <p className="text-xs text-neutral-600">Platform admin can extend trial, credit invoice, extend suspension grace. All actions audited with reason, adminUserId, old/new values.</p>
      </div>

      <div className="grid md:grid-cols-2 gap-4">
        <div className="border rounded-lg p-3 bg-primary-50">
          <h3 className="font-semibold text-sm">Extend Trial — Reason ≥10 chars required</h3>
          <input placeholder="TenantId" value={tenantId} onChange={e=>setTenantId(e.target.value)} className="mt-2 w-full h-9 px-2 border rounded text-sm" />
          <input type="date" value={newDate} onChange={e=>setNewDate(e.target.value)} className="mt-2 w-full h-9 px-2 border rounded text-sm" />
          <input placeholder="Reason — e.g. School requested extra week for board approval, 15 Jan" value={reason} onChange={e=>setReason(e.target.value)} className="mt-2 w-full h-9 px-2 border rounded text-sm" />
          <button onClick={extendTrial} className="mt-2 w-full py-2 rounded bg-primary-800 text-white text-sm">Extend Trial — Audited</button>
        </div>

        <div className="border rounded-lg p-3 bg-warning-50">
          <h3 className="font-semibold text-sm">Credit Invoice — Reason ≥10 chars</h3>
          <input placeholder="TenantId" value={tenantId} onChange={e=>setTenantId(e.target.value)} className="mt-1 w-full h-9 px-2 border rounded text-sm" />
          <input placeholder="InvoiceId" value={invoiceId} onChange={e=>setInvoiceId(e.target.value)} className="mt-1 w-full h-9 px-2 border rounded text-sm" />
          <input placeholder="Amount e.g. 50.00" value={creditAmount} onChange={e=>setCreditAmount(e.target.value)} className="mt-1 w-full h-9 px-2 border rounded text-sm" />
          <input placeholder="Reason — e.g. Credit for overcharge, approved by finance head" value={reason} onChange={e=>setReason(e.target.value)} className="mt-1 w-full h-9 px-2 border rounded text-sm" />
          <button onClick={creditInvoice} className="mt-2 w-full py-2 rounded bg-warning-600 text-white text-sm">Credit Invoice — Audited</button>
        </div>
      </div>

      <div>
        <h3 className="font-semibold text-sm">Recent Overrides — Fully Audited</h3>
        <div className="mt-2 max-h-80 overflow-auto border rounded">
          <table className="w-full text-xs border-collapse">
            <thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1">Tenant</th><th className="border p-1">Type</th><th className="border p-1">Details</th><th className="border p-1">Reason</th><th className="border p-1">Admin</th><th className="border p-1">Date</th></tr></thead>
            <tbody>{overrides.map(o=><tr key={o.id}><td className="border p-1">{o.tenantId}</td><td className="border p-1">{o.overrideType}</td><td className="border p-1">{o.detailsJson}</td><td className="border p-1">{o.reason}</td><td className="border p-1">{o.adminUserId}</td><td className="border p-1">{new Date(o.createdAtOverride||o.createdAt).toLocaleString()}</td></tr>)}</tbody>
          </table>
        </div>
      </div>

      <div className="p-3 rounded bg-neutral-50 border text-xs">
        <strong>Suspension behaviour:</strong> Tenant becomes read-only with clear banner and payment link. Never delete data, never lock school out entirely — banner: "Your account is suspended due to non-payment. You can view and export your records, but cannot edit. Pay now to reactivate." Payment link remains. Data retained 7 years per Ministry. That destroys any chance of winning them back if deleted — so we never delete.
        <br/><br/>
        <strong>Feature gating:</strong> Middleware blocks endpoints not included in plan, returns 402 upgrade_required with requiredFeature, currentPlan, upgradeUrl. Example Starter plan may exclude messaging, Scale includes all.
        <br/><br/>
        <strong>Plan changes:</strong> Upgrade immediate with pro-rata charge (priceDiff * learners * daysRemaining/daysInPeriod). Downgrade at next period, pendingPlanId set, effective next period start.
      </div>
    </div>
  );
}