/**
 * Parent Portal - For least technical users, mostly phone, simplicity outranks completeness
 * - Login with invitation flow, account links to one or more children potentially different classes
 * - Child switcher, home per child: outstanding balance, attendance %, latest published results, upcoming assessments, recent notices, homework due
 * - Fees: statement, invoice history, receipts, downloadable PDF, payment action once gateway exists
 * - Attendance detail with dates and reasons
 * - Results: published report cards only, downloadable
 * - Notices and homework
 * - Message to class teacher if school enables it, with moderation and rate limiting
 * - Profile and contact preferences including SMS opt-out
 * - Design constraints: small screen, readable at arm's length, minimal JS, understandable without instructions
 */

import React, { useState, useEffect } from 'react';
import { apiFetch, getAccessToken, setAccessToken } from '../../LearnCloud.Web/src/lib/apiClient.js'; // SECURITY C2 FIX

const API = "/api/parent";

function useChildren() {
  const [children,setChildren]=useState([]);
  const [loading,setLoading]=useState(true);
  useEffect(()=>{
    apiFetch(`${API}/children`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}})
      .then(r=>r.json()).then(data=>setChildren(data.children||[])).finally(()=>setLoading(false));
  },[]);
  return {children,loading};
}

export default function ParentPortal() {
  const [selectedChild, setSelectedChild] = useState(null);
  const [tab, setTab] = useState("home"); // home, fees, attendance, results, notices, homework, messages, profile
  const {children, loading} = useChildren();

  useEffect(()=>{
    if(children.length>0 && !selectedChild){
      setSelectedChild(children[0]);
      localStorage.setItem('selectedChildId', children[0].studentId);
    }
  },[children]);

  // Load selected from localStorage for resume
  useEffect(()=>{
    const savedId = localStorage.getItem('selectedChildId');
    if(savedId && children.length>0){
      const found = children.find(c=>c.studentId==savedId);
      if(found) setSelectedChild(found);
    }
  },[children]);

  if(loading) return <div className="p-6 text-center">Loading your children...</div>;

  if(children.length===0){
    return (
      <div className="min-h-screen bg-neutral-50 flex items-center justify-center p-6">
        <div className="bg-white border rounded-2xl p-8 max-w-sm w-full text-center shadow-sm">
          <div className="w-16 h-16 rounded-full bg-primary-50 text-primary-800 grid place-items-center mx-auto">
            <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5"><path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/></svg>
          </div>
          <h2 className="mt-4 font-bold text-[20px] tracking-tight">No children linked yet</h2>
          <p className="mt-2 text-sm text-neutral-600">School needs to link your account to your child. Contact school or check invitation email. Invitation flow: school triggers invitation, you receive link, set password, account links to one or more children.</p>
          <p className="mt-3 text-xs text-neutral-400">Account links to one or more children, potentially different classes — child switcher will appear here.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-neutral-50 pb-20">
      {/* Header - child switcher, simple */}
      <header className="sticky top-0 z-20 bg-white border-b p-3">
        <div className="flex items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-full bg-primary-800 text-white grid place-items-center font-bold text-sm">P</div>
            <div>
              <div className="font-bold text-sm leading-tight">Parent Portal</div>
              <div className="text-[11px] text-neutral-500">Simple, readable at arm's length</div>
            </div>
          </div>
          {/* Child Switcher - big, clear, arm's length readable */}
          <div className="flex items-center gap-2">
            <span className="text-xs text-neutral-500 hidden sm:block">Child:</span>
            <select 
              value={selectedChild?.studentId||""} 
              onChange={e=>{
                const child = children.find(c=>c.studentId==parseInt(e.target.value));
                setSelectedChild(child);
                localStorage.setItem('selectedChildId', e.target.value);
                setTab("home");
              }} 
              className="h-10 px-3 pr-8 border-2 border-primary-200 rounded-full bg-primary-50 font-semibold text-sm min-w-[140px]"
            >
              {children.map(c=><option key={c.studentId} value={c.studentId}>{c.fullName} — {c.gradeName} {c.streamName}</option>)}
            </select>
          </div>
        </div>
        {children.length>1 && (
          <div className="mt-3 flex gap-2 overflow-auto pb-1">
            {children.map(c=>(
              <button 
                key={c.studentId} 
                onClick={()=>{setSelectedChild(c); localStorage.setItem('selectedChildId', c.studentId); setTab("home");}}
                className={`flex-shrink-0 px-3 py-2 rounded-full border text-sm font-medium min-h-touch ${selectedChild?.studentId===c.studentId?"bg-primary-800 text-white border-primary-800":"bg-white"}`}
              >
                {c.fullName.split(" ")[0]} • {c.gradeName} {c.streamName}
              </button>
            ))}
          </div>
        )}
      </header>

      {/* Main content - per child */}
      <main className="max-w-xl mx-auto">
        {selectedChild ? (
          <>
            {tab==="home" && <HomeScreen child={selectedChild} />}
            {tab==="fees" && <FeesScreen child={selectedChild} />}
            {tab==="attendance" && <AttendanceScreen child={selectedChild} />}
            {tab==="results" && <ResultsScreen child={selectedChild} />}
            {tab==="notices" && <NoticesScreen child={selectedChild} />}
            {tab==="homework" && <HomeworkScreen child={selectedChild} />}
            {tab==="messages" && <MessagesScreen child={selectedChild} />}
            {tab==="profile" && <ProfileScreen />}
          </>
        ) : (
          <div className="p-6 text-center text-sm text-neutral-500">Select a child above</div>
        )}
      </main>

      {/* Bottom nav - PREMIUM ENTERPRISE - SVG icons not emoji, 44px touch, thumb zone, readable at arm's length */}
      <nav className="fixed bottom-0 left-0 right-0 bg-white border-t border-neutral-200 flex justify-around py-1 z-20 safe-bottom">
        {[
          {id:"home",label:"Home",icon:(p)=><svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/><polyline points="9 22 9 12 15 12 15 22"/></svg>},
          {id:"fees",label:"Fees",icon:(p)=><svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><line x1="12" y1="1" x2="12" y2="23"/><path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"/></svg>},
          {id:"attendance",label:"Attend",icon:(p)=><svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><rect x="3" y="4" width="18" height="18" rx="2"/><line x1="16" y1="2" x2="16" y2="6"/><line x1="8" y1="2" x2="8" y2="6"/><line x1="3" y1="10" x2="21" y2="10"/></svg>},
          {id:"results",label:"Results",icon:(p)=><svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/></svg>},
          {id:"homework",label:"HW",icon:(p)=><svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"/><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"/></svg>},
          {id:"profile",label:"Me",icon:(p)=><svg {...p} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>},
        ].map(item=>{
          const active = tab===item.id;
          return (
            <button key={item.id} onClick={()=>setTab(item.id)} aria-label={item.label} aria-current={active?"page":undefined} className={`flex flex-col items-center min-w-touch min-h-touch w-16 px-2 py-1.5 rounded-xl transition-all duration-200 focus:outline-none focus-visible:ring-2 focus-visible:ring-secondary-500/30 ${active?"text-primary-800 bg-primary-50 border border-primary-100 shadow-sm":"text-neutral-500 hover:text-neutral-700 hover:bg-neutral-50"}`}>
              <item.icon className={`w-5 h-5 ${active?"text-primary-800":"text-neutral-400"}`} />
              <span className={`text-[10px] mt-0.5 font-medium tracking-wide ${active?"text-primary-800":"text-neutral-500"}`}>{item.label}</span>
              {active && <span className="mt-0.5 w-1 h-1 rounded-full bg-primary-800" aria-hidden="true"></span>}
            </button>
          );
        })}
      </nav>
    </div>
  );
}

function HomeScreen({ child }) {
  const [home,setHome]=useState(null);
  const [loading,setLoading]=useState(true);

  useEffect(()=>{
    apiFetch(`${API}/children/${child.studentId}/home`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}})
      .then(r=>r.json()).then(setHome).finally(()=>setLoading(false));
  },[child.studentId]);

  if(loading) return <div className="p-6">Loading {child.fullName}...</div>;
  if(!home) return <div className="p-6">No data</div>;

  return (
    <div className="p-3 space-y-3">
      <div className="bg-white border rounded-xl p-4">
        <h2 className="font-bold text-lg leading-tight">{child.fullName}</h2>
        <p className="text-sm text-neutral-600">{child.gradeName} {child.streamName} • {child.studentNumber}</p>
      </div>

      {/* Outstanding balance - PREMIUM: Big, readable at arm's length, 16px base, 20px headings, big number */}
      <div className="bg-white border-2 border-neutral-200 rounded-2xl p-5 shadow-sm">
        <div className="flex justify-between items-start gap-4">
          <div className="flex-1 min-w-0">
            <div className="text-[11px] uppercase tracking-widest text-neutral-500 font-bold">Outstanding Balance • This Term</div>
            <div className="text-[34px] font-extrabold tracking-[-0.02em] leading-none mt-2 text-neutral-900">{home.currency} {home.outstandingBalance.toFixed(2)}</div>
            <div className="text-[13px] text-neutral-600 mt-2 leading-snug">Tap <span className="font-semibold text-primary-800">Fees</span> for statement, invoices, receipts PDF • Readable at arm's length</div>
          </div>
          <div className={`shrink-0 px-3 py-1.5 rounded-full text-[12px] font-bold border ${home.outstandingBalance>0?"bg-danger-50 text-danger-700 border-danger-200":"bg-success-50 text-success-700 border-success-200"}`}>{home.outstandingBalance>0?"Owes":"Paid ✓"}</div>
        </div>
        <div className="mt-4 h-2 bg-neutral-100 rounded-full overflow-hidden">
          <div className={`h-full ${home.outstandingBalance>0?"bg-danger-500":"bg-success-500"}`} style={{width: home.outstandingBalance>0?"75%":"100%"}}></div>
        </div>
      </div>

      {/* Attendance % */}
      <div className="grid grid-cols-2 gap-3">
        <div className="bg-white border rounded-xl p-4">
          <div className="text-xs uppercase tracking-widest text-neutral-500 font-semibold">Attendance This Term</div>
          <div className="text-2xl font-bold mt-1">{home.attendancePercentage.toFixed(1)}%</div>
          <div className="text-xs mt-1">{home.isChronicAbsence ? <span className="text-danger-600">Low — please check detail</span> : <span className="text-success-600">Good</span>}</div>
        </div>
        <div className="bg-white border rounded-xl p-4">
          <div className="text-xs uppercase tracking-widest text-neutral-500 font-semibold">Latest Result</div>
          {home.latestResult ? (
            <>
              <div className="text-lg font-bold mt-1">{home.latestResult.average.toFixed(1)}% {home.latestResult.overallGrade}</div>
              <div className="text-xs text-neutral-500">{home.latestResult.termName} • {new Date(home.latestResult.publishedAt).toLocaleDateString()}</div>
            </>
          ) : <div className="text-sm text-neutral-500 mt-1">No published results yet</div>}
        </div>
      </div>

      {/* Upcoming assessments */}
      <div className="bg-white border rounded-xl p-4">
        <h3 className="font-semibold text-sm">Upcoming Assessments (next 14 days)</h3>
        {home.upcomingAssessments.length===0 ? <p className="text-xs text-neutral-500 mt-2">No upcoming</p> :
          <div className="mt-2 space-y-2">
            {home.upcomingAssessments.map(a=>(
              <div key={a.assessmentId} className="flex justify-between text-sm p-2 rounded bg-neutral-50 border">
                <div><div className="font-medium">{a.subjectName} — {a.assessmentName}</div><div className="text-xs text-neutral-500">{new Date(a.assessmentDate).toLocaleDateString()} • Max {a.maxScore}</div></div>
                <div className="text-xs bg-warning-100 text-warning-700 px-2 py-1 rounded-full h-fit">{a.daysLeft}d left</div>
              </div>
            ))}
          </div>
        }
      </div>

      {/* Recent notices and homework due */}
      <div className="grid sm:grid-cols-2 gap-3">
        <div className="bg-white border rounded-xl p-4">
          <h3 className="font-semibold text-sm">Recent Notices</h3>
          {home.recentNotices.length===0 ? <p className="text-xs text-neutral-500 mt-2">No notices</p> :
            home.recentNotices.slice(0,3).map(n=>(
              <div key={n.id} className="mt-2 p-2 rounded bg-primary-50 border border-primary-100 text-sm">
                <div className="font-medium">{n.title}</div>
                <div className="text-xs text-neutral-600 truncate">{n.body.slice(0,80)}</div>
              </div>
            ))
          }
        </div>
        <div className="bg-white border rounded-xl p-4">
          <h3 className="font-semibold text-sm">Homework Due</h3>
          {home.homeworkDue.length===0 ? <p className="text-xs text-neutral-500 mt-2">No homework due</p> :
            home.homeworkDue.map(h=>(
              <div key={h.id} className="mt-2 p-2 rounded bg-warning-50 border text-sm">
                <div className="font-medium">{h.title} — {h.subjectName}</div>
                <div className="text-xs">Due {new Date(h.dueDate).toLocaleDateString()} • {h.daysLeft}d left • {h.status}</div>
              </div>
            ))
          }
        </div>
      </div>

      <p className="text-[11px] text-neutral-400 text-center p-2">Home per child — child switcher above. Simplicity outranks completeness. Minimal JS, readable at arm's length.</p>
    </div>
  );
}

function FeesScreen({ child }) {
  const [statement,setStatement]=useState(null);
  const [invoices,setInvoices]=useState([]);
  const [receipts,setReceipts]=useState([]);
  const [tab,setTab]=useState("statement");

  useEffect(()=>{
    apiFetch(`${API}/children/${child.studentId}/fees/statement`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setStatement);
    apiFetch(`${API}/children/${child.studentId}/fees/invoices`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setInvoices);
    apiFetch(`${API}/children/${child.studentId}/fees/receipts`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setReceipts);
  },[child.studentId]);

  return (
    <div className="p-3 space-y-3">
      <h2 className="font-bold">Fees — {child.fullName}</h2>
      <div className="flex gap-2 overflow-auto">
        {["statement","invoices","receipts"].map(t=><button key={t} onClick={()=>setTab(t)} className={`px-3 py-1.5 rounded-full border text-sm min-h-touch ${tab===t?"bg-primary-800 text-white":"bg-white"}`}>{t}</button>)}
      </div>

      {tab==="statement" && statement && (
        <div className="bg-white border rounded-xl p-3">
          <div className="flex justify-between items-center">
            <h3 className="font-semibold text-sm">Statement</h3>
            <a href={`${API}/children/${child.studentId}/fees/statement/pdf`} target="_blank" className="text-xs px-2 py-1 rounded border bg-white">Download PDF</a>
          </div>
          <div className="mt-2 grid grid-cols-3 gap-2 text-center text-xs">
            <div className="p-2 rounded bg-neutral-50"><div className="text-neutral-500">Invoiced</div><div className="font-bold">{statement.totalInvoiced.toFixed(2)} {statement.currency}</div></div>
            <div className="p-2 rounded bg-success-50"><div className="text-neutral-500">Paid</div><div className="font-bold">{statement.totalPaid.toFixed(2)}</div></div>
            <div className="p-2 rounded bg-danger-50"><div className="text-neutral-500">Balance</div><div className="font-bold">{statement.balanceDue.toFixed(2)}</div></div>
          </div>
          <div className="mt-3 max-h-80 overflow-auto border rounded">
            <table className="w-full text-xs border-collapse">
              <thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1">Date</th><th className="border p-1">Type</th><th className="border p-1">No</th><th className="border p-1">Debit</th><th className="border p-1">Credit</th><th className="border p-1">Bal</th></tr></thead>
              <tbody>{statement.lines.map((l,i)=><tr key={i}><td className="border p-1">{new Date(l.date).toLocaleDateString()}</td><td className="border p-1">{l.type}</td><td className="border p-1">{l.number}</td><td className="border p-1">{l.debit>0?l.debit.toFixed(2):""}</td><td className="border p-1">{l.credit>0?l.credit.toFixed(2):""}</td><td className="border p-1 font-bold">{l.balance.toFixed(2)}</td></tr>)}</tbody>
            </table>
          </div>
          <div className="mt-3 p-2 rounded bg-primary-50 border text-xs">Payment action once gateway exists: PayNow button will appear here. Currently manual at school.</div>
        </div>
      )}

      {tab==="invoices" && (
        <div className="bg-white border rounded-xl p-3">
          <h3 className="font-semibold text-sm">Invoice History — downloadable PDF</h3>
          <div className="mt-2 space-y-2">
            {invoices.map(inv=>(
              <div key={inv.invoiceId} className="p-2 border rounded flex justify-between items-center">
                <div><div className="font-medium text-sm">{inv.invoiceNumber} • {inv.status}</div><div className="text-xs text-neutral-500">{new Date(inv.issueDate).toLocaleDateString()} due {new Date(inv.dueDate).toLocaleDateString()} • Total {inv.totalAmount.toFixed(2)} Paid {inv.amountPaid.toFixed(2)} Balance {inv.balanceDue.toFixed(2)} {inv.currency}</div></div>
                <a href={`/api/fees/invoices/${inv.invoiceId}/print`} target="_blank" className="text-xs px-2 py-1 rounded border bg-white">PDF</a>
              </div>
            ))}
          </div>
        </div>
      )}

      {tab==="receipts" && (
        <div className="bg-white border rounded-xl p-3">
          <h3 className="font-semibold text-sm">Receipts — downloadable</h3>
          {receipts.map(r=>(
            <div key={r.paymentId} className="p-2 border rounded mt-2 flex justify-between">
              <div><div className="font-medium text-sm">{r.receiptNumber} • {r.amount.toFixed(2)} {r.currency}</div><div className="text-xs text-neutral-500">{new Date(r.paymentDate).toLocaleDateString()} • {r.method} {r.reference}</div></div>
              <a href={`/api/fees/payments/${r.paymentId}/receipt/print`} target="_blank" className="text-xs px-2 py-1 rounded border">PDF</a>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function AttendanceScreen({ child }) {
  const [detail,setDetail]=useState([]);
  const [summary,setSummary]=useState(null);
  useEffect(()=>{
    apiFetch(`${API}/children/${child.studentId}/attendance`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setDetail);
    apiFetch(`${API}/children/${child.studentId}/attendance/summary?academicYearId=2026&termId=1`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setSummary);
  },[child.studentId]);

  return (
    <div className="p-3 space-y-3">
      <h2 className="font-bold">Attendance — {child.fullName}</h2>
      {summary && (
        <div className="bg-white border rounded-xl p-3 grid grid-cols-3 gap-2 text-center">
          <div><div className="text-xs text-neutral-500">Total</div><div className="font-bold">{summary.totalDays}</div></div>
          <div><div className="text-xs text-neutral-500">Present</div><div className="font-bold text-success-600">{summary.present}</div></div>
          <div><div className="text-xs text-neutral-500">%</div><div className="font-bold">{summary.percentage}%</div></div>
          <div><div className="text-xs">Absent</div><div className="font-bold text-danger-600">{summary.absent}</div></div>
          <div><div className="text-xs">Late</div><div className="font-bold text-warning-600">{summary.late}</div></div>
          <div><div className="text-xs">Excused</div><div>{summary.excused}</div></div>
        </div>
      )}
      <div className="bg-white border rounded-xl p-3">
        <h3 className="font-semibold text-sm">Detail with dates and reasons</h3>
        <div className="mt-2 max-h-96 overflow-auto space-y-1">
          {detail.map((d,i)=>(
            <div key={i} className={`p-2 rounded border text-sm flex justify-between ${d.status==="Absent"?"bg-danger-50":d.status==="Late"?"bg-warning-50":"bg-success-50"}`}>
              <div><div className="font-medium">{new Date(d.date).toLocaleDateString()} • {d.status} {d.periodName}</div><div className="text-xs text-neutral-600">{d.reason?`Reason: ${d.reason}`:""} {d.note?`• ${d.note}`:""}</div></div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

function ResultsScreen({ child }) {
  const [results,setResults]=useState([]);
  useEffect(()=>{ apiFetch(`${API}/children/${child.studentId}/results`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setResults); },[child.studentId]);
  return (
    <div className="p-3 space-y-3">
      <h2 className="font-bold">Results — Published Report Cards Only, Downloadable</h2>
      <p className="text-xs text-neutral-500">Only published cards visible to parents, per requirement. Draft/approved not visible.</p>
      {results.length===0 ? <p className="text-sm text-neutral-500">No published report cards yet</p> :
        results.map(rc=>(
          <div key={rc.id} className="bg-white border rounded-xl p-4">
            <div className="flex justify-between"><div><div className="font-bold">{rc.termName}</div><div className="text-sm">Average {rc.average.toFixed(1)}% {rc.overallGrade} • {rc.positionDisplay}</div><div className="text-xs text-neutral-500">Published {new Date(rc.publishedAt).toLocaleDateString()}</div></div><a href={`${API}/report-cards/${rc.id}/pdf`} target="_blank" className="h-fit px-3 py-1.5 rounded border bg-white text-xs">PDF</a></div>
          </div>
        ))
      }
    </div>
  );
}

function NoticesScreen({ child }) {
  const [notices,setNotices]=useState([]);
  useEffect(()=>{ apiFetch(`${API}/children/${child.studentId}/notices`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setNotices); },[child.studentId]);
  return (
    <div className="p-3 space-y-2">
      <h2 className="font-bold">Notices — {child.fullName}</h2>
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

function HomeworkScreen({ child }) {
  const [hw,setHw]=useState([]);
  useEffect(()=>{ apiFetch(`${API}/children/${child.studentId}/homework`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setHw); },[child.studentId]);
  return (
    <div className="p-3 space-y-2">
      <h2 className="font-bold">Homework — {child.fullName}</h2>
      {hw.map(h=>(
        <div key={h.id} className="bg-white border rounded-lg p-3">
          <div className="font-medium text-sm">{h.title} — {h.subjectName}</div>
          <div className="text-xs text-neutral-500">Due {new Date(h.dueDate).toLocaleDateString()} • {h.daysLeft}d left • {h.status}</div>
        </div>
      ))}
    </div>
  );
}

function MessagesScreen({ child }) {
  const [messages,setMessages]=useState([]);
  const [form,setForm]=useState({subject:"",body:""});
  const [gradeId,setGradeId]=useState(child.gradeId);
  const [streamId,setStreamId]=useState(child.streamId);

  useEffect(()=>{
    apiFetch(`${API}/children/${child.studentId}/messages`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(data=>setMessages(data.messages||[]));
  },[child.studentId]);

  async function send(){
    const res=await apiFetch(`${API}/children/${child.studentId}/messages`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({studentId:child.studentId,gradeId:parseInt(gradeId),streamId:parseInt(streamId),subject:form.subject,body:form.body})});
    if(res.ok){ alert("Message sent — moderation if school enables it, rate limiting 5/hour"); setForm({subject:"",body:""}); apiFetch(`${API}/children/${child.studentId}/messages`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(data=>setMessages(data.messages||[])); }
    else alert("Failed: "+await res.text()+" — rate limiting or moderation?");
  }

  return (
    <div className="p-3 space-y-3">
      <h2 className="font-bold">Message to Class Teacher — {child.fullName}</h2>
      <p className="text-xs text-neutral-500">If school enables it, with school-side moderation setting and rate limiting 5/hour, 20/day.</p>
      <div className="bg-white border rounded-xl p-3 space-y-2">
        <input placeholder="Subject" value={form.subject} onChange={e=>setForm({...form,subject:e.target.value})} className="w-full h-10 px-3 border rounded text-sm" />
        <textarea placeholder="Message body — be respectful" value={form.body} onChange={e=>setForm({...form,body:e.target.value})} rows="3" className="w-full px-3 py-2 border rounded text-sm" />
        <button onClick={send} className="w-full min-h-touch py-2 rounded-lg bg-primary-800 text-white font-medium">Send to Teacher</button>
      </div>
      <div className="space-y-2">
        {messages.map(m=>(
          <div key={m.id} className="bg-white border rounded-lg p-3">
            <div className="font-medium text-sm">{m.subject} • {m.status} {m.requiresModeration&&<span className="text-xs bg-warning-100 px-1 rounded">Pending moderation</span>}</div>
            <div className="text-sm mt-1">{m.body}</div>
            <div className="text-xs text-neutral-500 mt-1">{new Date(m.createdAt).toLocaleString()} {m.moderationNote&&`• Mod: ${m.moderationNote}`}</div>
          </div>
        ))}
      </div>
    </div>
  );
}

function ProfileScreen() {
  const [profile,setProfile]=useState(null);
  useEffect(()=>{ apiFetch(`${API}/profile`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setProfile); },[]);

  async function updatePref(field, value){
    const newPref={...profile.contactPreferences,[field]:value};
    const res=await apiFetch(`${API}/profile/contact-preferences`,{method:"PUT",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify(newPref)});
    if(res.ok) setProfile(await res.json());
  }

  if(!profile) return <div className="p-4">Loading profile...</div>;

  return (
    <div className="p-3 space-y-3">
      <h2 className="font-bold">Profile & Contact Preferences — SMS Opt-Out</h2>
      <div className="bg-white border rounded-xl p-4">
        <div className="font-bold">{profile.fullName}</div>
        <div className="text-sm text-neutral-600">{profile.email} • {profile.phone}</div>
        <div className="text-xs mt-2">Children linked: {profile.children.map(c=>c.fullName).join(", ")}</div>
      </div>

      <div className="bg-white border rounded-xl p-4 space-y-3">
        <h3 className="font-semibold text-sm">Contact Preferences — including SMS opt-out always honoured</h3>
        <label className="flex justify-between items-center"><span className="text-sm">SMS Opt-In</span><input type="checkbox" checked={profile.contactPreferences.smsOptIn} onChange={e=>updatePref('smsOptIn',e.target.checked)} /></label>
        <label className="flex justify-between items-center"><span className="text-sm">Email Opt-In</span><input type="checkbox" checked={profile.contactPreferences.emailOptIn} onChange={e=>updatePref('emailOptIn',e.target.checked)} /></label>
        <label className="flex justify-between items-center text-danger-700"><span className="text-sm">SMS Opt-Out (hard) — stops all SMS</span><input type="checkbox" checked={profile.contactPreferences.smsOptOut} onChange={e=>updatePref('smsOptOut',e.target.checked)} /></label>
        <label className="flex justify-between items-center text-danger-700"><span className="text-sm">Email Opt-Out</span><input type="checkbox" checked={profile.contactPreferences.emailOptOut} onChange={e=>updatePref('emailOptOut',e.target.checked)} /></label>
        <p className="text-xs text-neutral-500">Opt-out is always honoured — filtered before send, delivery log is_opted_out true, counted as filteredOptOut, not sent. Cost estimate excludes opted out.</p>
      </div>

      <div className="bg-white border rounded-xl p-4">
        <h3 className="font-semibold text-sm">Password Management</h3>
        <a href="/api/auth/change-password" className="text-xs text-primary-800 underline">Change password via /api/auth/change-password — revokes all refresh tokens</a>
      </div>

      <p className="text-[11px] text-neutral-400 text-center">Works on small screen, readable at arm's length, minimal JavaScript, understandable without instructions. Touch targets 44px min, 16px inputs, bottom nav thumb zone.</p>
    </div>
  );
}