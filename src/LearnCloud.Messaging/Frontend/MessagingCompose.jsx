/**
 * Messaging Compose Flow - React
 * Audience selection, template choice, preview, recipient count, cost estimate and confirmation
 * Includes: class, stream, year group, all guardians, arrears over X, absent today, manual
 * Guardian contact preferences and opt-out honoured, hard cap warning, cost estimate before confirm
 */

import React, { useState, useEffect } from 'react';
import { apiFetch, getAccessToken, setAccessToken } from '../../LearnCloud.Web/src/lib/apiClient.js'; // SECURITY C2 FIX

const API = "/api/messaging";

export default function MessagingCompose() {
  const [templates, setTemplates] = useState([]);
  const [selectedTemplate, setSelectedTemplate] = useState(null);
  const [channel, setChannel] = useState("sms");
  const [audienceType, setAudienceType] = useState("class");
  const [audience, setAudience] = useState({ type: "class", gradeId: "", streamId: "", academicYearId: "2026", arrearsThreshold: "100", date: new Date().toISOString().slice(0,10) });
  const [body, setBody] = useState("Dear {{guardian_name}}, learner {{learner_name}} ({{class}}) has outstanding balance {{currency}} {{amount_owed}} due {{due_date}}. {{school_name}}");
  const [subject, setSubject] = useState("Notice from {{school_name}}");
  const [preview, setPreview] = useState(null);
  const [cost, setCost] = useState(null);
  const [batch, setBatch] = useState(null);
  const [usage, setUsage] = useState(null);

  useEffect(()=>{
    apiFetch(`${API}/templates`, {headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setTemplates);
    apiFetch(`${API}/usage`, {headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setUsage);
  },[]);

  async function doPreview(){
    const req = { templateId: selectedTemplate?.id||null, channel, audience: {...audience, type: audienceType}, body, subject };
    const res = await apiFetch(`${API}/preview`, {method:"POST", headers:{"Content-Type":"application/json", Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}, body: JSON.stringify(req)});
    if(res.ok){
      const data = await res.json();
      setPreview(data);
      setCost(data.costEstimate);
    }
  }

  async function createBatch(){
    const req = { title: `Message ${audienceType} ${new Date().toLocaleString()}`, templateId: selectedTemplate?.id||null, channel, audience: {...audience, type: audienceType, gradeId: audience.gradeId?parseInt(audience.gradeId):null, streamId: audience.streamId?parseInt(audience.streamId):null, academicYearId: audience.academicYearId?parseInt(audience.academicYearId):2026, arrearsThreshold: audience.arrearsThreshold?parseFloat(audience.arrearsThreshold):null}, customBody: body, customSubject: subject };
    const res = await apiFetch(`${API}/batches`, {method:"POST", headers:{"Content-Type":"application/json", Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}, body: JSON.stringify(req)});
    if(res.ok){
      const data = await res.json();
      setBatch(data);
      alert(`Batch ${data.batch.batchNumber} created with ${data.batch.totalRecipients} recipients. Cost ${data.batch.estimatedCost} ${data.batch.currency}. Filtered opt-out ${data.filteredOutOptOut}. ${data.capWarning? "WARNING: "+data.capWarning : ""}`);
    } else {
      const err = await res.json();
      alert("Failed: "+ (err.message || err.capWarning || "cap exceeded") + " Cost: "+JSON.stringify(err.costEstimate));
    }
  }

  async function confirmBatch(){
    if(!batch) return;
    const res = await apiFetch(`${API}/batches/${batch.batch.id}/confirm`, {method:"POST", headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}});
    if(res.ok){
      alert("Batch queued for sending - background job with batching, retry backoff, per-tenant rate limits");
      // Poll status
      const interval = setInterval(async ()=>{
        const r = await apiFetch(`${API}/batches/${batch.batch.id}`, {headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}});
        if(r.ok){
          const b = await r.json();
          setBatch({...batch, batch: b});
          if(b.status==="Sent" || b.status==="Failed") clearInterval(interval);
        }
      }, 3000);
    } else {
      alert("Confirm failed: "+await res.text());
    }
  }

  function selectTemplate(t){
    setSelectedTemplate(t);
    setBody(t.body);
    setSubject(t.subject);
    setChannel(t.channel.toLowerCase());
  }

  return (
    <div className="min-h-screen bg-neutral-50 p-4 max-w-5xl mx-auto">
      <h1 className="text-2xl font-bold">Messaging — SMS & Email to Guardians</h1>
      <p className="text-xs text-neutral-600 mt-1">Provider abstraction ISmsProvider/IEmailProvider swappable via settings. Audience: class, stream, year group, all guardians, arrears over X, absent today. Templates merge fields previewed against real recipient. Background job batching retry backoff rate limits. Delivery log per message cost timestamp + usage counter billing bundles. Hard cap warning. Opt-out honoured. Cost estimate before confirm.</p>

      {/* Usage + cap warning */}
      {usage && (
        <div className={`mt-4 p-3 rounded-lg border ${usage.nearCap?"bg-warning-50 border-warning-200":"bg-white"}`}>
          <div className="text-sm font-medium">Usage {usage.year}-{usage.month} — SMS {usage.smsCount}/{usage.smsLimit} remaining {usage.smsRemaining} — Email {usage.emailCount}/{usage.emailLimit} remaining {usage.emailRemaining}</div>
          {usage.warningMessage && <div className="text-xs text-warning-700 mt-1">{usage.warningMessage}</div>}
          <div className="text-xs text-neutral-500 mt-1">Hard per-tenant cap protects school and you from runaway costs</div>
        </div>
      )}

      <div className="mt-6 grid lg:grid-cols-[300px_1fr] gap-4">
        {/* Left: templates + audience */}
        <div className="space-y-4">
          <div className="bg-white border rounded-lg p-3">
            <h3 className="font-semibold text-sm">1. Template Choice — 3 seeded</h3>
            <div className="mt-2 space-y-1">
              {templates.map(t=>(
                <button key={t.id} onClick={()=>selectTemplate(t)} className={`w-full text-left p-2 rounded border text-xs ${selectedTemplate?.id===t.id?"bg-primary-800 text-white":"bg-neutral-50 hover:bg-white"}`}>
                  <div className="font-medium">{t.name} [{t.channel}] {t.isSystem?"★":""}</div>
                  <div className="text-[11px] opacity-80 truncate">{t.body.slice(0,80)}</div>
                  <div className="text-[10px] mt-1">Merge: {t.mergeFields.join(", ")}</div>
                </button>
              ))}
            </div>
            <button onClick={async()=>{
              const res=await apiFetch(`${API}/templates/seed`,{method:"POST", headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}});
              if(res.ok){ const j=await res.json(); alert(j.message); apiFetch(`${API}/templates`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setTemplates); }
            }} className="mt-2 w-full py-2 rounded border text-xs">Seed 3 Templates: fee reminder, absence, general notice</button>
          </div>

          <div className="bg-white border rounded-lg p-3">
            <h3 className="font-semibold text-sm">2. Audience Selection</h3>
            <select value={audienceType} onChange={e=>setAudienceType(e.target.value)} className="mt-2 w-full h-9 px-2 border rounded text-sm">
              <option value="class">Class — Grade + Stream</option>
              <option value="stream">Stream</option>
              <option value="year_group">Year Group — Grade</option>
              <option value="all_guardians">All Guardians</option>
              <option value="arrears_over_x">Guardians of learners with arrears over X</option>
              <option value="absent_today">Guardians of learners absent today</option>
              <option value="manual">Manual list</option>
            </select>

            <div className="mt-3 space-y-2">
              {(audienceType==="class"||audienceType==="stream") && (
                <>
                  <input placeholder="GradeId e.g. 5" value={audience.gradeId} onChange={e=>setAudience({...audience,gradeId:e.target.value})} className="w-full h-9 px-2 border rounded text-sm" />
                  <input placeholder="StreamId e.g. 10" value={audience.streamId} onChange={e=>setAudience({...audience,streamId:e.target.value})} className="w-full h-9 px-2 border rounded text-sm" />
                </>
              )}
              {audienceType==="year_group" && (
                <input placeholder="GradeId for year group" value={audience.gradeId} onChange={e=>setAudience({...audience,gradeId:e.target.value})} className="w-full h-9 px-2 border rounded text-sm" />
              )}
              {audienceType==="arrears_over_x" && (
                <input type="number" step="0.01" placeholder="Arrears threshold e.g. 100" value={audience.arrearsThreshold} onChange={e=>setAudience({...audience,arrearsThreshold:e.target.value})} className="w-full h-9 px-2 border rounded text-sm" />
              )}
              {audienceType==="absent_today" && (
                <input type="date" value={audience.date} onChange={e=>setAudience({...audience,date:e.target.value})} className="w-full h-9 px-2 border rounded text-sm" />
              )}
            </div>

            <div className="mt-2 text-[11px] text-neutral-500">
              Audience resolved server-side with tenant filter + guardian links. Dynamic lists: arrears over X joins fee_invoices balance_due, absent today joins attendance_records.
            </div>
          </div>

          <div className="bg-white border rounded-lg p-3">
            <h3 className="font-semibold text-sm">3. Channel</h3>
            <div className="mt-2 flex gap-2">
              <button onClick={()=>setChannel("sms")} className={`flex-1 py-2 rounded border text-sm ${channel==="sms"?"bg-primary-800 text-white":"bg-white"}`}>SMS (0.05 USD)</button>
              <button onClick={()=>setChannel("email")} className={`flex-1 py-2 rounded border text-sm ${channel==="email"?"bg-primary-800 text-white":"bg-white"}`}>Email (0.01 USD)</button>
            </div>
            <p className="text-[11px] text-neutral-500 mt-2">Provider via MessagingProviderSettings — EcoCashSms/BulkSmsZw swappable for SMS, Smtp/SendGrid for Email without touching calling code</p>
          </div>
        </div>

        {/* Right: compose, preview, cost, confirm */}
        <div className="space-y-4">
          <div className="bg-white border rounded-lg p-3">
            <h3 className="font-semibold text-sm">4. Compose with Merge Fields</h3>
            <p className="text-[11px] text-neutral-500">Fields: learner_name, class, amount_owed, date, school_name, guardian_name, etc. Previewed against real recipient.</p>
            <label className="block mt-3"><span className="text-xs font-medium">Subject (email)</span><input value={subject} onChange={e=>setSubject(e.target.value)} className="mt-1 w-full h-9 px-2 border rounded text-sm" placeholder="Fee Reminder - {{learner_name}}" /></label>
            <label className="block mt-3"><span className="text-xs font-medium">Body</span><textarea value={body} onChange={e=>setBody(e.target.value)} rows="5" className="mt-1 w-full px-2 py-2 border rounded text-sm" placeholder="Dear {{guardian_name}}, learner {{learner_name}}..." /></label>
            <div className="mt-2 text-[11px] text-neutral-500">Available merge fields: learner_name, learner_first_name, class, grade, stream, school_name, amount_owed, currency, date, due_date, guardian_name, teacher_name, next_term_date</div>
          </div>

          <div className="bg-white border rounded-lg p-3">
            <h3 className="font-semibold text-sm">5. Preview Against Real Recipient + Cost Estimate Before Confirm</h3>
            <button onClick={async()=>{
              const req = { templateId: selectedTemplate?.id||null, channel, audience: {...audience, type: audienceType, gradeId: audience.gradeId?parseInt(audience.gradeId):null, streamId: audience.streamId?parseInt(audience.streamId):null, academicYearId:2026, arrearsThreshold: audience.arrearsThreshold?parseFloat(audience.arrearsThreshold):null}, body, subject };
              const res=await apiFetch(`${API}/preview`,{method:"POST", headers:{"Content-Type":"application/json", Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}, body: JSON.stringify(req)});
              if(res.ok){ const data=await res.json(); setPreview(data); setCost(data.costEstimate); }
            }} className="mt-2 w-full py-2 rounded-lg bg-primary-50 border border-primary-200 text-primary-800 text-sm font-medium">Preview + Cost Estimate</button>

            {preview && (
              <div className="mt-3 p-3 rounded-lg bg-neutral-50 border text-sm space-y-2">
                <div><strong>Total Recipients:</strong> {preview.totalRecipients} — Filtered OptOut {preview.filteredOutOptOut} NoContact {preview.filteredOutNoContact} → Final {preview.totalRecipients - preview.filteredOutOptOut - preview.filteredOutNoContact}</div>
                <div><strong>Sample:</strong> {preview.sampleRecipientName} ({preview.sampleRecipientAddress})</div>
                <div><strong>Rendered Subject:</strong> {preview.renderedSubject}</div>
                <div className="p-2 bg-white border rounded"><strong>Rendered Body:</strong><br/>{preview.renderedBody}</div>
                <div><strong>Merge fields used:</strong> {preview.mergeFieldsUsed.join(", ")}</div>
              </div>
            )}

            {cost && (
              <div className={`mt-3 p-3 rounded-lg border ${cost.capExceeded?"bg-danger-50 border-danger-200":cost.capWarning?"bg-warning-50 border-warning-200":"bg-success-50 border-success-200"}`}>
                <div className="font-medium text-sm">Cost Estimate: {cost.totalCost.toFixed(2)} {cost.currency} — {cost.smsRecipients} SMS @ {cost.costPerSms} + {cost.emailRecipients} Email @ {cost.costPerEmail}</div>
                <div className="text-xs">Total {cost.totalRecipients} — Filtered OptOut {cost.filteredOutOptOut} NoContact {cost.filteredOutNoContact}</div>
                {cost.capWarning && <div className="mt-2 text-xs font-medium text-warning-700">⚠ {cost.capWarningMessage}</div>}
                {cost.capExceeded && <div className="mt-2 text-xs font-bold text-danger-700">⛔ HARD CAP EXCEEDED: {cost.capWarningMessage} — Reduce audience or increase cap</div>}
                <div className="text-xs mt-1">Remaining after send: {cost.remainingAfterSend}</div>
              </div>
            )}
          </div>

          <div className="bg-white border rounded-lg p-3">
            <h3 className="font-semibold text-sm">6. Confirm Bulk Send</h3>
            <button onClick={async()=>{
              const req = { title: `Message ${audienceType} ${new Date().toLocaleString()}`, templateId: selectedTemplate?.id||null, channel, audience: {...audience, type: audienceType, gradeId: audience.gradeId?parseInt(audience.gradeId):null, streamId: audience.streamId?parseInt(audience.streamId):null, academicYearId:2026, arrearsThreshold: audience.arrearsThreshold?parseFloat(audience.arrearsThreshold):null}, customBody: body, customSubject: subject };
              const res=await apiFetch(`${API}/batches`,{method:"POST", headers:{"Content-Type":"application/json", Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}, body: JSON.stringify(req)});
              if(res.ok){
                const data=await res.json();
                alert(`Batch ${data.batch.batchNumber} created ${data.batch.totalRecipients} recipients cost ${data.batch.estimatedCost} — filtered opt-out ${data.filteredOutOptOut} — ${data.capWarning||""}`);
                // Auto confirm for demo? In real flow, show confirmation dialog with cost
                if(confirm(`Confirm send ${data.batch.totalRecipients} messages costing ${data.batch.estimatedCost} ${data.batch.currency}?`)){
                  const res2=await apiFetch(`${API}/batches/${data.batch.id}/confirm`,{method:"POST", headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}});
                  if(res2.ok) alert("Queued - background job with batching retry backoff rate limits");
                }
              } else {
                const err=await res.json();
                alert("Failed: "+(err.message||JSON.stringify(err)));
              }
            }} className="w-full py-3 rounded-lg bg-primary-800 text-white font-semibold min-h-touch">Create Batch — Show Recipient Count + Cost + Confirm</button>
            <p className="text-[11px] text-neutral-500 mt-2">Sending runs as background job with batching 50, retry 3 exponential backoff 2^retry seconds, per-tenant rate limit 10/sec from MessagingProviderSettings. Delivery log per message recipient/channel/status/provider ref/cost/timestamp. Usage counter billing SMS bundles. Hard cap warning before hit.</p>
          </div>
        </div>
      </div>
    </div>
  );
}