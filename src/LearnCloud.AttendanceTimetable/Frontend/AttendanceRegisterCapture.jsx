/**
 * Attendance Register Capture - Optimized for Speed + A11Y + Colorblind
 * Requirements:
 * - Whole class listed, one tap per learner to cycle present, absent, late, excused
 * - Mark all present action
 * - Autosave every few seconds and clear indicator unsaved changes
 * - Usable one-handed on phone (44-48px targets, thumb zone)
 * - Absence reason and optional note
 * - Guard against duplicate registers for same class, date, period
 * - Backdating flagged
 * 
 * A11Y + Colorblind Fixes (P0):
 * - Color + icon + letter + full text beyond color-only
 * - aria-label with student name and status
 * - Focus-visible ring
 * - Live regions for autosave
 * - Keyboard navigation Arrow keys between students
 * - Border/pattern for colorblind (left thick border + icon shape distinct)
 * - 44px touch targets kept
 */

import React, { useState, useEffect, useRef, useCallback, useMemo, memo } from 'react';
import { apiFetch, getAccessToken } from '../../LearnCloud.Web/src/lib/apiClient.js';

const STATUS_CYCLE = ["present", "absent", "late", "excused", "sick"];

// A11Y + Colorblind: Each status has color + icon + letter + full label + pattern
const STATUS_CONFIG = {
  present: {
    color: "bg-success-50 border-success-200 text-success-700",
    solid: "bg-success-500 text-white",
    letter: "P",
    label: "Present",
    icon: (props) => <svg {...props} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" aria-hidden="true"><path d="M20 6L9 17l-5-5"/></svg>,
    borderLeft: "border-l-4 border-l-success-500",
    pattern: "" // solid
  },
  absent: {
    color: "bg-danger-50 border-danger-200 text-danger-700",
    solid: "bg-danger-500 text-white",
    letter: "A",
    label: "Absent",
    icon: (props) => <svg {...props} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" aria-hidden="true"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>,
    borderLeft: "border-l-4 border-l-danger-500",
    pattern: "bg-[repeating-linear-gradient(45deg,transparent,transparent_4px,rgba(198,40,40,0.1)_4px,rgba(198,40,40,0.1)_8px)]" // diagonal stripes for colorblind
  },
  late: {
    color: "bg-warning-50 border-warning-200 text-warning-700",
    solid: "bg-warning-500 text-white",
    letter: "L",
    label: "Late",
    icon: (props) => <svg {...props} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><circle cx="12" cy="12" r="10"/><polyline points="12 6 12 12 16 14"/></svg>,
    borderLeft: "border-l-4 border-l-warning-500",
    pattern: ""
  },
  excused: {
    color: "bg-secondary-50 border-secondary-200 text-secondary-700",
    solid: "bg-secondary-500 text-white",
    letter: "E",
    label: "Excused",
    icon: (props) => <svg {...props} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><circle cx="12" cy="12" r="10"/><line x1="12" y1="16" x2="12" y2="12"/><line x1="12" y1="8" x2="12.01" y2="8"/></svg>,
    borderLeft: "border-l-4 border-l-secondary-500",
    pattern: "border-dashed"
  },
  sick: {
    color: "bg-neutral-100 border-neutral-300 text-neutral-700",
    solid: "bg-neutral-500 text-white",
    letter: "S",
    label: "Sick",
    icon: (props) => <svg {...props} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/></svg>,
    borderLeft: "border-l-4 border-l-neutral-400",
    pattern: ""
  },
  unmarked: {
    color: "bg-white border-neutral-200 text-neutral-500",
    solid: "bg-neutral-100 text-neutral-600 border border-neutral-200",
    letter: "–",
    label: "Unmarked",
    icon: (props) => <svg {...props} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true"><circle cx="12" cy="12" r="10"/><line x1="8" y1="12" x2="16" y2="12"/></svg>,
    borderLeft: "border-l-4 border-l-neutral-200",
    pattern: ""
  }
};

// Memoized student row for performance (avoid re-render all 40 when one changes)
const StudentRow = memo(function StudentRow({ student, record, onCycleStatus, onUpdateRecord, focused, onFocus }) {
  const status = record?.status || "unmarked";
  const config = STATUS_CONFIG[status] || STATUS_CONFIG.unmarked;
  const nextStatus = STATUS_CYCLE[(STATUS_CYCLE.indexOf(status) + 1) % STATUS_CYCLE.length];
  const nextConfig = STATUS_CONFIG[nextStatus];

  return (
    <div 
      className={`bg-white border rounded-xl p-2.5 flex items-center gap-3 min-h-[64px] transition-all focus-within:ring-2 focus-within:ring-secondary-500/20 focus-within:border-secondary-300 ${config.borderLeft} ${focused ? 'ring-2 ring-primary-500' : ''}`}
      tabIndex={-1}
      onFocus={onFocus}
    >
      <div className="w-9 h-9 rounded-full bg-primary-50 text-primary-800 grid place-items-center text-xs font-bold shrink-0" aria-hidden="true">
        {student.firstName[0]}{student.lastName[0]}
      </div>
      
      <div className="flex-1 min-w-0">
        <div className="flex items-baseline gap-2">
          <div className="font-medium text-[14px] truncate text-neutral-900">{student.firstName} {student.lastName}</div>
          <div className="text-[11px] text-neutral-500 font-mono">{student.studentNumber}</div>
          {/* Status badge for colorblind - always visible text */}
          <span className={`hidden sm:inline-flex text-[10px] px-1.5 py-0.5 rounded-full font-medium border ${config.color}`}>
            {config.icon({ className: "w-3 h-3 inline mr-1" })} {config.label}
          </span>
        </div>
        
        {(status === "absent" || status === "sick" || status === "excused" || status === "late") && (
          <div className="flex gap-1.5 mt-1.5">
            <label className="sr-only" htmlFor={`reason-${student.studentId}`}>Absence reason for {student.firstName} {student.lastName}</label>
            <input
              id={`reason-${student.studentId}`}
              placeholder="Reason (sick, family...)"
              value={record?.absenceReason || ""}
              onChange={e => onUpdateRecord(student.studentId, { ...record, absenceReason: e.target.value })}
              className="h-8 px-2.5 rounded-lg border border-neutral-200 bg-neutral-50 text-[13px] w-28 focus:bg-white focus:border-secondary-400 focus:ring-2 focus:ring-secondary-500/20 focus:outline-none"
            />
            <label className="sr-only" htmlFor={`note-${student.studentId}`}>Note for {student.firstName}</label>
            <input
              id={`note-${student.studentId}`}
              placeholder="Note"
              value={record?.note || ""}
              onChange={e => onUpdateRecord(student.studentId, { ...record, note: e.target.value })}
              className="h-8 px-2.5 rounded-lg border border-neutral-200 bg-neutral-50 text-[13px] flex-1 focus:bg-white focus:border-secondary-400 focus:ring-2 focus:ring-secondary-500/20 focus:outline-none"
            />
          </div>
        )}
      </div>

      {/* A11Y + Colorblind: Icon + Letter + Full Text, not just color */}
      <button
        onClick={() => onCycleStatus(student.studentId)}
        aria-label={`Mark ${student.firstName} ${student.lastName} as ${nextConfig.label}, currently ${config.label}. Press to cycle status. ${student.studentNumber}`}
        aria-pressed={status !== "unmarked"}
        title={`Current: ${config.label}. Next: ${nextConfig.label}. Click to cycle.`}
        className={`
          min-w-touch min-h-touch w-[88px] h-[56px] rounded-xl font-medium text-[12px] 
          flex flex-col items-center justify-center gap-0.5
          border-2 active:scale-95 transition-all duration-150
          focus:outline-none focus-visible:ring-4 focus-visible:ring-secondary-500/30 focus-visible:border-secondary-500
          ${config.solid} ${config.pattern}
          hover:shadow-md
        `}
      >
        <span className="flex items-center gap-1">
          <config.icon className="w-4 h-4" />
          <span className="font-bold text-[14px]">{config.letter}</span>
        </span>
        <span className="text-[10px] leading-none font-semibold tracking-wide">{config.label}</span>
        <span className="text-[9px] opacity-80 hidden sm:block">→ {nextConfig.label}</span>
      </button>
    </div>
  );
});

export default function AttendanceRegisterCapture({ gradeId, streamId, academicYearId, termId, initialDate, periodNumber, tenantMode }) {
  const [date, setDate] = useState(initialDate || new Date().toISOString().slice(0,10));
  const [period, setPeriod] = useState(periodNumber || null);
  const [students, setStudents] = useState([]);
  const [records, setRecords] = useState({});
  const [header, setHeader] = useState(null);
  const [saving, setSaving] = useState(false);
  const [unsaved, setUnsaved] = useState(false);
  const [lastSaved, setLastSaved] = useState(null);
  const [isBackdated, setIsBackdated] = useState(false);
  const [filter, setFilter] = useState("all");
  const [focusedStudentId, setFocusedStudentId] = useState(null);

  const autoSaveRef = useRef(null);
  const hasMounted = useRef(false);
  const listRef = useRef(null);

  async function loadRegister() {
    const params = new URLSearchParams({ gradeId, streamId, attendanceDate: date, academicYearId, termId, ...(period?{periodNumber:period}:{}) });
    const res = await apiFetch(`/api/attendance/register?${params}`, { headers: { Authorization: `Bearer ${getAccessToken()}` } });
    if (res.ok) {
      const data = await res.json();
      setStudents(data.students);
      const map = {};
      data.records.forEach(r=>{ map[r.studentId] = { status: r.status, absenceReason: r.absenceReason, note: r.note }; });
      setRecords(map);
      setHeader(data.header);
      setIsBackdated(data.header?.isBackdated || false);
      setUnsaved(false);
    }
  }

  useEffect(()=>{ loadRegister(); }, [date, period, gradeId, streamId]);

  useEffect(()=>{
    if (!hasMounted.current) { hasMounted.current = true; return; }
    setUnsaved(true);
    if (autoSaveRef.current) clearTimeout(autoSaveRef.current);
    autoSaveRef.current = setTimeout(()=>{ saveRegister(); }, 5000);
    return ()=> clearTimeout(autoSaveRef.current);
  }, [records]);

  const cycleStatus = useCallback((studentId) => {
    const current = records[studentId]?.status || "unmarked";
    const idx = STATUS_CYCLE.indexOf(current);
    const next = STATUS_CYCLE[(idx+1)%STATUS_CYCLE.length];
    setRecords(prev=>({ ...prev, [studentId]: { status: next, absenceReason: next==="present"?"":prev[studentId]?.absenceReason||"", note: prev[studentId]?.note||"" } }));
  }, [records]);

  const updateRecord = useCallback((studentId, newData) => {
    setRecords(prev => ({ ...prev, [studentId]: newData }));
  }, []);

  const markAllPresent = useCallback(() => {
    const newRecords = {};
    students.forEach(s=>{ newRecords[s.studentId] = { status: "present", absenceReason: "", note: "" }; });
    setRecords(newRecords);
  }, [students]);

  async function saveRegister() {
    if (!Object.keys(records).length) return;
    setSaving(true);
    const items = Object.entries(records).map(([studentId, v])=>({ studentId: parseInt(studentId), status: v.status, absenceReason: v.absenceReason, note: v.note }));
    const body = { gradeId, streamId, attendanceDate: date, periodNumber: period, academicYearId, termId, items, backdateReason: isBackdated ? "Backdated via register" : null };
    const res = await apiFetch(`/api/attendance/mark`, { method:"POST", headers: { "Content-Type":"application/json", Authorization: `Bearer ${getAccessToken()}` }, body: JSON.stringify(body) });
    if (res.ok) {
      const result = await res.json();
      setLastSaved(new Date());
      setUnsaved(false);
      setIsBackdated(result.isBackdated);
    } else {
      const err = await res.json().catch(()=>({message:"Save failed"}));
      // Use accessible alert dialog instead of alert()
      const announcement = document.getElementById('a11y-announcement');
      if (announcement) announcement.textContent = `Save failed: ${err.message}`;
    }
    setSaving(false);
  }

  // Keyboard navigation Arrow keys between students
  const handleKeyDown = useCallback((e) => {
    if (e.key === "ArrowDown" || e.key === "ArrowUp") {
      e.preventDefault();
      const currentIndex = students.findIndex(s => s.studentId === focusedStudentId);
      let nextIndex = currentIndex;
      if (e.key === "ArrowDown") nextIndex = Math.min(students.length - 1, currentIndex + 1);
      if (e.key === "ArrowUp") nextIndex = Math.max(0, currentIndex - 1);
      if (nextIndex !== currentIndex && students[nextIndex]) {
        setFocusedStudentId(students[nextIndex].studentId);
        const el = document.querySelector(`[data-student-id="${students[nextIndex].studentId}"]`);
        el?.focus();
      }
    }
    if (e.key === " " || e.key === "Enter") {
      e.preventDefault();
      if (focusedStudentId) cycleStatus(focusedStudentId);
    }
  }, [students, focusedStudentId, cycleStatus]);

  const filteredStudents = useMemo(() => students.filter(s=>{
    if (filter==="unmarked") return !records[s.studentId] || records[s.studentId].status==="unmarked";
    if (filter==="absent") return records[s.studentId]?.status==="absent";
    return true;
  }), [students, records, filter]);

  const markedCount = useMemo(() => Object.keys(records).filter(id=>records[id].status!=="unmarked").length, [records]);

  const statusSummary = useMemo(() => {
    const counts = { present:0, absent:0, late:0, excused:0, sick:0, unmarked:0 };
    Object.values(records).forEach(r => { counts[r.status] = (counts[r.status]||0)+1; });
    counts.unmarked = students.length - Object.keys(records).filter(id=>records[id].status!=="unmarked").length;
    return counts;
  }, [records, students.length]);

  return (
    <div className="min-h-screen bg-neutral-50 pb-24" onKeyDown={handleKeyDown}>
      {/* A11Y Live regions */}
      <div id="a11y-announcement" aria-live="polite" aria-atomic="true" className="sr-only"></div>
      <div aria-live="assertive" aria-atomic="true" className="sr-only">
        {unsaved ? "Unsaved changes" : lastSaved ? `Saved at ${lastSaved.toLocaleTimeString()}` : ""}
      </div>

      {/* Header sticky with landmarks */}
      <header className="sticky top-0 bg-white border-b border-neutral-200 z-10 p-3" role="banner">
        <div className="flex items-center justify-between gap-2">
          <div>
            <h1 className="font-bold text-[16px] tracking-tight">{header?.gradeName} {header?.streamName} — {date} {period?`P${period}`:"Daily"}</h1>
            <div className="flex items-center gap-2 mt-1">
              <div className="text-xs text-neutral-600" aria-live="polite">{markedCount}/{students.length} marked</div>
              {isBackdated && <span className="px-2 py-0.5 rounded-full bg-warning-50 border border-warning-200 text-warning-700 text-[11px] font-medium" role="alert">Backdated — flagged in audit</span>}
              {header?.status==="submitted" && <span className="px-2 py-0.5 rounded-full bg-success-50 border border-success-200 text-success-700 text-[11px]">Submitted</span>}
            </div>
          </div>
          <div className="flex gap-2">
            <button onClick={markAllPresent} className="min-h-touch px-3 py-2 rounded-xl border border-neutral-200 bg-white text-[13px] font-medium hover:bg-neutral-50 focus:outline-none focus-visible:ring-4 focus-visible:ring-secondary-500/20">Mark All Present</button>
            <button onClick={saveRegister} disabled={saving || !unsaved} aria-busy={saving} className={`min-h-touch px-4 py-2 rounded-xl font-semibold text-[13px] focus:outline-none focus-visible:ring-4 ${unsaved?"bg-primary-800 text-white hover:bg-primary-900 focus-visible:ring-primary-500/30":"bg-neutral-200 text-neutral-500"}`}>{saving?"Saving...":unsaved?"Save":"Saved"}</button>
          </div>
        </div>

        {/* Status summary for colorblind - text summary beyond colors */}
        <div className="mt-3 flex flex-wrap gap-2" role="status" aria-label="Attendance summary">
          {Object.entries(statusSummary).map(([status, count]) => {
            if (count===0) return null;
            const cfg = STATUS_CONFIG[status];
            return (
              <span key={status} className={`inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-[11px] font-medium border ${cfg.color}`}>
                <cfg.icon className="w-3 h-3" />
                {count} {cfg.label}
              </span>
            );
          })}
        </div>

        <div className="mt-3 flex gap-2 items-center flex-wrap">
          <label className="sr-only" htmlFor="attendance-date">Attendance date</label>
          <input id="attendance-date" type="date" value={date} onChange={e=>setDate(e.target.value)} className="h-10 px-3 rounded-xl border border-neutral-200 bg-white text-[13px] focus:outline-none focus:border-secondary-400 focus:ring-4 focus:ring-secondary-500/20" />
          {tenantMode==="per_period" && (
            <>
              <label className="sr-only" htmlFor="period-select">Period</label>
              <select id="period-select" value={period||""} onChange={e=>setPeriod(e.target.value?parseInt(e.target.value):null)} className="h-10 px-3 rounded-xl border border-neutral-200 bg-white text-[13px] focus:outline-none focus:ring-4 focus:ring-secondary-500/20">
                <option value="">Daily</option>
                {[1,2,3,4,5,6,7,8].map(p=><option key={p} value={p}>Period {p}</option>)}
              </select>
            </>
          )}
          <label className="sr-only" htmlFor="filter-select">Filter students</label>
          <select id="filter-select" value={filter} onChange={e=>setFilter(e.target.value)} className="h-10 px-3 rounded-xl border border-neutral-200 bg-white text-[13px] focus:outline-none focus:ring-4 focus:ring-secondary-500/20">
            <option value="all">All learners</option>
            <option value="unmarked">Unmarked only</option>
            <option value="absent">Absent only</option>
          </select>
          <span className="text-[11px] text-neutral-500 ml-auto hidden sm:block">Tip: Use ↑↓ to navigate, Space/Enter to cycle status</span>
        </div>

        {/* Autosave status with live region */}
        <div className="mt-2 min-h-[20px]">
          {unsaved && <div className="text-xs text-warning-700 flex items-center gap-1.5" role="status"><span className="w-2 h-2 rounded-full bg-warning-500 animate-pulse" aria-hidden="true"></span> Unsaved changes — autosave in 5s — {markedCount} of {students.length} marked</div>}
          {lastSaved && !unsaved && <div className="text-xs text-success-700 flex items-center gap-1" role="status"><span aria-hidden="true">✓</span> Saved at {lastSaved.toLocaleTimeString()} — {markedCount}/{students.length} learners marked</div>}
        </div>
      </header>

      {/* Legend for colorblind */}
      <div className="p-3 bg-white border-b border-neutral-100">
        <div className="text-[11px] font-semibold uppercase tracking-widest text-neutral-500 mb-2">Legend - Color + Icon + Text beyond color-only (colorblind safe)</div>
        <div className="flex flex-wrap gap-2">
          {Object.entries(STATUS_CONFIG).map(([key, cfg]) => (
            <span key={key} className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-[11px] font-medium border ${cfg.color} ${cfg.borderLeft}`}>
              <cfg.icon className="w-3.5 h-3.5" />
              <span className="font-bold">{cfg.letter}</span>
              <span>{cfg.label}</span>
            </span>
          ))}
        </div>
      </div>

      {/* List */}
      <main className="p-2 space-y-1.5" role="main" aria-label="Attendance register">
        <div className="sr-only" role="status" aria-live="polite">{filteredStudents.length} learners shown, {markedCount} marked</div>
        {filteredStudents.map(s=>{
          const rec = records[s.studentId];
          return (
            <div key={s.studentId} data-student-id={s.studentId} tabIndex={0} onFocus={()=>setFocusedStudentId(s.studentId)}>
              <StudentRow student={s} record={rec} onCycleStatus={cycleStatus} onUpdateRecord={updateRecord} focused={focusedStudentId===s.studentId} onFocus={()=>setFocusedStudentId(s.studentId)} />
            </div>
          );
        })}
        {filteredStudents.length===0 && (
          <div className="p-8 text-center bg-white rounded-xl border border-neutral-200">
            <div className="text-sm text-neutral-600">No learners match filter "{filter}"</div>
            <button onClick={()=>setFilter("all")} className="mt-2 text-sm text-secondary-600 underline">Show all learners</button>
          </div>
        )}
      </main>

      {/* Floating thumb zone */}
      <div className="fixed bottom-0 left-0 right-0 bg-white border-t border-neutral-200 p-3 flex gap-2 safe-bottom" role="toolbar" aria-label="Attendance actions">
        <button onClick={markAllPresent} className="flex-1 min-h-touch py-3 rounded-xl bg-primary-50 border border-primary-200 text-primary-800 font-medium text-[13px] hover:bg-primary-100 focus:outline-none focus-visible:ring-4 focus-visible:ring-primary-500/20">Mark All Present (P)</button>
        <button onClick={saveRegister} disabled={!unsaved} aria-busy={saving} className="flex-1 min-h-touch py-3 rounded-xl bg-primary-800 text-white font-semibold text-[13px] disabled:bg-neutral-300 disabled:text-neutral-500 focus:outline-none focus-visible:ring-4 focus-visible:ring-primary-500/30">Save {markedCount}/{students.length}</button>
      </div>
    </div>
  );
}
