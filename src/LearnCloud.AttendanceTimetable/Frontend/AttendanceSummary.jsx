/**
 * Attendance Summaries: per learner, per class, per term, with attendance percentage and configurable threshold that flags chronic absence
 */

import React, { useState, useEffect } from 'react';
import { apiFetch, getAccessToken, setAccessToken } from '../../LearnCloud.Web/src/lib/apiClient.js'; // SECURITY C2 FIX

export default function AttendanceSummary({ gradeId, streamId, academicYearId, termId }) {
  const [summary, setSummary] = useState(null);
  const [settings, setSettings] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(()=>{
    async function load(){
      setLoading(true);
      const [sumRes, setRes] = await Promise.all([
        apiFetch(`/api/attendance/summary?gradeId=${gradeId||""}&streamId=${streamId||""}&academicYearId=${academicYearId}&termId=${termId}`, {headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}),
        apiFetch(`/api/attendance/settings`, {headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}})
      ]);
      if(sumRes.ok) setSummary(await sumRes.json());
      if(setRes.ok) setSettings(await setRes.json());
      setLoading(false);
    }
    load();
  }, [gradeId, streamId, academicYearId, termId]);

  if(loading) return <div className="p-4">Loading summaries...</div>;
  if(!summary) return <div className="p-4">No data</div>;

  return (
    <div className="space-y-4">
      <div className="bg-white border rounded-lg p-4">
        <h2 className="font-bold">{summary.gradeName} {summary.streamName} — Term {termId} Summary</h2>
        <div className="grid grid-cols-3 gap-3 mt-3">
          <div className="p-3 rounded bg-primary-50 border"><div className="text-xs text-primary-700">Total Students</div><div className="text-xl font-bold">{summary.totalStudents}</div></div>
          <div className="p-3 rounded bg-warning-50 border"><div className="text-xs text-warning-700">Avg %</div><div className="text-xl font-bold">{summary.averagePercentage}%</div></div>
          <div className="p-3 rounded bg-danger-50 border"><div className="text-xs text-danger-700">Chronic Absence &lt;{settings?.chronicAbsenceThreshold||85}%</div><div className="text-xl font-bold">{summary.chronicAbsenceCount}</div></div>
        </div>
        <p className="text-xs text-neutral-500 mt-2">Threshold configurable per tenant: {settings?.chronicAbsenceThreshold}% — late counts as {settings?.countLateAsPresent?"present":"absent"}, excused as {settings?.countExcusedAsPresent?"present":"absent"}</p>
      </div>

      <div className="bg-white border rounded-lg overflow-hidden">
        <div className="p-3 border-b flex justify-between">
          <h3 className="font-semibold">Per Learner</h3>
          <button onClick={()=>window.print()} className="text-xs px-2 py-1 border rounded">Print A4</button>
        </div>
        <div className="overflow-auto max-h-[500px]">
          <table className="w-full text-sm border-collapse">
            <thead className="sticky top-0 bg-neutral-50">
              <tr><th className="border p-2 text-left">Student</th><th className="border p-2">Total</th><th className="border p-2">P</th><th className="border p-2">A</th><th className="border p-2">L</th><th className="border p-2">E</th><th className="border p-2">S</th><th className="border p-2">%</th><th className="border p-2">Flag</th></tr>
            </thead>
            <tbody>
              {summary.learners.map(l=>(
                <tr key={l.studentId} className={l.isChronicAbsence?"bg-danger-50":""}>
                  <td className="border p-2">{l.studentName} <span className="text-xs text-neutral-500">{l.studentNumber}</span></td>
                  <td className="border p-2 text-center">{l.totalDays}</td>
                  <td className="border p-2 text-center">{l.presentCount}</td>
                  <td className="border p-2 text-center">{l.absentCount}</td>
                  <td className="border p-2 text-center">{l.lateCount}</td>
                  <td className="border p-2 text-center">{l.excusedCount}</td>
                  <td className="border p-2 text-center">{l.sickCount}</td>
                  <td className="border p-2 text-center font-bold">{l.percentage}%</td>
                  <td className="border p-2">{l.isChronicAbsence ? <span className="px-1.5 py-0.5 rounded bg-danger-500 text-white text-xs">Chronic {l.chronicMessage}</span> : <span className="text-xs text-success-600">OK</span>}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

export function PrintableMonthRegister({ gradeId, streamId, academicYearId, termId, year, month }) {
  const [data,setData]=useState(null);
  useEffect(()=>{
    apiFetch(`/api/attendance/printable/month?gradeId=${gradeId}&streamId=${streamId}&year=${year}&month=${month}&academicYearId=${academicYearId}&termId=${termId}`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setData);
  },[gradeId,streamId,year,month]);

  if(!data) return <div>Loading printable...</div>;

  return (
    <div className="bg-white p-4">
      <div className="flex justify-between items-center mb-4 print:hidden">
        <h2 className="font-bold">{data.gradeName} {data.streamName} — {data.monthName} A4 Register</h2>
        <button onClick={()=>window.print()} className="px-3 py-1.5 rounded bg-primary-800 text-white text-sm">Print A4</button>
      </div>
      <div className="overflow-auto">
        <table className="w-full border-collapse text-[10px]">
          <thead><tr><th className="border p-1">Student</th>{data.dates.map(d=><th key={d} className="border p-1">{new Date(d).getDate()}</th>)}<th className="border p-1">%</th></tr></thead>
          <tbody>
            {data.rows.map(row=>(
              <tr key={row.studentId}>
                <td className="border p-1 font-medium">{row.studentName} ({row.studentNumber})</td>
                {data.dates.map(d=>{
                  const key=new Date(d).toISOString().slice(0,10);
                  const val=row.attendanceByDate[key]||"";
                  const color=val==="P"?"bg-success-100":val==="A"?"bg-danger-100":val==="L"?"bg-warning-100":"";
                  return <td key={key} className={`border p-1 text-center ${color}`}>{val}</td>;
                })}
                <td className="border p-1"></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="mt-2 text-xs">Key: P=Present A=Absent L=Late S=Sick E=Excused | Printed from LearnCloud | Greyscale legible with letters</p>
      <style>{`@media print { body{margin:0} table{width:100%} th,td{border:1px solid black!important} .print\\:hidden{display:none} }`}</style>
    </div>
  );
}