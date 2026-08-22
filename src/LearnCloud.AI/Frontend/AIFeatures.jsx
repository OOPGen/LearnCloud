/**
 * AI-Assisted Features - Priority Order
 * 1. Report card comment drafting: given marks, attendance, subject performance, draft teacher comment configurable tone/length, teacher always reviews and edits before saving, nothing written automatically - saves hours per term - build first
 * 2. Attendance anomaly detection: flag unusual patterns weekday consistently missed or sudden drop with explanation why flagged
 * 3. At-risk learner identification: combine falling marks, declining attendance, fee arrears into flag for pastoral follow-up, always shown with underlying reasons
 */

import React, { useState, useEffect } from 'react';
import { apiFetch, getAccessToken, setAccessToken } from '../../LearnCloud.Web/src/lib/apiClient.js'; // SECURITY C2 FIX

const API = "/api/ai";

export default function AIFeatures() {
  const [tab,setTab]=useState("comments");
  return (
    <div className="min-h-screen bg-neutral-50 p-4">
      <h1 className="text-2xl font-bold">AI-Assisted Features — Teacher Always Reviews</h1>
      <p className="text-xs text-neutral-600 mt-1">1. Report card comment drafting saves hours per term — teacher reviews/edits before save, nothing auto-written. 2. Attendance anomaly detection. 3. At-risk flag with reasons.</p>
      <div className="mt-4 flex gap-2 overflow-auto">
        {[
          {id:"comments",label:"1. Report Comments Drafting — Priority"},
          {id:"attendance",label:"2. Attendance Anomaly"},
          {id:"atrisk",label:"3. At-Risk Pastoral Flag"},
        ].map(t=><button key={t.id} onClick={()=>setTab(t.id)} className={`px-3 py-1.5 rounded-full border text-sm whitespace-nowrap ${tab===t.id?"bg-primary-800 text-white":"bg-white"}`}>{t.label}</button>)}
      </div>
      <div className="mt-4">
        {tab==="comments" && <ReportCommentDrafting />}
        {tab==="attendance" && <AttendanceAnomalyDetection />}
        {tab==="atrisk" && <AtRiskIdentification />}
      </div>
    </div>
  );
}

function ReportCommentDrafting() {
  const [form,setForm]=useState({studentId:"",academicYearId:"2026",termId:"1",tone:"encouraging",length:"medium",customInstructions:""});
  const [draft,setDraft]=useState(null);
  const [edited,setEdited]=useState("");
  const [saving,setSaving]=useState(false);

  async function generate(){
    const res=await apiFetch(`${API}/comments/generate`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({
      studentId:parseInt(form.studentId),academicYearId:parseInt(form.academicYearId),termId:parseInt(form.termId),tone:form.tone,length:form.length,customInstructions:form.customInstructions
    })});
    if(res.ok){
      const data=await res.json();
      setDraft(data);
      setEdited(data.draftComment);
    } else alert("Failed: "+await res.text());
  }

  async function review(){
    if(!draft) return;
    const res=await apiFetch(`${API}/comments/${draft.draftId}/review`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({draftId:draft.draftId,editedComment:edited})});
    if(res.ok) alert("Reviewed — still draft, not yet saved to report card");
  }

  async function save(){
    if(!draft) return;
    setSaving(true);
    const res=await apiFetch(`${API}/comments/${draft.draftId}/save`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({draftId:draft.draftId,finalComment:edited})});
    setSaving(false);
    if(res.ok){
      const data=await res.json();
      alert(data.message+" — Teacher reviewed and edited before saving, nothing written automatically. This alone saves hours per term.");
    } else alert("Failed: "+await res.text());
  }

  return (
    <div className="bg-white border rounded-lg p-4 space-y-4">
      <h2 className="font-bold">Report Card Comment Drafting — Given marks, attendance, subject performance, draft teacher comment configurable tone/length — Teacher always reviews and edits before saving</h2>
      <div className="grid sm:grid-cols-3 gap-2">
        <input placeholder="StudentId" value={form.studentId} onChange={e=>setForm({...form,studentId:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <select value={form.tone} onChange={e=>setForm({...form,tone:e.target.value})} className="h-9 px-2 border rounded text-sm"><option value="encouraging">Encouraging</option><option value="formal">Formal</option><option value="concise">Concise</option><option value="detailed">Detailed</option><option value="neutral">Neutral</option></select>
        <select value={form.length} onChange={e=>setForm({...form,length:e.target.value})} className="h-9 px-2 border rounded text-sm"><option value="short">Short</option><option value="medium">Medium</option><option value="long">Long</option></select>
        <input placeholder="Custom instructions e.g. Focus on effort" value={form.customInstructions} onChange={e=>setForm({...form,customInstructions:e.target.value})} className="h-9 px-2 border rounded text-sm col-span-3" />
      </div>
      <button onClick={generate} className="w-full py-3 rounded-lg bg-primary-800 text-white font-semibold min-h-touch">Generate Draft — Saves Teacher Hours Per Term — Priority 1</button>

      {draft && (
        <div className="border rounded-lg p-4 bg-neutral-50 space-y-3">
          <div className="flex justify-between"><span className="font-medium text-sm">{draft.studentName} — Draft by {draft.providerName} {draft.model} — Tone {draft.tone} Length {draft.length}</span><span className="text-xs px-2 py-0.5 rounded bg-warning-100 text-warning-700">Requires Review — Nothing Written Automatically</span></div>
          <div className="grid sm:grid-cols-2 gap-3">
            <div className="p-2 border rounded bg-white">
              <div className="text-xs font-semibold">Subject Performances</div>
              {draft.subjectPerformances.map((s,i)=><div key={i} className="text-xs border-b py-1 flex justify-between"><span>{s.subjectName}</span><span>{s.score?.toFixed(0)}% {s.grade} Trend {s.trend} {s.strengthWeakness}</span></div>)}
            </div>
            <div className="p-2 border rounded bg-white">
              <div className="text-xs font-semibold">Attendance</div>
              <div className="text-xs">{draft.attendance.totalDays} days, {draft.attendance.present} present, {draft.attendance.absent} absent, {draft.attendance.percentage}% — {draft.attendance.summary}</div>
            </div>
          </div>
          <div>
            <div className="text-xs font-semibold">Draft Comment (AI Generated — Teacher Must Review and Edit)</div>
            <div className="mt-1 p-3 border rounded bg-white text-sm whitespace-pre-wrap">{draft.draftComment}</div>
          </div>
          <div>
            <div className="text-xs font-semibold">Edited Comment (Teacher Reviews and Edits Before Saving)</div>
            <textarea value={edited} onChange={e=>setEdited(e.target.value)} rows="6" className="mt-1 w-full px-3 py-2 border rounded text-sm" placeholder="Teacher edits here — always review before saving, nothing written automatically" />
          </div>
          <div className="flex gap-2">
            <button onClick={review} className="flex-1 py-2 rounded border bg-white text-sm">Review — Save as Edited Draft (Not Yet to Report Card)</button>
            <button onClick={save} disabled={saving} className="flex-1 py-2 rounded bg-success-600 text-white text-sm font-semibold disabled:opacity-50">{saving?"Saving...":"Save Final to Report Card — Explicit Teacher Action"}</button>
          </div>
          <p className="text-[11px] text-neutral-500">Teacher always reviews and edits before saving; nothing is written automatically. This alone saves a teacher hours per term and is the feature to build first. Draft → Review → Save flow ensures teacher control.</p>
        </div>
      )}
    </div>
  );
}

function AttendanceAnomalyDetection() {
  const [anomalies,setAnomalies]=useState([]);
  const [filter,setFilter]=useState({gradeId:"",streamId:"",daysBack:"30",threshold:"75"});

  async function detect(){
    const res=await apiFetch(`${API}/attendance-anomalies/detect`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({gradeId:filter.gradeId?parseInt(filter.gradeId):null,streamId:filter.streamId?parseInt(filter.streamId):null,daysBack:parseInt(filter.daysBack),threshold:parseFloat(filter.threshold)})});
    if(res.ok){ const data=await res.json(); setAnomalies(data); }
  }

  useEffect(()=>{ apiFetch(`${API}/attendance-anomalies`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setAnomalies); },[]);

  return (
    <div className="bg-white border rounded-lg p-4 space-y-4">
      <h2 className="font-bold">Attendance Anomaly Detection — Flag Unusual Patterns Such as Specific Weekday Consistently Missed, or Sudden Drop, with Explanation Why Flagged</h2>
      <div className="grid sm:grid-cols-4 gap-2">
        <input placeholder="GradeId filter" value={filter.gradeId} onChange={e=>setFilter({...filter,gradeId:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="StreamId filter" value={filter.streamId} onChange={e=>setFilter({...filter,streamId:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="Days back 30" value={filter.daysBack} onChange={e=>setFilter({...filter,daysBack:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="Threshold 75" value={filter.threshold} onChange={e=>setFilter({...filter,threshold:e.target.value})} className="h-9 px-2 border rounded text-sm" />
      </div>
      <button onClick={detect} className="w-full py-2 rounded bg-primary-800 text-white text-sm">Detect Anomalies — Weekday Pattern, Sudden Drop, Consecutive Absence</button>

      <div className="space-y-2 max-h-96 overflow-auto">
        {anomalies.map(a=>(
          <div key={a.id} className="p-3 border rounded bg-warning-50">
            <div className="flex justify-between"><span className="font-medium text-sm">{a.studentName} {a.studentNumber} — {a.anomalyType}</span><span className={`text-xs px-1.5 py-0.5 rounded ${a.confidenceScore>80?"bg-danger-500 text-white":a.confidenceScore>60?"bg-warning-500 text-white":"bg-neutral-200"}`}>{a.confidenceScore.toFixed(0)}% confidence</span></div>
            <div className="text-sm mt-1">{a.description}</div>
            <div className="text-xs text-neutral-700 mt-1 bg-white p-2 rounded border">Explanation: {a.explanation}</div>
            <div className="text-[11px] text-neutral-500 mt-1">Period {new Date(a.periodFrom).toLocaleDateString()} - {new Date(a.periodTo).toLocaleDateString()} • Detected {new Date(a.detectedAt).toLocaleString()} • Data {a.dataJson}</div>
          </div>
        ))}
        {anomalies.length===0 && <div className="text-xs text-neutral-500">No anomalies — run detection above. Flags: weekday consistently missed (e.g. Mondays 80% miss rate), sudden drop >20% last 7 days vs previous 21, consecutive absence 3+ days.</div>}
      </div>
    </div>
  );
}

function AtRiskIdentification() {
  const [flags,setFlags]=useState([]);
  const [filter,setFilter]=useState({gradeId:"",marksDrop:"15",attendanceDrop:"15",arrearsThreshold:"100"});

  async function detect(){
    const res=await apiFetch(`${API}/at-risk/detect`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({gradeId:filter.gradeId?parseInt(filter.gradeId):null,marksDropThreshold:parseFloat(filter.marksDrop),attendanceDropThreshold:parseFloat(filter.attendanceDrop),arrearsThreshold:parseFloat(filter.arrearsThreshold)})});
    if(res.ok) setFlags(await res.json());
  }

  useEffect(()=>{ apiFetch(`${API}/at-risk`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setFlags); },[]);

  return (
    <div className="bg-white border rounded-lg p-4 space-y-4">
      <h2 className="font-bold">At-Risk Learner Identification — Combine Falling Marks, Declining Attendance and Fee Arrears into Flag for Pastoral Follow-Up, Always Shown with Underlying Reasons</h2>
      <div className="grid sm:grid-cols-4 gap-2">
        <input placeholder="GradeId filter" value={filter.gradeId} onChange={e=>setFilter({...filter,gradeId:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="Marks drop threshold 15%" value={filter.marksDrop} onChange={e=>setFilter({...filter,marksDrop:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="Attendance drop 15%" value={filter.attendanceDrop} onChange={e=>setFilter({...filter,attendanceDrop:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="Arrears threshold 100" value={filter.arrearsThreshold} onChange={e=>setFilter({...filter,arrearsThreshold:e.target.value})} className="h-9 px-2 border rounded text-sm" />
      </div>
      <button onClick={detect} className="w-full py-2 rounded bg-primary-800 text-white text-sm">Detect At-Risk — Falling Marks + Declining Attendance + Fee Arrears</button>

      <div className="space-y-2 max-h-[600px] overflow-auto">
        {flags.map(f=>(
          <div key={f.id} className={`p-3 border rounded ${f.riskLevel==="critical"?"bg-danger-50 border-danger-200":f.riskLevel==="high"?"bg-warning-50 border-warning-200":"bg-neutral-50"}`}>
            <div className="flex justify-between"><span className="font-medium text-sm">{f.studentName} {f.studentNumber} — {f.gradeName} {f.streamName} — {f.riskLevel} risk {f.riskScore.toFixed(0)}</span><span className={`text-xs px-1.5 py-0.5 rounded ${f.riskLevel==="critical"?"bg-danger-500 text-white":f.riskLevel==="high"?"bg-warning-500 text-white":"bg-neutral-200"}`}>{f.riskLevel}</span></div>
            <div className="text-sm mt-1">{f.flagReason}</div>
            <div className="mt-2 space-y-1">
              {f.underlyingReasons.map((r,i)=>(
                <div key={i} className="text-xs p-2 rounded bg-white border flex justify-between">
                  <span><strong>{r.type}</strong>: {r.detail}</span>
                  <span className={`px-1 rounded ${r.severity==="high"?"bg-danger-100 text-danger-700":r.severity==="medium"?"bg-warning-100":"bg-neutral-100"}`}>{r.severity} {r.score?.toFixed(1)}</span>
                </div>
              ))}
            </div>
            <div className="text-[11px] text-neutral-500 mt-1">Detected {new Date(f.detectedAt).toLocaleString()} • Status {f.status} • Always shown with underlying reasons for pastoral follow-up</div>
          </div>
        ))}
        {flags.length===0 && <div className="text-xs text-neutral-500">No at-risk flags — run detection. Combines falling marks 15%+, declining attendance 15%+, fee arrears over threshold, low attendance &lt;85%, into risk score 0-100 level low/medium/high/critical, always with underlying reasons.</div>}
      </div>
    </div>
  );
}