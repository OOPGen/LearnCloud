/**
 * Communication Full Module - Frontend
 * Announcements with expiry shown across portals, scheduled sending, saved audience segments, template library categories, two-way SMS, event-triggered rule engine, delivery analytics by campaign, per-learner communication log searchable
 * Preserves provider abstraction, usage caps, opt-out
 */

import React, { useState, useEffect } from 'react';
import { apiFetch, getAccessToken, setAccessToken } from '../../LearnCloud.Web/src/lib/apiClient.js'; // SECURITY C2 FIX

const API = "/api/communication";
const MSG_API = "/api/messaging";

export default function CommunicationFull() {
  const [tab,setTab]=useState("announcements");

  return (
    <div className="min-h-screen bg-neutral-50">
      <header className="bg-white border-b p-4 sticky top-0 z-10">
        <h1 className="text-xl font-bold">Communication — Full Module</h1>
        <p className="text-xs text-neutral-500">Announcements expiry across portals, scheduled sending, saved segments, template categories, two-way SMS, rule engine absence N days / arrears threshold / report published / invoice due 7 days with per-tenant config opt-out, analytics by campaign, log searchable by learner. Preserves provider abstraction, caps, opt-out.</p>
        <div className="mt-3 flex gap-2 overflow-auto">
          {[
            {id:"announcements",label:"Announcements"},
            {id:"scheduled",label:"Scheduled"},
            {id:"segments",label:"Saved Segments"},
            {id:"templates",label:"Template Library Categories"},
            {id:"twoway",label:"Two-Way SMS"},
            {id:"rules",label:"Rule Engine"},
            {id:"analytics",label:"Analytics by Campaign"},
            {id:"logs",label:"Per-Learner Log"},
          ].map(t=><button key={t.id} onClick={()=>setTab(t.id)} className={`px-3 py-1.5 rounded-full border text-sm whitespace-nowrap ${tab===t.id?"bg-primary-800 text-white":"bg-white"}`}>{t.label}</button>)}
        </div>
      </header>

      <main className="p-4 max-w-6xl mx-auto">
        {tab==="announcements" && <AnnouncementsTab />}
        {tab==="scheduled" && <ScheduledTab />}
        {tab==="segments" && <SegmentsTab />}
        {tab==="templates" && <TemplatesTab />}
        {tab==="twoway" && <TwoWayTab />}
        {tab==="rules" && <RulesTab />}
        {tab==="analytics" && <AnalyticsTab />}
        {tab==="logs" && <LogsTab />}
      </main>
    </div>
  );
}

function AnnouncementsTab() {
  const [list,setList]=useState([]);
  const [form,setForm]=useState({title:"",body:"",audienceType:"all",expiryDate:"",priority:"normal",showTeacher:true,showParent:true,showStudent:true});
  useEffect(()=>{ apiFetch(`${API}/announcements`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setList); },[]);

  async function create(){
    const res=await apiFetch(`${API}/announcements`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({
      title:form.title,body:form.body,audienceType:form.audienceType,audienceFilterJson:"{}",expiryDate:form.expiryDate?new Date(form.expiryDate).toISOString():null,priority:form.priority,showInTeacherPortal:form.showTeacher,showInParentPortal:form.showParent,showInStudentPortal:form.showStudent,showInAdminDashboard:true
    })});
    if(res.ok){ setForm({title:"",body:"",audienceType:"all",expiryDate:"",priority:"normal",showTeacher:true,showParent:true,showStudent:true}); apiFetch(`${API}/announcements`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setList); }
  }

  return (
    <div className="space-y-4">
      <div className="bg-white border rounded-lg p-4">
        <h3 className="font-semibold text-sm">Create Announcement — Audience and Expiry Date Shown Across Portals</h3>
        <div className="mt-3 grid sm:grid-cols-2 gap-2">
          <input placeholder="Title" value={form.title} onChange={e=>setForm({...form,title:e.target.value})} className="h-9 px-2 border rounded text-sm" />
          <select value={form.audienceType} onChange={e=>setForm({...form,audienceType:e.target.value})} className="h-9 px-2 border rounded text-sm"><option value="all">All</option><option value="class">Class</option><option value="role">Role: teacher/parent/student</option></select>
          <input type="date" value={form.expiryDate} onChange={e=>setForm({...form,expiryDate:e.target.value})} className="h-9 px-2 border rounded text-sm" />
          <select value={form.priority} onChange={e=>setForm({...form,priority:e.target.value})} className="h-9 px-2 border rounded text-sm"><option>normal</option><option>high</option><option>urgent</option></select>
          <textarea placeholder="Body" value={form.body} onChange={e=>setForm({...form,body:e.target.value})} rows="3" className="col-span-2 w-full px-2 py-2 border rounded text-sm" />
          <label className="flex gap-1 text-xs"><input type="checkbox" checked={form.showTeacher} onChange={e=>setForm({...form,showTeacher:e.target.checked})} /> Teacher portal</label>
          <label className="flex gap-1 text-xs"><input type="checkbox" checked={form.showParent} onChange={e=>setForm({...form,showParent:e.target.checked})} /> Parent portal</label>
          <label className="flex gap-1 text-xs"><input type="checkbox" checked={form.showStudent} onChange={e=>setForm({...form,showStudent:e.target.checked})} /> Student portal</label>
        </div>
        <button onClick={create} className="mt-3 w-full py-2 rounded bg-primary-800 text-white text-sm">Create Announcement — Shown Across Portals Until Expiry</button>
      </div>
      <div className="space-y-2">
        {list.map(a=>(
          <div key={a.id} className="bg-white border rounded-lg p-3">
            <div className="flex justify-between"><span className="font-medium text-sm">{a.title} <span className="text-xs px-1 rounded bg-neutral-100">{a.priority}</span></span><span className="text-xs text-neutral-500">{a.expiryDate?`Expires ${new Date(a.expiryDate).toLocaleDateString()}`:"No expiry"} • {a.status}</span></div>
            <div className="text-sm mt-1">{a.body}</div>
            <div className="text-[11px] text-neutral-500 mt-1">Audience {a.audienceType} • Teacher:{a.showTeacher?"Y":"N"} Parent:{a.showParent?"Y":"N"} Student:{a.showStudent?"Y":"N"}</div>
          </div>
        ))}
      </div>
    </div>
  );
}

function ScheduledTab() {
  const [list,setList]=useState([]);
  useEffect(()=>{ apiFetch(`${API}/scheduled`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setList); },[]);
  return (
    <div className="bg-white border rounded-lg p-4">
      <h3 className="font-semibold text-sm">Scheduled Sending</h3>
      <p className="text-xs text-neutral-500">Create message with scheduledSendAt future date, status scheduled, background job picks at scheduled time and queues batch. Same audience, template, cost estimate flow.</p>
      <div className="mt-3 space-y-1">
        {list.map(s=><div key={s.id} className="p-2 border rounded text-sm flex justify-between"><span>{s.title} — {new Date(s.scheduledSendAt).toLocaleString()} — {s.status}</span><span className="text-xs">{s.channel}</span></div>)}
      </div>
    </div>
  );
}

function SegmentsTab() {
  const [list,setList]=useState([]);
  const [form,setForm]=useState({name:"",description:"",audienceType:"class",filterJson:"{}",isDynamic:false});
  useEffect(()=>{ apiFetch(`${API}/segments`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setList); },[]);
  async function create(){
    const res=await apiFetch(`${API}/segments`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify(form)});
    if(res.ok){ setForm({name:"",description:"",audienceType:"class",filterJson:"{}",isDynamic:false}); apiFetch(`${API}/segments`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setList); }
  }
  async function preview(id){
    const res=await apiFetch(`${API}/segments/preview`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({segmentId:id})});
    if(res.ok){ const data=await res.json(); alert(`Preview: ${data.totalGuardians} guardians, ${data.totalStudents} students, sample: ${JSON.stringify(data.sampleRecipients.slice(0,2))}`); }
  }
  return (
    <div className="bg-white border rounded-lg p-4">
      <h3 className="font-semibold text-sm">Saved Audience Segments — Reusable, Dynamic Re-evaluated at Send Time</h3>
      <div className="mt-3 grid sm:grid-cols-2 gap-2">
        <input placeholder="Name e.g. Grade 5 Blue Parents" value={form.name} onChange={e=>setForm({...form,name:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="Description" value={form.description} onChange={e=>setForm({...form,description:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <select value={form.audienceType} onChange={e=>setForm({...form,audienceType:e.target.value})} className="h-9 px-2 border rounded text-sm"><option value="class">Class</option><option value="arrears_over_x">Arrears over X dynamic</option><option value="absent_today">Absent today dynamic</option><option value="all_guardians">All guardians</option></select>
        <input placeholder='Filter JSON e.g. {"gradeId":5,"streamId":10,"arrearsThreshold":100}' value={form.filterJson} onChange={e=>setForm({...form,filterJson:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <label className="flex gap-1 text-xs"><input type="checkbox" checked={form.isDynamic} onChange={e=>setForm({...form,isDynamic:e.target.checked})} /> Is Dynamic (re-evaluated at send time)</label>
        <button onClick={create} className="h-9 px-3 rounded bg-primary-800 text-white text-sm">Save Segment</button>
      </div>
      <div className="mt-4 space-y-1">
        {list.map(s=><div key={s.id} className="p-2 border rounded text-sm flex justify-between"><div><div className="font-medium">{s.name} {s.isDynamic&&<span className="text-xs bg-warning-100 px-1 rounded">Dynamic</span>}</div><div className="text-xs text-neutral-500">{s.audienceType} • {s.filterJson}</div></div><button onClick={()=>preview(s.id)} className="text-xs px-2 py-1 border rounded">Preview</button></div>)}
      </div>
    </div>
  );
}

function TemplatesTab() {
  const [categories,setCategories]=useState([]);
  const [templates,setTemplates]=useState([]);
  const [selectedCat,setSelectedCat]=useState("");
  useEffect(()=>{
    apiFetch(`${API}/template-categories`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setCategories);
    apiFetch(`${API}/templates/with-category`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setTemplates);
  },[]);
  return (
    <div className="bg-white border rounded-lg p-4">
      <h3 className="font-semibold text-sm">Template Library with Categories — Fees, Attendance, Academic, General, Discipline</h3>
      <div className="mt-2 flex gap-2 overflow-auto">
        <button onClick={()=>{ setSelectedCat(""); apiFetch(`${API}/templates/with-category`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setTemplates); }} className={`px-3 py-1 rounded-full border text-xs ${!selectedCat?"bg-primary-800 text-white":"bg-white"}`}>All</button>
        {categories.map(c=><button key={c.id} onClick={()=>{ setSelectedCat(c.code); apiFetch(`${API}/templates/with-category?categoryCode=${c.code}`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setTemplates); }} className={`px-3 py-1 rounded-full border text-xs ${selectedCat===c.code?"bg-primary-800 text-white":"bg-white"}`}>{c.name}</button>)}
      </div>
      <div className="mt-3 grid sm:grid-cols-2 gap-2">
        {templates.map(t=>(
          <div key={t.id} className="p-3 border rounded">
            <div className="font-medium text-sm">{t.name} [{t.channel}] {t.categoryName&&<span className="text-xs px-1 rounded" style={{background:t.categoryName==="Fees"?"#B7791F22":t.categoryName==="Attendance"?"#C6282822":"#0F153A11"}}>{t.categoryName}</span>}</div>
            <div className="text-xs text-neutral-500 mt-1">{t.body.slice(0,100)}</div>
            <div className="text-[11px] mt-1">Merge: {t.mergeFields.join(", ")}</div>
          </div>
        ))}
      </div>
    </div>
  );
}

function TwoWayTab() {
  const [inbound,setInbound]=useState([]);
  useEffect(()=>{ apiFetch(`${API}/inbound-sms`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setInbound); },[]);
  return (
    <div className="bg-white border rounded-lg p-4">
      <h3 className="font-semibold text-sm">Two-Way SMS Handling If Provider Supports It — Inbound Webhook, Matched Guardian, Reply</h3>
      <p className="text-xs text-neutral-500">Provider webhook POST /api/communication/inbound-sms/webhook {From, To, Text, ProviderReference} → matched via phone to guardian, creates inbound_sms status matched/unmatched, creates communication_log inbound direction, searchable per learner. Reply via UI if provider supports two-way.</p>
      <div className="mt-3 space-y-1 max-h-96 overflow-auto">
        {inbound.map(m=>(
          <div key={m.id} className="p-2 border rounded text-sm">
            <div className="flex justify-between"><span className="font-medium">{m.fromNumber} → {m.toNumber}</span><span className="text-xs">{new Date(m.receivedAt).toLocaleString()} • {m.status}</span></div>
            <div className="mt-1">{m.body}</div>
            {m.matchedGuardianId && <div className="text-xs text-success-600">Matched guardian {m.matchedGuardianId}</div>}
            {m.replyBody && <div className="text-xs mt-1 p-1 bg-primary-50 border rounded">Reply: {m.replyBody}</div>}
          </div>
        ))}
      </div>
    </div>
  );
}

function RulesTab() {
  const [rules,setRules]=useState([]);
  const [form,setForm]=useState({name:"",code:"",eventType:"absence_n_days",description:"",configJson:'{"n":3}',templateId:"",channel:"sms",audienceType:"dynamic"});
  useEffect(()=>{ apiFetch(`${API}/rules`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setRules); },[]);

  async function create(){
    const res=await apiFetch(`${API}/rules`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({...form, templateId:form.templateId?parseInt(form.templateId):null, isActive:true})});
    if(res.ok){ setForm({name:"",code:"",eventType:"absence_n_days",description:"",configJson:'{"n":3}',templateId:"",channel:"sms",audienceType:"dynamic"}); apiFetch(`${API}/rules`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setRules); }
  }

  return (
    <div className="bg-white border rounded-lg p-4">
      <h3 className="font-semibold text-sm">Event-Triggered Rule Engine — Absence N Days, Arrears Over Threshold, Report Card Published, Invoice Due in 7 Days — Per-Tenant Config + Opt-Out</h3>
      <div className="mt-3 grid sm:grid-cols-2 gap-2">
        <input placeholder="Name e.g. Absence 3 consecutive days" value={form.name} onChange={e=>setForm({...form,name:e.target.value})} className="h-9 px-2 border rounded text-xs" />
        <input placeholder="Code e.g. absence_3_days" value={form.code} onChange={e=>setForm({...form,code:e.target.value})} className="h-9 px-2 border rounded text-xs" />
        <select value={form.eventType} onChange={e=>setForm({...form,eventType:e.target.value})} className="h-9 px-2 border rounded text-xs"><option value="absence_n_days">Absence N Days</option><option value="arrears_over_threshold">Arrears Over Threshold</option><option value="report_card_published">Report Card Published</option><option value="invoice_due_7_days">Invoice Due in 7 Days</option></select>
        <input placeholder='Config JSON e.g. {"n":3} or {"threshold":100}' value={form.configJson} onChange={e=>setForm({...form,configJson:e.target.value})} className="h-9 px-2 border rounded text-xs" />
        <select value={form.channel} onChange={e=>setForm({...form,channel:e.target.value})} className="h-9 px-2 border rounded text-xs"><option value="sms">SMS</option><option value="email">Email</option></select>
        <button onClick={create} className="h-9 px-3 rounded bg-primary-800 text-white text-xs">Create Rule — Opt-Out Honoured</button>
      </div>
      <div className="mt-4 space-y-1">
        {rules.map(r=>(
          <div key={r.id} className="p-2 border rounded text-sm">
            <div className="flex justify-between"><span className="font-medium">{r.name} ({r.code}) — {r.eventType}</span><span className={`text-xs px-1 rounded ${r.isActive?"bg-success-100":"bg-neutral-100"}`}>{r.isActive?"Active":"Inactive"}</span></div>
            <div className="text-xs text-neutral-500">Config {r.configJson} • Channel {r.channel} • Triggered {r.triggerCount} times • Last {r.lastTriggeredAt?new Date(r.lastTriggeredAt).toLocaleString():"never"} • RespectOptOut {r.respectOptOut?"Yes":"No"}</div>
            <div className="text-xs">{r.description}</div>
          </div>
        ))}
      </div>
      <p className="text-xs text-neutral-500 mt-2">Rule engine runs hourly background job, checks consecutive absence, arrears threshold, report published event, invoice due 7 days, creates MessageBatch auto, respects opt-out and contact preferences, per-tenant config.</p>
    </div>
  );
}

function AnalyticsTab() {
  const [data,setData]=useState([]);
  useEffect(()=>{ apiFetch(`${API}/analytics/campaigns`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setData); },[]);
  return (
    <div className="bg-white border rounded-lg p-4">
      <h3 className="font-semibold text-sm">Delivery Analytics by Campaign — Sent, Delivered, Failed, Read, Cost, Delivery Rate, Read Rate</h3>
      <div className="mt-3 overflow-auto max-h-96 border rounded">
        <table className="w-full text-xs border-collapse">
          <thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1">Batch</th><th className="border p-1">Title</th><th className="border p-1">Channel</th><th className="border p-1">Total</th><th className="border p-1">Sent</th><th className="border p-1">Delivered</th><th className="border p-1">Failed</th><th className="border p-1">Cost</th><th className="border p-1">Delivery%</th></tr></thead>
          <tbody>{data.map(d=><tr key={d.batchId}><td className="border p-1">{d.batchNumber}</td><td className="border p-1">{d.title}</td><td className="border p-1">{d.channel}</td><td className="border p-1">{d.totalRecipients}</td><td className="border p-1">{d.sent}</td><td className="border p-1">{d.delivered}</td><td className="border p-1">{d.failed}</td><td className="border p-1">{d.totalCost.toFixed(2)} {d.currency}</td><td className="border p-1">{d.deliveryRate.toFixed(1)}%</td></tr>)}</tbody>
        </table>
      </div>
    </div>
  );
}

function LogsTab() {
  const [logs,setLogs]=useState([]);
  const [filter,setFilter]=useState({studentId:"",searchText:""});
  async function search(){
    const params=new URLSearchParams();
    if(filter.studentId) params.append("studentId",filter.studentId);
    if(filter.searchText) params.append("searchText",filter.searchText);
    params.append("page","1"); params.append("pageSize","50");
    const res=await apiFetch(`${API}/logs?${params}`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}});
    if(res.ok){ const data=await res.json(); setLogs(data.items||[]); }
  }
  useEffect(()=>{ search(); },[]);
  return (
    <div className="bg-white border rounded-lg p-4">
      <h3 className="font-semibold text-sm">Per-Tenant Communication Log Searchable by Learner — Preserves Provider Abstraction, Caps, Opt-Out</h3>
      <div className="mt-2 flex gap-2">
        <input placeholder="StudentId" value={filter.studentId} onChange={e=>setFilter({...filter,studentId:e.target.value})} className="h-9 px-2 border rounded text-xs w-24" />
        <input placeholder="Search text (learner name, body)" value={filter.searchText} onChange={e=>setFilter({...filter,searchText:e.target.value})} className="h-9 px-2 border rounded text-xs flex-1" />
        <button onClick={search} className="h-9 px-3 rounded bg-primary-800 text-white text-xs">Search</button>
      </div>
      <div className="mt-3 max-h-96 overflow-auto border rounded space-y-1">
        {logs.map(l=>(
          <div key={l.id} className="p-2 border-b text-xs">
            <div className="flex justify-between"><span className="font-medium">{l.studentName} — {l.guardianName} — {l.channel} {l.direction}</span><span>{new Date(l.sentAt).toLocaleString()} • {l.status}</span></div>
            <div className="mt-1">{l.messageBody.slice(0,120)}</div>
            <div className="text-[11px] text-neutral-500">To {l.recipientAddress} • Provider {l.provider} • Cost {l.cost} {l.currency} • Batch {l.batchId||l.announcementId||l.ruleId}</div>
          </div>
        ))}
      </div>
    </div>
  );
}