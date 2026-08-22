/**
 * Billing Screens - How schools pay you
 * Tenant billing: subscription state, invoices, payment link, read-only banner, plan change upgrade immediate pro-rata / downgrade next period
 * Suspension: read-only with clear banner and payment link, never delete data, never lock out entirely
 */

import React, { useState, useEffect } from 'react';
import { apiFetch, getAccessToken, setAccessToken } from '../../LearnCloud.Web/src/lib/apiClient.js'; // SECURITY C2 FIX

const API = "/api/billing";
const PLATFORM_API = "/api/platform";

export function ReadOnlyBanner() {
  const [status,setStatus]=useState(null);
  useEffect(()=>{
    apiFetch(`${API}/read-only-status`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setStatus);
  },[]);
  if(!status || !status.isReadOnly) return null;
  return (
    <div className="bg-warning-100 border-b-2 border-warning-500 text-warning-800 p-3 text-sm flex justify-between items-center">
      <div>
        <strong>⚠ {status.state} — Read-Only Mode:</strong> {status.banner || "Your account is read-only due to billing. You can view and export your records, but cannot edit. Never delete data, never lock out entirely."}
        <br/><span className="text-xs">Data retained 7 years per Ministry, payment link remains, win-back possible.</span>
      </div>
      <a href={status.paymentLink||"/billing/pay"} className="ml-4 px-4 py-2 rounded bg-primary-800 text-white text-sm font-semibold whitespace-nowrap">Pay Now to Reactivate</a>
    </div>
  );
}

export function TenantBilling() {
  const [sub,setSub]=useState(null);
  const [invoices,setInvoices]=useState([]);
  const [plans,setPlans]=useState([]);
  const [newPlanId,setNewPlanId]=useState("");

  useEffect(()=>{
    apiFetch(`${API}/subscription`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setSub);
    apiFetch(`${API}/invoices`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setInvoices);
    // Plans list would be from /api/plans or /api/platform/plans - mock
    apiFetch(`/api/platform/plans`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setPlans).catch(()=>setPlans([{id:1,name:"Starter",code:"starter"},{id:2,name:"Growth",code:"growth"},{id:3,name:"Scale",code:"scale"}]));
  },[]);

  async function changePlan(){
    const res=await apiFetch(`${API}/plan/change`,{method:"POST", headers:{"Content-Type":"application/json", Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}, body: JSON.stringify({newPlanId:parseInt(newPlanId), reason:"School requested upgrade/downgrade"})});
    if(res.ok){
      const data=await res.json();
      alert(data.message);
      window.location.reload();
    } else alert("Failed: "+await res.text());
  }

  if(!sub) return <div className="p-4">Loading billing...</div>;

  const subData = sub.subscription;
  const isTrialing = subData.state==="Trialing";
  const isReadOnly = sub.isReadOnly;

  return (
    <div className="p-4 space-y-4 max-w-5xl mx-auto">
      {isReadOnly && <ReadOnlyBanner />}

      <div className="bg-white border rounded-lg p-4">
        <h2 className="font-bold text-lg">Subscription — {subData.plan} ({subData.planCode}) — {subData.state}</h2>
        <div className="grid sm:grid-cols-2 gap-3 mt-3 text-sm">
          <div>Billing Period: {new Date(subData.currentPeriodStart).toLocaleDateString()} - {new Date(subData.currentPeriodEnd).toLocaleDateString()} (aligned to school term)</div>
          <div>Billable Learners Snapshot at Period Start: {subData.billableLearnerCount} (used as billable count)</div>
          <div>Current Learners: {subData.currentLearnerCount} / Limit {subData.learnerLimit} {subData.currentLearnerCount>subData.learnerLimit&&<span className="text-danger-600">OVER LIMIT</span>}</div>
          <div>Price per Learner per Term: ${subData.pricePerLearner} — Min Charge ${subData.minimumCharge} — Included SMS {subData.includedSms}</div>
          <div>Modules Included: {subData.includedModules}</div>
          {subData.trialEndsAt && <div>Trial Ends: {new Date(subData.trialEndsAt).toLocaleDateString()} — 14 days no card, reminders day7,12,expiry, 30-day read-only window before archival</div>}
          {subData.readOnlyUntil && <div>Read-Only Until: {new Date(subData.readOnlyUntil).toLocaleDateString()} — 30-day window after expiry before archival, data never deleted</div>}
          {subData.pendingPlanId && <div>Pending Downgrade: Plan {subData.pendingPlanId} effective at next period {new Date(subData.pendingPlanEffectiveAt).toLocaleDateString()} — downgrade takes effect at next period</div>}
        </div>
        {sub.banner && <div className="mt-3 p-3 rounded bg-warning-50 border border-warning-200 text-sm">{sub.banner}</div>}
        {sub.paymentLink && <div className="mt-2"><a href={sub.paymentLink} className="px-4 py-2 rounded bg-primary-800 text-white text-sm">Payment Link — Never lock out entirely, win-back possible</a></div>}
      </div>

      <div className="bg-white border rounded-lg p-4">
        <h3 className="font-semibold">Plan Change — Upgrade Immediate with Pro-Rata Charge, Downgrade Next Period, Both Logged</h3>
        <div className="mt-2 flex gap-2">
          <select value={newPlanId} onChange={e=>setNewPlanId(e.target.value)} className="h-10 px-2 border rounded text-sm"><option value="">Select new plan</option>{plans.map(p=><option key={p.id} value={p.id}>{p.name} - {p.code}</option>)}</select>
          <button onClick={changePlan} className="px-4 py-2 rounded bg-primary-800 text-white text-sm">Change Plan</button>
        </div>
        <p className="text-xs text-neutral-500 mt-2">Upgrade: effect immediate, pro-rata charge = priceDiff * learners * daysRemaining/daysInPeriod. Downgrade: effect next period, pendingPlanId set. Both logged in plan_change_logs with reason, admin, pro-rata charge, audited.</p>
      </div>

      <div className="bg-white border rounded-lg p-4">
        <h3 className="font-semibold">Platform Invoices to School — Numbering PLAT-2026-00001, Line Items, Due Date, Manual Capture</h3>
        <div className="mt-2 max-h-96 overflow-auto border rounded">
          <table className="w-full text-xs border-collapse">
            <thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1">Number</th><th className="border p-1">Issue/Due</th><th className="border p-1">Subtotal</th><th className="border p-1">Total</th><th className="border p-1">Balance</th><th className="border p-1">Status</th></tr></thead>
            <tbody>{invoices.map(inv=><tr key={inv.id}><td className="border p-1">{inv.invoiceNumber}</td><td className="border p-1">{new Date(inv.issueDate).toLocaleDateString()} / {new Date(inv.dueDate).toLocaleDateString()}</td><td className="border p-1">{inv.subtotal}</td><td className="border p-1">{inv.totalAmount}</td><td className="border p-1 font-bold">{inv.balanceDue}</td><td className="border p-1">{inv.status}</td></tr>)}</tbody>
          </table>
        </div>
        <p className="text-xs text-neutral-500 mt-2">Billing period aligned to school term, enrolment snapshot at period start used as billable count. Invoices numbering, line items learner count, due date, manual capture at first gateway later. Printable and downloadable.</p>
      </div>
    </div>
  );
}

// Re-export admin console from previous file for completeness
export { default as AdminConsole } from "./AdminConsole.jsx";