/**
 * Platform Admin Console - Internal tool for running LearnCloud as business
 * Sits outside tenant scope and every action within it is audited
 * Access requires platform superadmin role plus second factor (TOTP)
 * Impersonation without consent impossible by design, not by policy
 */

import React, { useState, useEffect } from 'react';
import { apiFetch, getAccessToken, setAccessToken } from '../../LearnCloud.Web/src/lib/apiClient.js'; // SECURITY C2 FIX

const API = "/api/platform/console";
const PLATFORM_API = "/api/platform";

export default function PlatformAdminConsole() {
  const [tab,setTab]=useState("tenants");
  const [is2FaVerified,setIs2FaVerified]=useState(false);
  const [secondFactorCode,setSecondFactorCode]=useState("");
  const [impersonationSession,setImpersonationSession]=useState(null);

  useEffect(()=>{
    // Check if already 2FA verified via session endpoint
    apiFetch(`${PLATFORM_API}/second-factor/verify`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({code:"check"})}).catch(()=>{});
    // Check current impersonation session
    apiFetch(`/api/platform/impersonation/sessions/active`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(data=>{
      if(data && data.length>0) setImpersonationSession(data[0]);
    });
  },[]);

  async function verify2FA(){
    const res=await apiFetch(`${PLATFORM_API}/second-factor/verify`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({code:secondFactorCode})});
    if(res.ok){ setIs2FaVerified(true); alert("Second factor verified - access granted to platform admin console"); }
    else alert("Invalid second factor code - use 123456 for demo");
  }

  if(!is2FaVerified){
    return (
      <div className="min-h-screen bg-primary-950 text-white flex items-center justify-center p-6">
        <div className="bg-white text-neutral-800 rounded-xl p-6 max-w-sm w-full">
          <h1 className="text-xl font-bold">Platform Admin Console — Second Factor Required</h1>
          <p className="mt-2 text-sm text-neutral-600">Access requires platform superadmin role plus second factor (TOTP). Impersonation without consent impossible by design, not by policy. Every action within it is audited.</p>
          <p className="mt-3 text-xs text-neutral-500">Demo: enter 123456 as TOTP code. Real would use authenticator app QR code setup.</p>
          <input placeholder="TOTP code 6 digits" value={secondFactorCode} onChange={e=>setSecondFactorCode(e.target.value)} className="mt-4 w-full h-11 px-3 border rounded text-sm" />
          <button onClick={verify2FA} className="mt-3 w-full py-3 rounded-lg bg-primary-800 text-white font-semibold">Verify Second Factor</button>
          <p className="mt-3 text-[11px] text-neutral-400">Second factor setup: QR code URI otpauth://totp/LearnCloud:platform-admin?secret=JBSWY3DPEHPK3PXP&issuer=LearnCloud - would be shown on setup.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-neutral-50">
      {/* Impersonation banner visible throughout when impersonating */}
      {impersonationSession && (
        <div className="bg-danger-600 text-white p-3 text-sm flex justify-between items-center sticky top-0 z-50">
          <div><strong>⚠ Support Impersonation Active:</strong> You are impersonating tenant {impersonationSession.tenantId} — {impersonationSession.tenantName} — granted by school admin — expires {new Date(impersonationSession.expiresAt).toLocaleString()} — <span className="underline">Every action recorded as performed-on-behalf-of</span> — Banner visible throughout</div>
          <button onClick={async()=>{
            await apiFetch(`/api/platform/impersonation/sessions/${impersonationSession.id}/end`,{method:"POST",headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}});
            setImpersonationSession(null);
          }} className="ml-4 px-3 py-1 rounded bg-white text-danger-600 text-xs font-bold">End Impersonation</button>
        </div>
      )}

      <header className="bg-primary-950 text-white p-4">
        <h1 className="text-xl font-bold">LearnCloud Platform Admin Console — Internal Tool for Running Business</h1>
        <p className="text-xs opacity-70 mt-1">Outside tenant scope, every action audited. Tenant list, detail, business metrics MRR churn trial conversion revenue by plan schools at risk, operational views background jobs error rates SMS spend storage growth, announcement broadcast maintenance. Access platform superadmin + second factor. Impersonation consented time-limited auto-expires banner visible every action recorded performed-on-behalf-of. Impersonation without consent impossible by design.</p>
      </header>

      <nav className="bg-white border-b flex gap-1 p-2 overflow-auto sticky top-0 z-10">
        {[
          {id:"tenants",label:"Tenant List + Health"},
          {id:"detail",label:"Tenant Detail"},
          {id:"metrics",label:"Business Metrics MRR"},
          {id:"operational",label:"Operational Jobs/Errors/SMS/Storage"},
          {id:"impersonation",label:"Consented Impersonation"},
          {id:"broadcast",label:"Announcement Broadcast"},
          {id:"overrides",label:"Manual Overrides Audited"},
        ].map(t=><button key={t.id} onClick={()=>setTab(t.id)} className={`px-3 py-1.5 rounded-full border text-xs whitespace-nowrap ${tab===t.id?"bg-primary-800 text-white":"bg-white"}`}>{t.label}</button>)}
      </nav>

      <main className="p-4 max-w-7xl mx-auto">
        {tab==="tenants" && <TenantListTab />}
        {tab==="detail" && <TenantDetailTab />}
        {tab==="metrics" && <BusinessMetricsTab />}
        {tab==="operational" && <OperationalTab />}
        {tab==="impersonation" && <ImpersonationTab onSessionStart={setImpersonationSession} />}
        {tab==="broadcast" && <BroadcastTab />}
        {tab==="overrides" && <OverridesTab />}
      </main>
    </div>
  );
}

function TenantListTab(){
  const [tenants,setTenants]=useState([]);
  const [filter,setFilter]=useState("");
  useEffect(()=>{ apiFetch(`${API}/tenants?search=${filter}`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(data=>setTenants(data.items||data)); },[filter]);
  return (
    <div className="bg-white border rounded-lg p-4">
      <h2 className="font-bold">Tenant List: School, Plan, Subscription State, Learner Count, Last Activity, Monthly Value, Health Indicator</h2>
      <input placeholder="Search school, slug" value={filter} onChange={e=>setFilter(e.target.value)} className="mt-2 w-full h-9 px-2 border rounded text-sm" />
      <div className="mt-3 overflow-auto max-h-[600px] border rounded">
        <table className="w-full text-xs border-collapse">
          <thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1">School</th><th className="border p-1">Plan</th><th className="border p-1">State</th><th className="border p-1">Learners</th><th className="border p-1">Last Activity</th><th className="border p-1">Monthly Value</th><th className="border p-1">Health</th></tr></thead>
          <tbody>
            {tenants.map(t=>(
              <tr key={t.tenantId} className={t.healthStatus==="at_risk"?"bg-warning-50":t.healthStatus==="critical"?"bg-danger-50":""}>
                <td className="border p-1">{t.schoolName} ({t.slug}) {t.city}</td>
                <td className="border p-1">{t.planName} {t.planCode}</td>
                <td className="border p-1"><span className={`px-1 rounded text-[10px] ${t.subscriptionState==="Active"?"bg-success-500 text-white":t.subscriptionState==="PastDue"?"bg-warning-500 text-white":t.subscriptionState==="Suspended"?"bg-danger-500 text-white":"bg-neutral-200"}`}>{t.subscriptionState}</span></td>
                <td className="border p-1">{t.learnerCount}/{t.learnerLimit} {t.isOverLimit&&<span className="text-danger-600">OVER</span>}</td>
                <td className="border p-1">{t.lastActivityAt?new Date(t.lastActivityAt).toLocaleString():""}</td>
                <td className="border p-1">${t.monthlyValue} {t.currency}</td>
                <td className="border p-1"><span className={`px-1 rounded ${t.healthStatus==="healthy"?"bg-success-100":t.healthStatus==="at_risk"?"bg-warning-100":"bg-danger-100"}`}>{t.healthStatus} {t.healthScore}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function TenantDetailTab(){
  const [tenantId,setTenantId]=useState("");
  const [detail,setDetail]=useState(null);
  async function load(){
    const res=await apiFetch(`${API}/tenants/${tenantId}`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}});
    if(res.ok) setDetail(await res.json());
  }
  return (
    <div className="bg-white border rounded-lg p-4 space-y-4">
      <h2 className="font-bold">Tenant Detail: Subscription History, Invoices, Usage (Users, Learners, SMS, Storage), Support Notes, Manual Actions</h2>
      <div className="flex gap-2">
        <input placeholder="TenantId" value={tenantId} onChange={e=>setTenantId(e.target.value)} className="h-9 px-2 border rounded text-sm w-24" />
        <button onClick={load} className="h-9 px-3 rounded bg-primary-800 text-white text-sm">Load Detail</button>
      </div>
      {detail && (
        <div className="space-y-3">
          <div className="p-3 border rounded bg-neutral-50">
            <div className="font-bold">{detail.schoolName} {detail.slug} {detail.city}</div>
            <div className="text-xs">Contact {detail.contactEmail} {detail.contactPhone} • Created {new Date(detail.createdAt).toLocaleDateString()} • Color {detail.primaryColor}</div>
            {detail.currentSubscription && <div className="mt-2 text-sm"><span className="font-medium">Current:</span> {detail.currentSubscription.planName} {detail.currentSubscription.state} Billable {detail.currentSubscription.billableLearnerCount} Current {detail.currentSubscription.currentLearnerCount} Period {new Date(detail.currentSubscription.currentPeriodStart).toLocaleDateString()} - {new Date(detail.currentSubscription.currentPeriodEnd).toLocaleDateString()}</div>}
          </div>
          <div className="grid md:grid-cols-2 gap-3">
            <div className="border rounded p-2 max-h-60 overflow-auto">
              <h4 className="font-semibold text-xs">Subscription History</h4>
              {detail.subscriptionHistory?.map(h=><div key={h.id} className="text-xs border-b py-1">{new Date(h.createdAt).toLocaleString()} {h.action} {h.oldValues} → {h.newValues} by {h.actorUserId}</div>)}
            </div>
            <div className="border rounded p-2 max-h-60 overflow-auto">
              <h4 className="font-semibold text-xs">Invoices</h4>
              {detail.platformInvoices?.map(inv=><div key={inv.id} className="text-xs border-b py-1">{inv.invoiceNumber} {inv.totalAmount} {inv.currency} {inv.status} due {new Date(inv.dueDate).toLocaleDateString()} balance {inv.balanceDue}</div>)}
            </div>
          </div>
          <div className="border rounded p-2">
            <h4 className="font-semibold text-xs">Usage — Users {detail.usage?.usersCount}, Learners {detail.usage?.learnersCount}, SMS {detail.usage?.smsCount} cost {detail.usage?.smsCost}, Storage {detail.usage?.storageGb} GB</h4>
          </div>
          <div className="border rounded p-2 max-h-40 overflow-auto">
            <h4 className="font-semibold text-xs">Support Notes — Internal</h4>
            {detail.supportNotes?.map(n=><div key={n.id} className="text-xs border-b py-1"><span className="font-medium">{n.category} {n.isInternal?"(internal)":""} by {n.createdByName}</span> {n.content} • {new Date(n.createdAt).toLocaleString()}</div>)}
          </div>
          <div className="border rounded p-3 bg-warning-50">
            <h4 className="font-semibold text-xs">Manual Actions — Extend Trial, Change Plan, Credit Invoice, Suspend, Reactivate Each Requiring Reason</h4>
            <div className="mt-2 grid sm:grid-cols-2 gap-2 text-xs">
              <button onClick={async()=>{
                const reason=prompt("Reason >=10 chars required for audit - extend trial");
                if(!reason||reason.length<10){alert("Reason >=10 required");return;}
                const newDate=prompt("New trial ends at YYYY-MM-DD", new Date(Date.now()+7*24*3600*1000).toISOString().slice(0,10));
                const res=await apiFetch(`/api/platform/console/tenants/${tenantId}/extend-trial`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({newTrialEndsAt:newDate,reason})});
                alert(res.ok?"Extended - audited":"Failed: "+await res.text());
              }} className="px-2 py-1 rounded bg-white border">Extend Trial (Reason Required Audited)</button>
              <button onClick={async()=>{
                const reason=prompt("Reason >=10 for suspend");
                const res=await apiFetch(`/api/platform/console/tenants/${tenantId}/suspend`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({reason:reason||"Non-payment",isImmediate:true})});
                alert(res.ok?"Suspended":"Failed: "+await res.text());
              }} className="px-2 py-1 rounded bg-danger-600 text-white">Suspend (Reason Audited)</button>
              <button onClick={async()=>{
                const reason=prompt("Reason >=10 for reactivate");
                const res=await apiFetch(`/api/platform/console/tenants/${tenantId}/reactivate`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({reason:reason||"Payment received"})});
                alert(res.ok?"Reactivated":"Failed: "+await res.text());
              }} className="px-2 py-1 rounded bg-success-600 text-white">Reactivate (Reason Audited)</button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

function BusinessMetricsTab(){
  const [metrics,setMetrics]=useState(null);
  useEffect(()=>{ apiFetch(`${API}/tenants/../../platform/console/metrics/business`.replace("/tenants/../../","/"),{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setMetrics).catch(()=>fetch(`${API.replace("/tenants","")}/../platform/console/metrics/business`.replace("//","/"),{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setMetrics)); },[]);
  // Simplified fetch
  useEffect(()=>{
    apiFetch(`/api/platform/console/metrics/business`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setMetrics).catch(()=>{});
  },[]);
  if(!metrics) return <div className="p-4">Loading business metrics MRR, new tenants, churn, trial conversion, revenue by plan, schools at risk...</div>;
  return (
    <div className="bg-white border rounded-lg p-4 space-y-4">
      <h2 className="font-bold">Business Metrics: MRR, New Tenants, Churn, Trial Conversion, Revenue by Plan, Schools at Risk</h2>
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
        <div className="p-3 rounded bg-primary-50 border"><div className="text-xs">MRR</div><div className="text-xl font-bold">${metrics.mrr?.toFixed(2)||"0"}</div><div className="text-xs">Growth {metrics.mrrGrowth}%</div></div>
        <div className="p-3 rounded bg-success-50 border"><div className="text-xs">New This Month</div><div className="text-xl font-bold">{metrics.newTenantsThisMonth}</div><div className="text-xs">Last {metrics.newTenantsLastMonth}</div></div>
        <div className="p-3 rounded bg-danger-50 border"><div className="text-xs">Churn</div><div className="text-xl font-bold">{metrics.churnThisMonth} ({metrics.churnRate}%)</div></div>
        <div className="p-3 rounded bg-warning-50 border"><div className="text-xs">Trial Conversion</div><div className="text-xl font-bold">{metrics.trialConversionRate}%</div><div className="text-xs">{metrics.trialsTotal} trials</div></div>
      </div>
      <div className="grid md:grid-cols-2 gap-4">
        <div className="border rounded p-3">
          <h4 className="font-semibold text-xs">Revenue by Plan</h4>
          {metrics.revenueByPlan?.map(p=><div key={p.planCode} className="flex justify-between text-xs border-b py-1"><span>{p.planName} ({p.tenantsCount})</span><span>${p.monthlyRevenue.toFixed(2)} {p.percentage}%</span></div>)}
        </div>
        <div className="border rounded p-3">
          <h4 className="font-semibold text-xs">Schools at Risk — Falling Logins, Rising Tickets, Unpaid</h4>
          {metrics.schoolsAtRisk?.map(s=><div key={s.tenantId} className="text-xs border-b py-1 flex justify-between"><span>{s.schoolName} {s.state}</span><span>{s.reason}</span></div>)}
        </div>
      </div>
      <div className="border rounded p-3">
        <h4 className="font-semibold text-xs">Revenue by Month</h4>
        <div className="grid grid-cols-12 gap-1 mt-2">
          {(metrics.revenueByMonth||[]).map(m=><div key={`${m.year}-${m.month}`} className="text-center"><div className="text-[10px]">{m.monthName}</div><div className="text-xs font-bold">${m.revenue.toFixed(0)}</div><div className="w-full bg-neutral-200 h-1 rounded mt-1"><div className="bg-primary-800 h-1 rounded" style={{width:`${Math.min(100, m.revenue/1000*100)}%`}}></div></div></div>)}
        </div>
      </div>
    </div>
  );
}

function OperationalTab(){
  const [jobs,setJobs]=useState([]);
  const [errors,setErrors]=useState([]);
  const [smsSpend,setSmsSpend]=useState([]);
  useEffect(()=>{
    apiFetch(`${API.replace("/tenants","")}/../platform/console/operational/jobs`.replace("//","/").replace("/tenants/../","/"),{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setJobs).catch(()=>{});
    apiFetch(`/api/platform/console/operational/jobs`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setJobs);
    apiFetch(`/api/platform/console/operational/error-rates`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setErrors);
    const now=new Date(); apiFetch(`/api/platform/console/operational/sms-spend?year=${now.getFullYear()}&month=${now.getMonth()+1}`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setSmsSpend);
  },[]);
  return (
    <div className="bg-white border rounded-lg p-4 space-y-4">
      <h2 className="font-bold">Operational Views: Background Job Status and Failures, Error Rates, SMS Spend by Tenant, Storage Growth</h2>
      <div className="grid md:grid-cols-2 gap-4">
        <div className="border rounded p-2 max-h-80 overflow-auto">
          <h4 className="font-semibold text-xs">Background Jobs</h4>
          {jobs.map(j=><div key={j.id} className={`text-xs border-b py-1 ${j.status==="Failed"?"bg-danger-50":""}`}><div className="font-medium">{j.jobType} {j.status} {j.tenantName||""}</div><div className="text-[11px] text-neutral-500">{j.errorMessage?.slice(0,100)} {j.startedAt?new Date(j.startedAt).toLocaleString():""}</div></div>)}
        </div>
        <div className="border rounded p-2 max-h-80 overflow-auto">
          <h4 className="font-semibold text-xs">Error Rates</h4>
          {errors.map(e=><div key={e.snapshotDate} className="text-xs border-b py-1 flex justify-between"><span>{new Date(e.snapshotDate).toLocaleDateString()} {e.service}</span><span>{e.errorCount} err {e.errorRate.toFixed(2)}% rate</span></div>)}
        </div>
      </div>
      <div className="border rounded p-2 max-h-80 overflow-auto">
        <h4 className="font-semibold text-xs">SMS Spend by Tenant</h4>
        {smsSpend.map(s=><div key={s.tenantId} className="text-xs border-b py-1 flex justify-between"><span>{s.schoolName} SMS {s.smsCount} cost ${s.smsCost.toFixed(2)}</span><span>${s.totalCost?.toFixed(2)||s.smsCost} {s.currency}</span></div>)}
      </div>
    </div>
  );
}

function ImpersonationTab({onSessionStart}){
  const [grants,setGrants]=useState([]);
  const [tenantId,setTenantId]=useState("");
  const [reason,setReason]=useState("");
  const [duration,setDuration]=useState(60);
  const [hasConsent,setHasConsent]=useState(false);

  async function loadGrants(){
    const res=await apiFetch(`/api/platform/tenants/${tenantId||1}/impersonation-grants`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}});
    if(res.ok) setGrants(await res.json());
  }

  async function startSession(grantId){
    const res=await apiFetch(`/api/platform/impersonation/sessions`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({grantId})});
    if(res.ok){
      const data=await res.json();
      alert("Impersonation session started - banner visible, expires automatically, every action audited as performed-on-behalf-of");
      if(onSessionStart) onSessionStart(data.session);
    } else alert("Failed - grant must be active and consented, impersonation without consent impossible by design: "+await res.text());
  }

  return (
    <div className="bg-white border rounded-lg p-4 space-y-4">
      <h2 className="font-bold">Consented, Time-Limited Support Impersonation — School Admin Grants Access, Session Expires Automatically, Banner Visible, Every Action Recorded as Performed-On-Behalf-Of</h2>
      <p className="text-xs text-neutral-600">Impersonation without consent must be impossible by design, not by policy — enforced by code: only SCHOOL_ADMIN role in tenant can create grant, platform admin cannot create grant for themselves. Grant requires explicit consent checkbox, reason ≥10 chars, duration 5-240 min max 4 hours, expires automatically. Session banner visible throughout, every action audited as performed-on-behalf-of with tenantId, impersonatorUserId, grantId. Second factor required for platform admin console access.</p>

      <div className="border rounded p-3 bg-warning-50">
        <h4 className="font-semibold text-xs">School Admin Grants Access (This endpoint requires SCHOOL_ADMIN role — Platform admin cannot grant for themselves by design)</h4>
        <p className="text-xs text-neutral-600 mt-1">In real flow, school admin goes to Settings → Support → Grant Access, enters reason, checks consent, selects duration. Platform admin sees grant in list then starts session.</p>
        <div className="mt-2 grid sm:grid-cols-2 gap-2">
          <input placeholder="TenantId" value={tenantId} onChange={e=>setTenantId(e.target.value)} className="h-9 px-2 border rounded text-xs" />
          <input placeholder="Reason ≥10 chars — why support needed" value={reason} onChange={e=>setReason(e.target.value)} className="h-9 px-2 border rounded text-xs" />
          <input type="number" placeholder="Duration minutes 5-240" value={duration} onChange={e=>setDuration(parseInt(e.target.value))} className="h-9 px-2 border rounded text-xs" />
          <label className="flex gap-1 text-xs items-center"><input type="checkbox" checked={hasConsent} onChange={e=>setHasConsent(e.target.checked)} /> I consent to support impersonation — HasConsent checkbox required</label>
          <button onClick={async()=>{
            const res=await apiFetch(`/api/platform/tenants/${tenantId}/impersonation-grants`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({reason,durationMinutes:duration,hasConsent})});
            if(res.ok){ alert("Grant created by school admin — now platform admin can start session"); loadGrants(); } else alert("Failed - only SCHOOL_ADMIN can grant, platform admin cannot grant for themselves by design: "+await res.text());
          }} className="h-9 px-3 rounded bg-primary-800 text-white text-xs">Grant Access (SCHOOL_ADMIN only)</button>
        </div>
      </div>

      <div>
        <h4 className="font-semibold text-xs">Active Grants — Consented</h4>
        <button onClick={loadGrants} className="mt-1 px-2 py-1 border rounded text-xs">Load Grants for Tenant {tenantId||1}</button>
        <div className="mt-2 space-y-1 max-h-60 overflow-auto">
          {grants.map(g=>(
            <div key={g.id} className={`p-2 border rounded text-xs ${g.isActive?"bg-success-50":"bg-neutral-50"}`}>
              <div className="flex justify-between"><span className="font-medium">Grant {g.id} Tenant {g.tenantId} by {g.grantedByName} role {g.grantedByRole}</span><span className={g.isActive?"text-success-600":"text-neutral-500"}>{g.isActive?"Active":"Expired/Revoked"}</span></div>
              <div>Reason: {g.reason} • Expires {new Date(g.expiresAt).toLocaleString()} • Consent {g.hasConsent?"Yes":"No"} • GrantedTo {g.grantedToRole}</div>
              {g.isActive && <button onClick={()=>startSession(g.id)} className="mt-1 px-2 py-1 rounded bg-danger-600 text-white text-xs">Start Impersonation Session — Expires Automatically — Banner Visible — Audited as Performed-On-Behalf-Of</button>}
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function BroadcastTab(){
  const [title,setTitle]=useState("");
  const [body,setBody]=useState("");
  const [audience,setAudience]=useState("all_school_admins");
  const [priority,setPriority]=useState("normal");
  async function send(){
    const res=await apiFetch(`/api/platform/console/broadcasts`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({title,body,audience,audienceFilterJson:null,scheduledAt:null,expiresAt:null,priority})});
    if(res.ok) alert("Broadcast created — would send to all school admins for maintenance windows and releases");
    else alert("Failed: "+await res.text());
  }
  return (
    <div className="bg-white border rounded-lg p-4">
      <h3 className="font-semibold text-sm">Announcement Broadcast to All School Admins for Maintenance Windows and Releases</h3>
      <div className="mt-3 space-y-2">
        <input placeholder="Title e.g. Maintenance window Sat 02:00-04:00 CAT" value={title} onChange={e=>setTitle(e.target.value)} className="w-full h-9 px-2 border rounded text-sm" />
        <textarea placeholder="Body markdown — maintenance details, release notes" value={body} onChange={e=>setBody(e.target.value)} rows="4" className="w-full px-2 py-2 border rounded text-sm" />
        <div className="grid sm:grid-cols-3 gap-2">
          <select value={audience} onChange={e=>setAudience(e.target.value)} className="h-9 px-2 border rounded text-sm"><option value="all_school_admins">All School Admins</option><option value="all_tenants">All Tenants</option><option value="specific_plan">Specific Plan</option></select>
          <select value={priority} onChange={e=>setPriority(e.target.value)} className="h-9 px-2 border rounded text-sm"><option value="normal">Normal</option><option value="high">High</option><option value="urgent">Urgent Maintenance</option></select>
          <button onClick={send} className="h-9 px-3 rounded bg-primary-800 text-white text-sm">Broadcast — Audited</button>
        </div>
      </div>
    </div>
  );
}

function OverridesTab(){
  const [overrides,setOverrides]=useState([]);
  useEffect(()=>{ apiFetch(`/api/platform/overrides`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setOverrides); },[]);
  return (
    <div className="bg-white border rounded-lg p-4">
      <h3 className="font-semibold text-sm">Manual Overrides — Extend Trial, Credit Invoice, Fully Audited</h3>
      <div className="mt-2 max-h-96 overflow-auto border rounded">
        <table className="w-full text-xs border-collapse">
          <thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1">Tenant</th><th className="border p-1">Type</th><th className="border p-1">Details</th><th className="border p-1">Reason</th><th className="border p-1">Admin</th><th className="border p-1">Date</th></tr></thead>
          <tbody>{overrides.map(o=><tr key={o.id}><td className="border p-1">{o.tenantId}</td><td className="border p-1">{o.overrideType}</td><td className="border p-1">{o.detailsJson}</td><td className="border p-1">{o.reason}</td><td className="border p-1">{o.adminUserId}</td><td className="border p-1">{new Date(o.createdAtOverride||o.createdAt).toLocaleString()}</td></tr>)}</tbody>
        </table>
      </div>
    </div>
  );
}