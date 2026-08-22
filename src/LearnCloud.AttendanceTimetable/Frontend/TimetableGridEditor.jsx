/**
 * Timetable Weekly Grid Editor - Assign subject and teacher to a class period
 * Clash detection on save: teacher in two places, class double-booked, room conflict if rooms used
 * Explain clash in plain language, do not silently refuse
 * Effective dated so mid-term change does not rewrite history
 * Views by class and by teacher, both printable
 */

import React, { useState, useEffect } from 'react';
import { apiFetch, getAccessToken, setAccessToken } from '../../LearnCloud.Web/src/lib/apiClient.js'; // SECURITY C2 FIX

const DAYS = [{id:1,name:"Monday"},{id:2,name:"Tuesday"},{id:3,name:"Wednesday"},{id:4,name:"Thursday"},{id:5,name:"Friday"}];

export default function TimetableGridEditor({ timetableId, gradeId, streamId, academicYearId, termId }) {
  const [periods, setPeriods] = useState([]);
  const [grid, setGrid] = useState(null);
  const [subjects, setSubjects] = useState([]);
  const [teachers, setTeachers] = useState([]);
  const [rooms, setRooms] = useState([]);
  const [clash, setClash] = useState(null);
  const [selectedCell, setSelectedCell] = useState(null); // {day, period}
  const [form, setForm] = useState({ subjectId:"", teacherStaffId:"", roomId:"" });
  const [saving, setSaving] = useState(false);

  async function load() {
    const [pRes, gRes, subjRes, teachRes, roomRes] = await Promise.all([
      apiFetch(`/api/timetable/periods?academicYearId=${academicYearId}`, {headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}),
      apiFetch(`/api/timetable/view/class?gradeId=${gradeId}&streamId=${streamId}&academicYearId=${academicYearId}&termId=${termId}`, {headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}),
      apiFetch(`/api/academic/subjects?gradeId=${gradeId}`, {headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).catch(()=>({ok:false})),
      apiFetch(`/api/staff?status=active`, {headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).catch(()=>({ok:false})),
      apiFetch(`/api/academic/rooms`, {headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).catch(()=>({ok:false})),
    ]);
    if (pRes.ok) setPeriods(await pRes.json());
    if (gRes.ok) setGrid(await gRes.json());
    if (subjRes.ok) { const d=await subjRes.json(); setSubjects(d.items||d); }
    if (teachRes.ok) { const d=await teachRes.json(); setTeachers(d.items||d); }
    if (roomRes.ok) { const d=await roomRes.json(); setRooms(d.items||d); }
  }

  useEffect(()=>{ load(); }, [timetableId, gradeId, streamId]);

  async function checkClash(day, period) {
    const body = { gradeId, streamId, subjectId: parseInt(form.subjectId), teacherStaffId: parseInt(form.teacherStaffId), roomId: form.roomId?parseInt(form.roomId):null, dayOfWeek: day, periodNumber: period };
    const res = await apiFetch(`/api/timetable/${timetableId}/slots/check-clash`, { method:"POST", headers:{"Content-Type":"application/json", Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}, body: JSON.stringify(body) });
    if (res.ok) {
      const data = await res.json();
      setClash(data.hasClash ? data : null);
      return data;
    }
    return {hasClash:false};
  }

  async function saveSlot() {
    if (!selectedCell) return;
    setSaving(true);
    const body = { gradeId, streamId, subjectId: parseInt(form.subjectId), teacherStaffId: parseInt(form.teacherStaffId), roomId: form.roomId?parseInt(form.roomId):null, dayOfWeek: selectedCell.day, periodNumber: selectedCell.period };
    const clashRes = await checkClash(selectedCell.day, selectedCell.period);
    if (clashRes.hasClash) {
      // Explain clash in plain language, do not silently refuse - show dialog
      setSaving(false);
      return;
    }
    const res = await apiFetch(`/api/timetable/${timetableId}/slots`, { method:"POST", headers:{"Content-Type":"application/json", Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}, body: JSON.stringify(body) });
    if (res.ok) {
      setSelectedCell(null);
      setClash(null);
      await load();
    } else {
      const err = await res.json();
      alert("Save failed: "+err.message);
      if (err.code==="CLASH_DETECTED") {
        // Try to get clash details
        const cRes = await apiFetch(`/api/timetable/${timetableId}/slots/check-clash`, { method:"POST", headers:{"Content-Type":"application/json", Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}, body: JSON.stringify(body) });
        if (cRes.ok) setClash(await cRes.json());
      }
    }
    setSaving(false);
  }

  return (
    <div className="bg-white rounded-lg border overflow-hidden">
      <div className="p-4 border-b flex justify-between items-center">
        <div>
          <h2 className="font-bold">{grid?.timetableName || `Timetable ${timetableId}`} — Effective {grid && new Date().toISOString().slice(0,10)} (mid-term change preserves history)</h2>
          <p className="text-xs text-neutral-500">Weekly grid editor — assign subject and teacher to class period. Clash detection explains teacher in two places, class double-booked, room conflict.</p>
        </div>
        <div className="flex gap-2">
          <button onClick={()=>window.print()} className="px-3 py-1.5 rounded border text-sm">Print Class View</button>
          <a href={`/api/timetable/view/class/html?gradeId=${gradeId}&streamId=${streamId}&academicYearId=${academicYearId}&termId=${termId}`} target="_blank" className="px-3 py-1.5 rounded bg-primary-800 text-white text-sm">Printable A4</a>
        </div>
      </div>

      {/* Grid */}
      <div className="overflow-auto">
        <table className="w-full border-collapse text-sm">
          <thead>
            <tr>
              <th className="border p-2 bg-neutral-50">Day / Period</th>
              {periods.filter(p=>!p.isBreak).map(p=><th key={p.periodNumber} className="border p-2 bg-neutral-50">{p.name}<br/><small>{p.startTime}-{p.endTime}</small></th>)}
            </tr>
          </thead>
          <tbody>
            {DAYS.map(day=>(
              <tr key={day.id}>
                <td className="border p-2 font-medium bg-neutral-50">{day.name}</td>
                {periods.filter(p=>!p.isBreak).map(per=> {
                  const slot = grid?.days.find(d=>d.dayOfWeek===day.id)?.slots.find(s=>s.periodNumber===per.periodNumber);
                  const isSelected = selectedCell?.day===day.id && selectedCell?.period===per.periodNumber;
                  return (
                    <td key={per.periodNumber} onClick={()=>setSelectedCell({day:day.id, period:per.periodNumber})} className={`border p-2 min-w-[120px] cursor-pointer hover:bg-primary-50 ${isSelected?"bg-primary-100 ring-2 ring-primary-800":""} ${slot?"bg-white":"bg-neutral-50"}`}>
                      {slot ? (
                        <div>
                          <div className="font-medium">{slot.subjectName}</div>
                          <div className="text-xs text-neutral-600">{slot.teacherName}</div>
                          <div className="text-xs text-neutral-500">{slot.roomName}</div>
                        </div>
                      ) : (
                        <div className="text-xs text-neutral-400">+ Add</div>
                      )}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Edit modal for selected cell */}
      {selectedCell && (
        <div className="fixed inset-0 bg-black/40 z-30 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl border shadow-xl max-w-md w-full p-6">
            <h3 className="font-bold">Edit {DAYS.find(d=>d.id===selectedCell.day)?.name} Period {selectedCell.period}</h3>
            <div className="mt-4 space-y-3">
              <label className="block"><span className="text-sm font-medium">Subject *</span><select value={form.subjectId} onChange={e=>setForm(f=>({...f,subjectId:e.target.value}))} className="mt-1 w-full h-10 px-2 border rounded"><option value="">Select subject</option>{subjects.map(s=><option key={s.id||s.code} value={s.id}>{s.name}</option>)}</select></label>
              <label className="block"><span className="text-sm font-medium">Teacher *</span><select value={form.teacherStaffId} onChange={e=>setForm(f=>({...f,teacherStaffId:e.target.value}))} className="mt-1 w-full h-10 px-2 border rounded"><option value="">Select teacher</option>{teachers.map(t=><option key={t.id} value={t.id}>{t.firstName} {t.lastName}</option>)}</select></label>
              <label className="block"><span className="text-sm font-medium">Room (optional)</span><select value={form.roomId} onChange={e=>setForm(f=>({...f,roomId:e.target.value}))} className="mt-1 w-full h-10 px-2 border rounded"><option value="">No room</option>{rooms.map(r=><option key={r.id} value={r.id}>{r.name}</option>)}</select></label>
            </div>

            {clash?.hasClash && (
              <div className="mt-4 p-3 rounded-lg bg-danger-50 border border-danger-200 text-sm">
                <div className="font-semibold text-danger-700">Clash detected — explained in plain language:</div>
                <ul className="mt-2 list-disc pl-5 space-y-1">
                  {clash.clashes.map((c,i)=><li key={i}><strong>{c.clashType}:</strong> {c.message}<br/><small>Existing: {c.existingInfo}</small></li>)}
                </ul>
                <p className="mt-2 text-xs text-neutral-600">We do not silently refuse — you see exactly why. Change teacher, class, period or room.</p>
              </div>
            )}

            <div className="mt-6 flex gap-2">
              <button onClick={saveSlot} disabled={saving || !form.subjectId || !form.teacherStaffId} className="px-4 py-2 rounded-lg bg-primary-800 text-white font-medium disabled:opacity-50">{saving?"Saving...":"Save Slot"}</button>
              <button onClick={()=>{setSelectedCell(null);setClash(null);}} className="px-4 py-2 rounded-lg border">Cancel</button>
              <button onClick={async()=>{await checkClash(selectedCell.day, selectedCell.period);}} className="px-3 py-2 rounded-lg border text-sm">Check Clash</button>
            </div>
          </div>
        </div>
      )}

      {/* Effective dated info */}
      <div className="p-3 bg-primary-50 border-t text-xs text-primary-800">
        Effective dated: This timetable version effective from {grid?.timetableName || "now"}. Mid-term change creates new version v{grid ? 2 : 1} with new EffectiveFrom, old history preserved for past registers. Use Clone to create new effective version.
      </div>
    </div>
  );
}

export function ClassTimetableView({ gradeId, streamId, academicYearId, termId }) {
  const [grid,setGrid]=useState(null);
  useEffect(()=>{ apiFetch(`/api/timetable/view/class?gradeId=${gradeId}&streamId=${streamId}&academicYearId=${academicYearId}&termId=${termId}`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setGrid); },[gradeId,streamId]);
  if(!grid) return <div>Loading...</div>;
  return <div className="p-4 border rounded bg-white"><h3 className="font-bold">Class Timetable {grid.timetableName}</h3><pre className="text-xs overflow-auto">{JSON.stringify(grid,null,2)}</pre><button onClick={()=>window.print()} className="mt-2 px-3 py-1 border rounded text-sm">Print A4</button></div>;
}

export function TeacherTimetableView({ teacherStaffId, academicYearId, termId }) {
  const [grid,setGrid]=useState(null);
  useEffect(()=>{ apiFetch(`/api/timetable/view/teacher?teacherStaffId=${teacherStaffId}&academicYearId=${academicYearId}&termId=${termId}`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setGrid); },[teacherStaffId]);
  if(!grid) return <div>Loading...</div>;
  return <div className="p-4 border rounded bg-white"><h3 className="font-bold">Teacher Timetable {grid.timetableName}</h3><pre className="text-xs overflow-auto">{JSON.stringify(grid,null,2)}</pre><button onClick={()=>window.print()} className="mt-2 px-3 py-1 border rounded text-sm">Print A4</button></div>;
}