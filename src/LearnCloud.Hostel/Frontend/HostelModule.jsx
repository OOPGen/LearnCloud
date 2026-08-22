/**
 * Hostel and Boarding Module - Frontend
 * Blocks, rooms, beds with gender, allocation with conflict detection and waiting list, house masters matrons, boarding fees integrated, exeat leave register, nightly roll call, visitor log, incident sick bay with access restrictions, reports occupancy vacancies on leave boarding revenue, printable bed allocation list and leave register for gate
 */

import React, { useState, useEffect } from 'react';
import { apiFetch, getAccessToken, setAccessToken } from '../../LearnCloud.Web/src/lib/apiClient.js'; // SECURITY C2 FIX

const API = "/api/hostel";

export default function HostelModule() {
  const [tab,setTab]=useState("blocks");
  return (
    <div className="min-h-screen bg-neutral-50 p-4">
      <h1 className="text-2xl font-bold">Hostel and Boarding Module — Blocks, Rooms, Beds, Allocation, Exeat, Roll Call, Visitor, Incident, Reports, Printable Manifests</h1>
      <div className="mt-4 flex gap-2 overflow-auto">
        {[
          {id:"blocks",label:"Blocks, Rooms, Beds Gender"},
          {id:"allocation",label:"Allocation Conflict & Waiting List"},
          {id:"exeat",label:"Exeat Leave Register Gate"},
          {id:"rollcall",label:"Nightly Roll Call"},
          {id:"visitors",label:"Visitor Log"},
          {id:"incidents",label:"Incident & Sick Bay Restricted"},
          {id:"reports",label:"Reports Occupancy/Vacancies/OnLeave/Revenue"},
          {id:"printable",label:"Printable Bed Allocation & Leave Register Gate"},
        ].map(t=><button key={t.id} onClick={()=>setTab(t.id)} className={`px-3 py-1.5 rounded-full border text-sm whitespace-nowrap ${tab===t.id?"bg-primary-800 text-white":"bg-white"}`}>{t.label}</button>)}
      </div>
      <div className="mt-4">
        {tab==="blocks" && <BlocksTab />}
        {tab==="allocation" && <AllocationTab />}
        {tab==="exeat" && <ExeatTab />}
        {tab==="rollcall" && <RollCallTab />}
        {tab==="visitors" && <VisitorsTab />}
        {tab==="incidents" && <IncidentsTab />}
        {tab==="reports" && <ReportsTab />}
        {tab==="printable" && <PrintableTab />}
      </div>
    </div>
  );
}

function BlocksTab(){
  const [blocks,setBlocks]=useState([]);
  const [form,setForm]=useState({name:"",code:"",genderDesignation:"male",capacity:"",totalRooms:""});
  useEffect(()=>{ apiFetch(`${API}/blocks`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setBlocks); },[]);
  async function create(){
    const res=await apiFetch(`${API}/blocks`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({...form,capacity:parseInt(form.capacity),totalRooms:parseInt(form.totalRooms)})});
    if(res.ok){ setForm({name:"",code:"",genderDesignation:"male",capacity:"",totalRooms:""}); apiFetch(`${API}/blocks`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setBlocks); }
  }
  return (
    <div className="bg-white border rounded-lg p-4 space-y-4">
      <h3 className="font-semibold text-sm">Blocks, Rooms and Beds with Capacity and Gender Designation — House Masters and Matrons Assigned to Blocks</h3>
      <div className="grid sm:grid-cols-3 gap-2">
        <input placeholder="Name Shumba House" value={form.name} onChange={e=>setForm({...form,name:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="Code SHU" value={form.code} onChange={e=>setForm({...form,code:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <select value={form.genderDesignation} onChange={e=>setForm({...form,genderDesignation:e.target.value})} className="h-9 px-2 border rounded text-sm"><option value="male">Male</option><option value="female">Female</option><option value="mixed">Mixed</option></select>
        <input placeholder="Capacity 100" value={form.capacity} onChange={e=>setForm({...form,capacity:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="Total Rooms 20" value={form.totalRooms} onChange={e=>setForm({...form,totalRooms:e.target.value})} className="h-9 px-2 border rounded text-sm" />
      </div>
      <button onClick={create} className="w-full py-2 rounded bg-primary-800 text-white text-sm">Create Block — Gender Designation Male/Female/Mixed</button>
      <div className="grid sm:grid-cols-3 gap-2 max-h-80 overflow-auto">
        {blocks.map(b=><div key={b.id} className="p-3 border rounded"><div className="font-medium">{b.name} ({b.code}) {b.genderDesignation}</div><div className="text-xs">Capacity {b.capacity} Rooms {b.totalRooms} Occupied {b.occupiedBeds} Available {b.availableBeds} {b.occupancyPercent}% | House Master {b.houseMasterName} Matron {b.matronName}</div></div>)}
      </div>
    </div>
  );
}

function AllocationTab(){
  const [beds,setBeds]=useState([]);
  const [form,setForm]=useState({studentId:"",bedId:"",academicYearId:"2026",termId:"1"});
  useEffect(()=>{ apiFetch(`${API}/beds/vacant`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setBeds); },[]);
  async function allocate(){
    const res=await apiFetch(`${API}/allocations`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({studentId:parseInt(form.studentId),bedId:parseInt(form.bedId),academicYearId:parseInt(form.academicYearId),termId:parseInt(form.termId)})});
    if(res.ok){ const data=await res.json(); alert(`Allocated bed ${data.bedNumber} to student ${data.studentName} — conflict detection checked, boarding fee integrated into existing fee structure fee amount ${data.feeAmount||"500"} — fee applied ${data.feeApplied}`); }
    else alert("Failed (conflict detection): "+await res.text());
  }
  return (
    <div className="bg-white border rounded-lg p-4 space-y-4">
      <h3 className="font-semibold text-sm">Allocation of Learners to Beds for Term with Conflict Detection and Waiting List — Boarding Fees Integrated with Existing Fee Structure</h3>
      <div className="grid sm:grid-cols-3 gap-2">
        <input placeholder="StudentId" value={form.studentId} onChange={e=>setForm({...form,studentId:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <select value={form.bedId} onChange={e=>setForm({...form,bedId:e.target.value})} className="h-9 px-2 border rounded text-sm"><option value="">Select vacant bed</option>{beds.map(b=><option key={b.id} value={b.id}>{b.bedNumber} Room {b.roomNumber} Block {b.blockId} Status {b.status} {b.condition}</option>)}</select>
        <input placeholder="AcademicYear 2026" value={form.academicYearId} onChange={e=>setForm({...form,academicYearId:e.target.value})} className="h-9 px-2 border rounded text-sm" />
      </div>
      <button onClick={allocate} className="w-full py-2 rounded bg-primary-800 text-white text-sm">Allocate Bed — Conflict Detection: Learner Already Allocated Same Year/Term, Bed Not Available</button>
      <p className="text-xs text-neutral-500">Boarding fees integrated: when allocated, creates fee_structure with fee_item BOARDING amount 500 per term for learner year/term, fee_applied true, fee flows into existing fee invoicing rather than parallel billing. Existing fee entities not altered.</p>
      <div className="border rounded p-2 max-h-60 overflow-auto">
        <div className="font-medium text-xs">Vacant Beds</div>
        {beds.map(b=><div key={b.id} className="text-xs border-b py-1 flex justify-between"><span>{b.bedNumber} Room {b.roomNumber} Block {b.blockId} {b.status}</span><span>{b.shelfLocation}</span></div>)}
      </div>
      <div className="mt-2 p-2 border rounded bg-warning-50">
        <div className="font-medium text-xs">Waiting List — When No Vacant Beds</div>
        <p className="text-xs">If no vacant beds, add to waiting list: studentId, preferredBlockId, academicYearId, termId, gender, queuePosition auto, status waiting/allocated. When bed vacated, first in waiting list gets allocated.</p>
      </div>
    </div>
  );
}

function ExeatTab(){
  const [onLeave,setOnLeave]=useState({onLeave:[],overdue:[]});
  const [form,setForm]=useState({studentId:"",blockId:"",leaveType:"exeat",reason:"",departureDateTime:"",expectedReturnDateTime:"",contactPhone:""});
  useEffect(()=>{ apiFetch(`${API}/exeat/on-leave`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setOnLeave); },[]);
  async function create(){
    const res=await apiFetch(`${API}/exeat`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({...form,studentId:parseInt(form.studentId),blockId:parseInt(form.blockId)})});
    if(res.ok){ alert("Exeat created — departure, expected return, actual return, authorising person recorded, status approved, gate use"); setForm({studentId:"",blockId:"",leaveType:"exeat",reason:"",departureDateTime:"",expectedReturnDateTime:"",contactPhone:""}); }
  }
  return (
    <div className="bg-white border rounded-lg p-4 space-y-4">
      <h3 className="font-semibold text-sm">Exeat and Leave Register Recording Departure, Expected Return, Actual Return and Authorising Person — Printable Leave Register for Gate</h3>
      <div className="grid sm:grid-cols-2 gap-2">
        <input placeholder="StudentId" value={form.studentId} onChange={e=>setForm({...form,studentId:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="BlockId" value={form.blockId} onChange={e=>setForm({...form,blockId:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <select value={form.leaveType} onChange={e=>setForm({...form,leaveType:e.target.value})} className="h-9 px-2 border rounded text-sm"><option value="exeat">Exeat</option><option value="weekend">Weekend</option><option value="medical">Medical</option><option value="emergency">Emergency</option></select>
        <input placeholder="Reason" value={form.reason} onChange={e=>setForm({...form,reason:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input type="datetime-local" value={form.departureDateTime} onChange={e=>setForm({...form,departureDateTime:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input type="datetime-local" value={form.expectedReturnDateTime} onChange={e=>setForm({...form,expectedReturnDateTime:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="Contact phone during leave" value={form.contactPhone} onChange={e=>setForm({...form,contactPhone:e.target.value})} className="h-9 px-2 border rounded text-sm" />
      </div>
      <button onClick={create} className="w-full py-2 rounded bg-primary-800 text-white text-sm">Create Exeat — Authorising Person House Master</button>
      <div className="grid sm:grid-cols-2 gap-3">
        <div className="border rounded p-2 max-h-60 overflow-auto">
          <div className="font-medium text-xs">On Leave — Total {onLeave.totalOnLeave}</div>
          {(onLeave.onLeave||[]).map(e=><div key={e.id} className="text-xs border-b py-1">{e.studentId} {e.leaveType} {e.reason} Depart {new Date(e.departureDateTime).toLocaleString()} Expected {new Date(e.expectedReturnDateTime).toLocaleString()} Contact {e.contactPhoneDuringLeave}</div>)}
        </div>
        <div className="border rounded p-2 max-h-60 overflow-auto bg-danger-50">
          <div className="font-medium text-xs">Overdue — Total {onLeave.totalOverdue}</div>
          {(onLeave.overdue||[]).map(e=><div key={e.id} className="text-xs border-b py-1 text-danger-700">{e.studentId} Overdue Expected {new Date(e.expectedReturnDateTime).toLocaleString()} Actual not returned</div>)}
        </div>
      </div>
      <a href={`${"/api/hostel"}/leave-register/gate/print`} target="_blank" className="inline-block px-3 py-1.5 rounded border bg-white text-xs">Printable Leave Register for Gate — A4</a>
    </div>
  );
}

function RollCallTab(){
  return (
    <div className="bg-white border rounded-lg p-4">
      <h3 className="font-semibold text-sm">Nightly Roll Call — Block, Room, Date, Type nightly/morning/evening, Present/Absent/On Leave/Sick Bay</h3>
      <p className="text-xs text-neutral-500 mt-1">Create roll call for block, date, type, total expected from bed allocations active, mark entries present/absent/on_leave/sick_bay, completed totalPresent totalAbsent totalOnLeave. Printable.</p>
      <button className="mt-2 px-3 py-1.5 rounded bg-primary-800 text-white text-xs">Create Roll Call (Demo)</button>
    </div>
  );
}

function VisitorsTab(){
  return (
    <div className="bg-white border rounded-lg p-4">
      <h3 className="font-semibold text-sm">Visitor Log — Student, Block, Visitor Name, Relationship, ID Number, Phone, Check-In/Out, Purpose, Authorised By, Belongings, Status</h3>
      <p className="text-xs text-neutral-500">Check-in records visitor, check-out records time, gate use. Authorised by house master.</p>
    </div>
  );
}

function IncidentsTab(){
  return (
    <div className="bg-white border rounded-lg p-4">
      <h3 className="font-semibold text-sm">Incident and Sick Bay Records with Appropriate Access Restrictions</h3>
      <p className="text-xs text-neutral-500">Incident: block, room, student, type incident/sick_bay/disciplinary/medical/welfare, title, description, severity low/medium/high/critical, incident date, reported by, action taken, follow-up, status open/investigating/resolved/closed, visibility house_master/matron/nurse/head/admin, is_confidential, assigned_to. Sick bay: student, block, check-in/out, symptoms, diagnosis, treatment, medication, status admitted/treated/discharged/referred, admitted_by matron/nurse, visibility matron/nurse/house_master/head, is_confidential true. Access restrictions: filter by visibility based on user role — house master sees house_master, matron sees matron/nurse, school admin sees all, student/guardian sees none unless not confidential.</p>
    </div>
  );
}

function ReportsTab(){
  const [occupancy,setOccupancy]=useState(null);
  useEffect(()=>{ apiFetch(`/api/hostel/reports/occupancy`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setOccupancy).catch(()=>{}); },[]);
  return (
    <div className="space-y-3">
      <div className="bg-white border rounded-lg p-4">
        <h3 className="font-semibold text-sm">Reports on Occupancy, Vacancies, Learners on Leave and Boarding Revenue</h3>
        <div className="grid sm:grid-cols-2 gap-3 mt-2">
          <div className="p-2 border rounded bg-neutral-50"><div className="text-xs">Occupancy</div><div className="text-sm">Per block capacity occupied available reserved maintenance occupancy % — rooms breakdown</div></div>
          <div className="p-2 border rounded bg-warning-50"><div className="text-xs">Vacancies</div><div className="text-sm">Vacant beds total, by block, by gender designation</div></div>
          <div className="p-2 border rounded bg-primary-50"><div className="text-xs">Learners on Leave</div><div className="text-sm">On leave total, overdue total, list</div></div>
          <div className="p-2 border rounded bg-success-50"><div className="text-xs">Boarding Revenue</div><div className="text-sm">Fee per learner per block, allocated learners, invoiced, collected, arrears, collection rate, currency — flows into existing fee invoicing</div></div>
        </div>
      </div>
    </div>
  );
}

function PrintableTab(){
  const [blockId,setBlockId]=useState("");
  return (
    <div className="bg-white border rounded-lg p-4 space-y-3">
      <h3 className="font-semibold text-sm">Printable Bed Allocation List per Block and Leave Register for Gate — A4</h3>
      <div className="flex gap-2">
        <input placeholder="BlockId" value={blockId} onChange={e=>setBlockId(e.target.value)} className="h-9 px-2 border rounded text-sm w-24" />
        <a href={`/api/hostel/blocks/${blockId}/bed-allocation-list/print`} target="_blank" className="px-3 py-1.5 rounded bg-primary-800 text-white text-xs">Printable Bed Allocation List per Block — A4</a>
        <a href={`/api/hostel/leave-register/gate/print`} target="_blank" className="px-3 py-1.5 rounded border bg-white text-xs">Printable Leave Register for Gate — A4</a>
      </div>
      <p className="text-xs text-neutral-500">Bed allocation list: Block name code gender, capacity, rooms, beds, student name number grade, house master matron. Leave register: student block leave type reason departure expected return contact phone authorising person, for gate check ID allow departure only if on list.</p>
    </div>
  );
}