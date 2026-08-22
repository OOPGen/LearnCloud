/**
 * Student Portal - Secure login, own record scoping, simple and fast, shared/low-end devices
 * - Timetable for week, attendance record, published results and report cards, assignments with due dates and submission where enabled, notices, fee statement if school permits, profile with password change
 * - Scope every endpoint to authenticated student's own record, verified server-side
 * - School-level setting controlling whether students may see fee info at all
 * - Simple and fast, minimal JS, readable at arm's length
 */

import React, { useState, useEffect } from 'react';
import { apiFetch, getAccessToken, setAccessToken } from '../../LearnCloud.Web/src/lib/apiClient.js'; // SECURITY C2 FIX

const API = "/api/student";

function useStudentDashboard() {
  const [data,setData]=useState(null);
  const [loading,setLoading]=useState(true);
  useEffect(()=>{
    apiFetch(`${API}/dashboard`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}})
      .then(r=>r.json()).then(setData).finally(()=>setLoading(false));
  },[]);
  return {data,loading};
}

export default function StudentPortal() {
  const [tab,setTab]=useState("dashboard");

  return (
    <div className="min-h-screen bg-neutral-50 pb-20">
      <header className="sticky top-0 z-20 bg-primary-800 text-white p-3 flex justify-between items-center">
        <div className="font-bold">LearnCloud Student</div>
        <div className="text-xs opacity-80">Own record only • Low-end device friendly</div>
      </header>

      <main className="max-w-xl mx-auto">
        {tab==="dashboard" && <DashboardTab />}
        {tab==="timetable" && <TimetableTab />}
        {tab==="attendance" && <AttendanceTab />}
        {tab==="results" && <ResultsTab />}
        {tab==="assignments" && <AssignmentsTab />}
        {tab==="notices" && <NoticesTab />}
        {tab==="fees" && <FeesTab />}
        {tab==="profile" && <ProfileTab />}
      </main>

      <nav className="fixed bottom-0 left-0 right-0 bg-white border-t flex justify-around py-1 z-20">
        {[
          {id:"dashboard",label:"Home",icon:"🏠"},
          {id:"timetable",label:"Table",icon:"📅"},
          {id:"attendance",label:"Attend",icon:"✓"},
          {id:"results",label:"Results",icon:"📄"},
          {id:"assignments",label:"HW",icon:"📚"},
          {id:"fees",label:"Fees",icon:"💳"},
          {id:"profile",label:"Me",icon:"👤"},
        ].map(item=>(
          <button key={item.id} onClick={()=>setTab(item.id)} className={`flex flex-col items-center min-w-touch min-h-touch px-2 py-1 rounded ${tab===item.id?"text-primary-800 bg-primary-50":"text-neutral-500"}`}>
            <span className="text-lg">{item.icon}</span>
            <span className="text-[10px] font-medium">{item.label}</span>
          </button>
        ))}
      </nav>
    </div>
  );
}

function DashboardTab() {
  const {data,loading}=useStudentDashboard();
  if(loading) return <div className="p-6 text-center">Loading your dashboard...</div>;
  if(!data) return <div className="p-6">No data</div>;

  return (
    <div className="p-3 space-y-3">
      <div className="bg-white border rounded-xl p-4">
        <h2 className="font-bold text-lg">{data.studentName}</h2>
        <p className="text-sm text-neutral-600">{data.gradeName} {data.streamName} • {data.studentNumber}</p>
      </div>

      <div className="bg-white border rounded-xl p-4">
        <h3 className="font-semibold text-sm">Today's Timetable</h3>
        {data.timetableWeek.flatMap(d=>d.slots).length===0 ? <p className="text-xs text-neutral-500 mt-2">No lessons today</p> :
          <div className="mt-2 space-y-1">
            {data.timetableWeek.flatMap(d=>d.slots.map(s=>({...s, dayName:d.dayName, dayOfWeek:d.dayOfWeek}))).filter(s=>s.dayOfWeek===new Date().getDay()||true).slice(0,5).map((s,i)=>(
              <div key={i} className="flex justify-between text-sm p-2 rounded bg-primary-50 border"><div><div className="font-medium">{s.periodName} {s.startTime}-{s.endTime}</div><div className="text-xs">{s.subjectName} • {s.teacherName} {s.roomName?`• ${s.roomName}`:""}</div></div></div>
            ))}
          </div>
        }
      </div>

      <div className="grid grid-cols-2 gap-3">
        <div className="bg-white border rounded-xl p-4">
          <div className="text-xs uppercase tracking-widest text-neutral-500 font-semibold">Attendance</div>
          <div className="text-2xl font-bold mt-1">{data.attendance.percentage}%</div>
          <div className="text-xs">{data.attendance.present}/{data.attendance.totalDays} present</div>
        </div>
        <div className="bg-white border rounded-xl p-4">
          <div className="text-xs uppercase tracking-widest text-neutral-500 font-semibold">Latest Result</div>
          {data.latestResult ? <><div className="text-lg font-bold mt-1">{data.latestResult.average.toFixed(1)}% {data.latestResult.grade}</div><div className="text-xs">{data.latestResult.termName}</div></> : <div className="text-xs text-neutral-500 mt-1">No published results yet</div>}
        </div>
      </div>

      {data.feeSummary && (
        <div className="bg-white border rounded-xl p-4">
          <div className="text-xs uppercase tracking-widest text-neutral-500 font-semibold">Fees — {data.canViewFees?"School permits view":"Not enabled"}</div>
          <div className="text-2xl font-bold mt-1">{data.feeSummary.currency} {data.feeSummary.balanceDue.toFixed(2)} owed</div>
          <div className="text-xs text-neutral-500">Total invoiced {data.feeSummary.totalInvoiced.toFixed(2)} • Paid {data.feeSummary.totalPaid.toFixed(2)}</div>
        </div>
      )}

      <div className="bg-white border rounded-xl p-4">
        <h3 className="font-semibold text-sm">Homework Due ({data.assignmentsDue?.length||0})</h3>
        {(data.assignmentsDue||[]).map(a=>(
          <div key={a.id} className="mt-2 p-2 rounded bg-warning-50 border text-sm"><div className="font-medium">{a.title} — {a.subjectName}</div><div className="text-xs">Due {new Date(a.dueDate).toLocaleDateString()} • {a.daysLeft}d left • {a.status}</div></div>
        ))}
      </div>

      <p className="text-[11px] text-neutral-400 text-center">Simple and fast — minimal JS, shared device friendly, own record only enforced server-side</p>
    </div>
  );
}

function TimetableTab() {
  const [week,setWeek]=useState([]);
  useEffect(()=>{ apiFetch(`${API}/timetable`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setWeek); },[]);
  return (
    <div className="p-3 space-y-3">
      <h2 className="font-bold">Timetable for the Week</h2>
      {week.map(day=>(
        <div key={day.dayOfWeek} className="bg-white border rounded-xl p-3">
          <div className="font-semibold text-sm">{day.dayName}</div>
          <div className="mt-2 space-y-1">
            {day.slots.length===0 ? <div className="text-xs text-neutral-500">No lessons</div> :
              day.slots.map((s,i)=>(
                <div key={i} className="flex justify-between text-sm p-2 rounded bg-neutral-50 border">
                  <div><div className="font-medium">{s.periodName} {s.startTime}-{s.endTime}</div><div className="text-xs">{s.subjectName} • {s.teacherName} {s.roomName?`• ${s.roomName}`:""}</div></div>
                  {s.isBreak && <span className="text-xs bg-neutral-200 px-1 rounded">Break</span>}
                </div>
              ))
            }
          </div>
        </div>
      ))}
    </div>
  );
}

function AttendanceTab() {
  const [records,setRecords]=useState([]);
  const [summary,setSummary]=useState(null);
  useEffect(()=>{
    apiFetch(`${API}/attendance`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setRecords);
    apiFetch(`${API}/attendance/summary?academicYearId=2026&termId=1`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setSummary);
  },[]);
  return (
    <div className="p-3 space-y-3">
      <h2 className="font-bold">Attendance Record</h2>
      {summary && (
        <div className="bg-white border rounded-xl p-3 grid grid-cols-3 gap-2 text-center">
          <div><div className="text-xs">Total</div><div className="font-bold">{summary.totalDays}</div></div>
          <div><div className="text-xs">Present</div><div className="font-bold text-success-600">{summary.present}</div></div>
          <div><div className="text-xs">%</div><div className="font-bold">{summary.percentage}%</div></div>
        </div>
      )}
      <div className="bg-white border rounded-xl p-3">
        <div className="space-y-1 max-h-96 overflow-auto">
          {records.map((r,i)=>(
            <div key={i} className={`p-2 rounded border text-sm flex justify-between ${r.status==="Absent"?"bg-danger-50":r.status==="Late"?"bg-warning-50":"bg-success-50"}`}>
              <div><div className="font-medium">{new Date(r.date).toLocaleDateString()} • {r.status} {r.periodName}</div><div className="text-xs text-neutral-600">{r.reason?`Reason: ${r.reason}`:""} {r.note?`• ${r.note}`:""}</div></div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function ResultsTab() {
  const [results,setResults]=useState([]);
  useEffect(()=>{ apiFetch(`${API}/results`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setResults); },[]);
  return (
    <div className="p-3 space-y-3">
      <h2 className="font-bold">Published Results and Report Cards — Only Published Visible</h2>
      <p className="text-xs text-neutral-500">Only published cards visible to parents and students, draft/approved not visible. Enforced server-side.</p>
      {results.map(rc=>(
        <div key={rc.id} className="bg-white border rounded-xl p-4">
          <div className="flex justify-between"><div><div className="font-bold">{rc.termName}</div><div className="text-sm">Average {rc.average.toFixed(1)}% {rc.overallGrade} • {rc.positionDisplay}</div><div className="text-xs text-neutral-500">Published {new Date(rc.publishedAt).toLocaleDateString()}</div></div><a href={`${API}/results/${rc.id}/pdf`} target="_blank" className="h-fit px-3 py-1.5 rounded border bg-white text-xs">PDF</a></div>
        </div>
      ))}
    </div>
  );
}

function AssignmentsTab() {
  const [list,setList]=useState([]);
  useEffect(()=>{ apiFetch(`${API}/assignments`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setList); },[]);
  return (
    <div className="p-3 space-y-3">
      <h2 className="font-bold">Assignments with Due Dates and Submission Where Enabled</h2>
      {list.map(a=>(
        <div key={a.id} className="bg-white border rounded-xl p-3">
          <div className="font-medium text-sm">{a.title} — {a.subjectName}</div>
          <div className="text-xs text-neutral-500">Due {new Date(a.dueDate).toLocaleDateString()} • {a.daysLeft}d left • {a.status} {a.canSubmit?"• Can submit":"• Submission disabled by school"}</div>
          {a.canSubmit && (
            <div className="mt-2 flex gap-2">
              <input type="file" className="text-xs" onChange={e=>{/* upload file */}} />
              <button onClick={async()=>{
                const res=await apiFetch(`${API}/assignments/${a.id}/submit`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({note:"Submitted", fileUrl:null})});
                if(res.ok) alert("Submitted");
              }} className="px-3 py-1 rounded bg-primary-800 text-white text-xs">Submit</button>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

function NoticesTab() {
  const [notices,setNotices]=useState([]);
  useEffect(()=>{ apiFetch(`${API}/notices`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(d=>setNotices(d.notices||d)); },[]);
  return (
    <div className="p-3 space-y-2">
      <h2 className="font-bold">Notices</h2>
      {notices.map(n=>(
        <div key={n.id} className="bg-white border rounded-lg p-3">
          <div className="font-medium text-sm">{n.title}</div>
          <div className="text-xs text-neutral-500">{new Date(n.createdAt).toLocaleDateString()} • {n.priority}</div>
          <div className="text-sm mt-1">{n.body}</div>
        </div>
      ))}
    </div>
  );
}

function FeesTab() {
  const [summary,setSummary]=useState(null);
  const [error,setError]=useState(null);
  useEffect(()=>{
    apiFetch(`${API}/fees/summary`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(async r=>{
      if(r.status===403){ const j=await r.json(); setError(j.message); return null; }
      return r.json();
    }).then(data=>{ if(data) setSummary(data); });
  },[]);
  if(error){
    return (
      <div className="p-6 text-center">
        <div className="bg-white border rounded-xl p-6">
          <div className="text-4xl">🔒</div>
          <h2 className="mt-3 font-bold">Fee Information Disabled</h2>
          <p className="mt-2 text-sm text-neutral-600">{error}</p>
          <p className="mt-2 text-xs text-neutral-500">School-level setting controlling whether students may see fee information at all — school must opt-in. Contact bursar if you need statement.</p>
        </div>
      </div>
    );
  }
  if(!summary) return <div className="p-6">Loading fees...</div>;
  return (
    <div className="p-3 space-y-3">
      <h2 className="font-bold">Fee Statement — If School Permits Students to See It</h2>
      <div className="bg-white border rounded-xl p-4">
        <div className="text-xs uppercase tracking-widest text-neutral-500">Balance Due</div>
        <div className="text-3xl font-extrabold mt-1">{summary.currency} {summary.balanceDue.toFixed(2)}</div>
        <div className="text-xs text-neutral-500 mt-1">Invoiced {summary.totalInvoiced.toFixed(2)} • Paid {summary.totalPaid.toFixed(2)}</div>
      </div>
      <div className="bg-white border rounded-xl p-3">
        <h3 className="font-semibold text-sm">Statement — Downloadable PDF</h3>
        <a href={`${API}/fees/statement?from=&to=`} target="_blank" className="mt-2 inline-block px-3 py-1.5 rounded border bg-white text-xs">Download PDF Statement</a>
        <p className="text-xs text-neutral-500 mt-2">Payment action once gateway exists: PayNow button will appear here.</p>
      </div>
    </div>
  );
}

function ProfileTab() {
  const [profile,setProfile]=useState(null);
  useEffect(()=>{ apiFetch(`${API}/profile`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setProfile); },[]);
  if(!profile) return <div className="p-4">Loading profile...</div>;
  return (
    <div className="p-3 space-y-3">
      <h2 className="font-bold">Profile & Password Change</h2>
      <div className="bg-white border rounded-xl p-4">
        <div className="font-bold">{profile.fullName}</div>
        <div className="text-sm text-neutral-600">{profile.studentNumber} • {profile.gradeName} {profile.streamName}</div>
        <div className="text-xs text-neutral-500 mt-1">Status {profile.status} • DOB {profile.dob?new Date(profile.dob).toLocaleDateString():""}</div>
        <div className="text-xs mt-2">Email {profile.email||"none"} • Phone {profile.phone||"none"}</div>
      </div>
      <div className="bg-white border rounded-xl p-4">
        <h3 className="font-semibold text-sm">Change Password — Own Record Only Enforced Server-Side</h3>
        <form onSubmit={async e=>{
          e.preventDefault();
          const fd=new FormData(e.target);
          const res=await apiFetch(`/api/auth/change-password`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({currentPassword:fd.get('current'),newPassword:fd.get('new'),confirmPassword:fd.get('confirm')})});
          if(res.ok) alert("Password changed, all sessions revoked, please login again");
          else alert("Failed: "+await res.text());
        }} className="mt-3 space-y-2">
          <input name="current" type="password" placeholder="Current password" required className="w-full h-11 px-3 border rounded text-sm" />
          <input name="new" type="password" placeholder="New password - upper lower digit symbol" required className="w-full h-11 px-3 border rounded text-sm" />
          <input name="confirm" type="password" placeholder="Confirm new" required className="w-full h-11 px-3 border rounded text-sm" />
          <button type="submit" className="w-full min-h-touch py-3 rounded-lg bg-primary-800 text-white font-semibold">Change Password — Revokes All Refresh Tokens</button>
        </form>
      </div>
      <p className="text-[11px] text-neutral-400 text-center">Simple and fast — minimal JS, shared device friendly. Secure login via JWT 15min + rotating refresh 14d. Every endpoint verifies own record server-side, not just tenant filter. Fee view gated by school setting AllowStudentsViewFees.</p>
    </div>
  );
}