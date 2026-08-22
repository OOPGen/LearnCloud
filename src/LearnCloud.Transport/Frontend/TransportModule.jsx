/**
 * Transport Module - Frontend
 * Routes with ordered stops and expected times, vehicles capacity registration insurance licence expiry with reminders, drivers assistants licence expiry tracking, learner assignment capacity enforcement, transport fees flow into existing fee structure, boarding attendance per trip, route change and absence notifications using existing messaging module, reports utilisation per route revenue per route unassigned learners, printable route manifest for each driver
 */

import React, { useState, useEffect } from 'react';
import { apiFetch, getAccessToken, setAccessToken } from '../../LearnCloud.Web/src/lib/apiClient.js'; // SECURITY C2 FIX

const API = "/api/transport";

export default function TransportModule() {
  const [tab, setTab] = useState("routes");
  return (
    <div className="min-h-screen bg-neutral-50 p-4">
      <h1 className="text-2xl font-bold">Transport Module — Routes, Vehicles, Drivers, Learner Assignment, Fees Flow into Existing Invoicing, Boarding Attendance, Notifications, Reports, Manifest</h1>
      <div className="mt-4 flex gap-2 overflow-auto">
        {[
          {id:"routes",label:"Routes & Stops"},
          {id:"vehicles",label:"Vehicles Expiry Reminders"},
          {id:"drivers",label:"Drivers Licence Expiry"},
          {id:"assignments",label:"Learner Assignment Capacity"},
          {id:"attendance",label:"Boarding Attendance Per Trip"},
          {id:"reports",label:"Reports Utilisation/Revenue/Unassigned"},
          {id:"manifest",label:"Printable Manifest Driver"},
        ].map(t => <button key={t.id} onClick={()=>setTab(t.id)} className={`px-3 py-1.5 rounded-full border text-sm whitespace-nowrap ${tab===t.id?"bg-primary-800 text-white":"bg-white"}`}>{t.label}</button>)}
      </div>
      <div className="mt-4">
        {tab==="routes" && <RoutesTab />}
        {tab==="vehicles" && <VehiclesTab />}
        {tab==="drivers" && <DriversTab />}
        {tab==="assignments" && <AssignmentsTab />}
        {tab==="attendance" && <BoardingAttendanceTab />}
        {tab==="reports" && <ReportsTab />}
        {tab==="manifest" && <ManifestTab />}
      </div>
    </div>
  );
}

function RoutesTab() {
  const [routes,setRoutes]=useState([]);
  const [form,setForm]=useState({name:"",code:"",description:"",direction:"both",totalDistanceKm:"",estimatedDurationMinutes:"",feeAmount:"",currency:"USD"});
  const [stops,setStops]=useState([]);
  const [selectedRoute,setSelectedRoute]=useState(null);

  useEffect(()=>{ apiFetch(`${API}/routes`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setRoutes); },[]);

  async function createRoute(){
    const res=await apiFetch(`${API}/routes`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({...form,totalDistanceKm:parseFloat(form.totalDistanceKm),estimatedDurationMinutes:parseInt(form.estimatedDurationMinutes),feeAmount:parseFloat(form.feeAmount)})});
    if(res.ok){ const r=await res.json(); setRoutes([...routes,r]); setForm({name:"",code:"",description:"",direction:"both",totalDistanceKm:"",estimatedDurationMinutes:"",feeAmount:"",currency:"USD"}); }
  }

  async function loadStops(routeId){
    setSelectedRoute(routeId);
    const res=await apiFetch(`${API}/routes/${routeId}/stops`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}});
    if(res.ok) setStops(await res.json());
  }

  return (
    <div className="grid lg:grid-cols-2 gap-4">
      <div className="bg-white border rounded-lg p-4">
        <h3 className="font-semibold text-sm">Create Route — Ordered Stops and Expected Times, Fee Flows into Existing Invoicing</h3>
        <div className="mt-2 grid sm:grid-cols-2 gap-2">
          <input placeholder="Name Hillside Route A" value={form.name} onChange={e=>setForm({...form,name:e.target.value})} className="h-9 px-2 border rounded text-sm" />
          <input placeholder="Code RTE-A" value={form.code} onChange={e=>setForm({...form,code:e.target.value})} className="h-9 px-2 border rounded text-sm" />
          <input placeholder="Description" value={form.description} onChange={e=>setForm({...form,description:e.target.value})} className="h-9 px-2 border rounded text-sm col-span-2" />
          <select value={form.direction} onChange={e=>setForm({...form,direction:e.target.value})} className="h-9 px-2 border rounded text-sm"><option value="both">Both morning/evening</option><option value="morning">Morning only</option><option value="evening">Evening only</option></select>
          <input placeholder="Total distance km" value={form.totalDistanceKm} onChange={e=>setForm({...form,totalDistanceKm:e.target.value})} className="h-9 px-2 border rounded text-sm" />
          <input placeholder="Duration minutes" value={form.estimatedDurationMinutes} onChange={e=>setForm({...form,estimatedDurationMinutes:e.target.value})} className="h-9 px-2 border rounded text-sm" />
          <input placeholder="Fee amount per term" value={form.feeAmount} onChange={e=>setForm({...form,feeAmount:e.target.value})} className="h-9 px-2 border rounded text-sm" />
          <select value={form.currency} onChange={e=>setForm({...form,currency:e.target.value})} className="h-9 px-2 border rounded text-sm"><option>USD</option><option>ZWG</option></select>
        </div>
        <button onClick={createRoute} className="mt-3 w-full py-2 rounded bg-primary-800 text-white text-sm">Create Route — Fee Flows into Existing Fee Structure</button>

        <div className="mt-4 max-h-80 overflow-auto border rounded">
          <table className="w-full text-xs border-collapse"><thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1">Route</th><th className="border p-1">Code</th><th className="border p-1">Fee</th><th className="border p-1">Assigned/Capacity</th><th className="border p-1">Utilisation</th></tr></thead>
          <tbody>{routes.map(r=><tr key={r.id} onClick={()=>loadStops(r.id)} className="cursor-pointer hover:bg-primary-50"><td className="border p-1">{r.name}</td><td className="border p-1">{r.code}</td><td className="border p-1">{r.feeAmount} {r.currency}</td><td className="border p-1">{r.assignedLearners}/{r.capacity}</td><td className="border p-1">{r.utilisationPercent}%</td></tr>)}</tbody></table>
        </div>
      </div>

      <div className="bg-white border rounded-lg p-4">
        <h3 className="font-semibold text-sm">Stops — Ordered with Expected Times — Route {selectedRoute||"Select route"}</h3>
        <div className="mt-2 space-y-1 max-h-96 overflow-auto">
          {stops.map(s=><div key={s.id} className="p-2 border rounded text-xs flex justify-between"><div><div className="font-medium">{s.orderNumber}. {s.name}</div><div className="text-neutral-500">{s.address||""} • Arrival {s.expectedArrivalTime||""} • {s.distanceFromStartKm}km</div></div><div className="text-[10px]">Order {s.orderNumber}</div></div>)}
          {stops.length===0 && <div className="text-xs text-neutral-500">No stops — add stops in order, expected times, distance from start. Reorder via drag — will notify guardians via existing messaging module route change notification.</div>}
        </div>
        <button onClick={async()=>{
          const name=prompt("Stop name e.g. Hillside Shops");
          if(!name) return;
          const res=await apiFetch(`${API}/routes/${selectedRoute}/stops`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({name,address:"",orderNumber:stops.length+1,expectedArrivalTime:"06:30",expectedDepartureTime:"06:32",distanceFromStartKm:0,estimatedMinutesFromStart:0})});
          if(res.ok) loadStops(selectedRoute);
        }} className="mt-2 w-full py-2 rounded border text-sm">Add Stop — Ordered</button>
      </div>
    </div>
  );
}

function VehiclesTab() {
  const [vehicles,setVehicles]=useState([]);
  const [form,setForm]=useState({registrationNumber:"",make:"",model:"",capacity:"",year:"",insuranceExpiry:"",licenceExpiry:""});
  useEffect(()=>{ apiFetch(`${API}/vehicles`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setVehicles); },[]);
  async function create(){
    const res=await apiFetch(`${API}/vehicles`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({...form,capacity:parseInt(form.capacity),year:parseInt(form.year)})});
    if(res.ok){ setForm({registrationNumber:"",make:"",model:"",capacity:"",year:"",insuranceExpiry:"",licenceExpiry:""}); apiFetch(`${API}/vehicles`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setVehicles); }
  }
  return (
    <div className="bg-white border rounded-lg p-4">
      <h3 className="font-semibold text-sm">Vehicles — Capacity, Registration, Insurance and Licence Expiry with Reminders</h3>
      <div className="mt-2 grid sm:grid-cols-3 gap-2">
        <input placeholder="Reg ACD 1234" value={form.registrationNumber} onChange={e=>setForm({...form,registrationNumber:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="Make Toyota" value={form.make} onChange={e=>setForm({...form,make:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="Model Coaster" value={form.model} onChange={e=>setForm({...form,model:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="Capacity 40" value={form.capacity} onChange={e=>setForm({...form,capacity:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="Year 2020" value={form.year} onChange={e=>setForm({...form,year:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input type="date" value={form.insuranceExpiry} onChange={e=>setForm({...form,insuranceExpiry:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input type="date" value={form.licenceExpiry} onChange={e=>setForm({...form,licenceExpiry:e.target.value})} className="h-9 px-2 border rounded text-sm" />
      </div>
      <button onClick={create} className="mt-3 w-full py-2 rounded bg-primary-800 text-white text-sm">Create Vehicle</button>
      <div className="mt-4 max-h-80 overflow-auto border rounded">
        <table className="w-full text-xs border-collapse"><thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1">Reg</th><th className="border p-1">Make Model</th><th className="border p-1">Cap</th><th className="border p-1">Insurance Expiry</th><th className="border p-1">Licence Expiry</th><th className="border p-1">Days Left</th></tr></thead>
        <tbody>{vehicles.map(v=><tr key={v.id} className={v.needsInsuranceReminder||v.needsLicenceReminder?"bg-warning-50":""}><td className="border p-1">{v.registrationNumber}</td><td className="border p-1">{v.make} {v.model}</td><td className="border p-1">{v.capacity}</td><td className="border p-1">{new Date(v.insuranceExpiry).toLocaleDateString()} {v.needsInsuranceReminder&&<span className="text-danger-600">⚠ Reminder</span>}</td><td className="border p-1">{new Date(v.licenceExpiry).toLocaleDateString()} {v.needsLicenceReminder&&<span className="text-danger-600">⚠</span>}</td><td className="border p-1">{v.daysToInsuranceExpiry}d / {v.daysToLicenceExpiry}d</td></tr>)}</tbody></table>
      </div>
      <p className="text-xs text-neutral-500 mt-2">Reminders: insurance and licence expiry 30 days before, via messaging module to school admin, daily check in background job.</p>
    </div>
  );
}

function DriversTab() {
  const [drivers,setDrivers]=useState([]);
  const [form,setForm]=useState({fullName:"",role:"driver",licenceNumber:"",licenceType:"",licenceExpiry:"",phone:""});
  useEffect(()=>{ apiFetch(`${API}/drivers`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setDrivers); },[]);
  async function create(){
    const res=await apiFetch(`${API}/drivers`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify(form)});
    if(res.ok){ setForm({fullName:"",role:"driver",licenceNumber:"",licenceType:"",licenceExpiry:"",phone:""}); apiFetch(`${API}/drivers`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setDrivers); }
  }
  return (
    <div className="bg-white border rounded-lg p-4">
      <h3 className="font-semibold text-sm">Drivers and Assistants with Licence Expiry Tracking</h3>
      <div className="mt-2 grid sm:grid-cols-3 gap-2">
        <input placeholder="Full name" value={form.fullName} onChange={e=>setForm({...form,fullName:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <select value={form.role} onChange={e=>setForm({...form,role:e.target.value})} className="h-9 px-2 border rounded text-sm"><option value="driver">Driver</option><option value="assistant">Assistant</option></select>
        <input placeholder="Licence number" value={form.licenceNumber} onChange={e=>setForm({...form,licenceNumber:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="Licence type Class 4" value={form.licenceType} onChange={e=>setForm({...form,licenceType:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input type="date" value={form.licenceExpiry} onChange={e=>setForm({...form,licenceExpiry:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <input placeholder="Phone" value={form.phone} onChange={e=>setForm({...form,phone:e.target.value})} className="h-9 px-2 border rounded text-sm" />
      </div>
      <button onClick={create} className="mt-2 w-full py-2 rounded bg-primary-800 text-white text-sm">Create Driver/Assistant</button>
      <div className="mt-4 max-h-80 overflow-auto border rounded">
        <table className="w-full text-xs border-collapse"><thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1">Name</th><th className="border p-1">Role</th><th className="border p-1">Licence</th><th className="border p-1">Expiry</th><th className="border p-1">Days Left</th></tr></thead>
        <tbody>{drivers.map(d=><tr key={d.id} className={d.needsLicenceReminder?"bg-warning-50":""}><td className="border p-1">{d.fullName}</td><td className="border p-1">{d.role}</td><td className="border p-1">{d.licenceNumber} {d.licenceType}</td><td className="border p-1">{d.licenceExpiry?new Date(d.licenceExpiry).toLocaleDateString():""}</td><td className="border p-1">{d.daysToLicenceExpiry}d {d.needsLicenceReminder&&"⚠ Reminder"}</td></tr>)}</tbody></table>
      </div>
    </div>
  );
}

function AssignmentsTab() {
  const [routes,setRoutes]=useState([]);
  const [assignments,setAssignments]=useState([]);
  const [form,setForm]=useState({studentId:"",routeId:"",pickupStopId:"",academicYearId:"2026",termId:"1"});
  useEffect(()=>{ apiFetch(`${API}/routes`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setRoutes); },[]);
  async function assign(){
    const res=await apiFetch(`${API}/assignments`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({...form,studentId:parseInt(form.studentId),routeId:parseInt(form.routeId),pickupStopId:parseInt(form.pickupStopId),academicYearId:parseInt(form.academicYearId),termId:parseInt(form.termId)})});
    if(res.ok){ const data=await res.json(); alert(`Assigned learner to route, capacity enforced, transport fee flows into existing fee invoicing: fee amount ${data.feeAmount} ${data.currency}, feeApplied ${data.feeApplied}`); }
    else alert("Failed (capacity enforcement?): "+await res.text());
  }
  async function loadAssignments(){
    if(!form.routeId) return;
    const res=await apiFetch(`${API}/routes/${form.routeId}/assignments`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}});
    if(res.ok) setAssignments(await res.json());
  }
  return (
    <div className="bg-white border rounded-lg p-4 space-y-4">
      <h3 className="font-semibold text-sm">Learner Assignment to Route and Stop with Capacity Enforcement — Transport Fees Flow into Existing Fee Structure</h3>
      <div className="grid sm:grid-cols-3 gap-2">
        <input placeholder="StudentId" value={form.studentId} onChange={e=>setForm({...form,studentId:e.target.value})} className="h-9 px-2 border rounded text-sm" />
        <select value={form.routeId} onChange={e=>setForm({...form,routeId:e.target.value})} className="h-9 px-2 border rounded text-sm"><option value="">Select route</option>{routes.map(r=><option key={r.id} value={r.id}>{r.name} {r.code} Cap {r.capacity} Assigned {r.assignedLearners} Util {r.utilisationPercent}%</option>)}</select>
        <input placeholder="PickupStopId" value={form.pickupStopId} onChange={e=>setForm({...form,pickupStopId:e.target.value})} className="h-9 px-2 border rounded text-sm" />
      </div>
      <button onClick={assign} className="w-full py-2 rounded bg-primary-800 text-white text-sm">Assign Learner — Capacity Enforcement + Fee Flows into Existing Invoicing</button>
      <button onClick={loadAssignments} className="w-full py-2 rounded border text-sm">Load Assignments for Route</button>
      <div className="max-h-80 overflow-auto border rounded">
        <table className="w-full text-xs border-collapse"><thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1">Student</th><th className="border p-1">Route</th><th className="border p-1">Pickup</th><th className="border p-1">Fee Applied</th><th className="border p-1">Status</th></tr></thead>
        <tbody>{assignments.map(a=><tr key={a.id}><td className="border p-1">{a.studentName} {a.studentNumber}</td><td className="border p-1">{a.routeName}</td><td className="border p-1">{a.pickupStopName}</td><td className="border p-1">{a.feeApplied?"Yes "+a.feeAmount+" "+a.currency:"No"}</td><td className="border p-1">{a.status}</td></tr>)}</tbody></table>
      </div>
      <p className="text-xs text-neutral-500">Transport fees flow into existing fee structure and invoicing rather than parallel billing: when assigned, creates or updates fee_structure with fee_item TRANSPORT amount route.feeAmount for learner's year/term, then fee invoice generation per term will include transport fee. Existing fee entities not altered, just new fee_structure_item.</p>
    </div>
  );
}

function BoardingAttendanceTab() {
  const [routeId,setRouteId]=useState("");
  const [tripDate,setTripDate]=useState(new Date().toISOString().slice(0,10));
  const [tripType,setTripType]=useState("morning");
  const [items,setItems]=useState([{studentId:"",status:"boarded"}]);

  async function mark(){
    const res=await apiFetch(`${API}/attendance/boarding`,{method:"POST",headers:{"Content-Type":"application/json",Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage},body:JSON.stringify({routeId:parseInt(routeId),tripDate,tripType,items:items.map(i=>({studentId:parseInt(i.studentId),status:i.status}))})});
    if(res.ok){ alert("Boarding attendance marked per trip, if absent notification to guardians via existing messaging module"); }
  }

  return (
    <div className="bg-white border rounded-lg p-4">
      <h3 className="font-semibold text-sm">Boarding Attendance Per Trip if School Wants It — Route Change and Absence Notifications Using Existing Messaging Module</h3>
      <div className="grid sm:grid-cols-3 gap-2">
        <input placeholder="RouteId" value={routeId} onChange={e=>setRouteId(e.target.value)} className="h-9 px-2 border rounded text-sm" />
        <input type="date" value={tripDate} onChange={e=>setTripDate(e.target.value)} className="h-9 px-2 border rounded text-sm" />
        <select value={tripType} onChange={e=>setTripType(e.target.value)} className="h-9 px-2 border rounded text-sm"><option value="morning">Morning</option><option value="evening">Evening</option></select>
      </div>
      <div className="mt-3 space-y-2">
        {items.map((it,i)=><div key={i} className="flex gap-2"><input placeholder="StudentId" value={it.studentId} onChange={e=>{const copy=[...items]; copy[i].studentId=e.target.value; setItems(copy);}} className="flex-1 h-9 px-2 border rounded text-sm" /><select value={it.status} onChange={e=>{const copy=[...items]; copy[i].status=e.target.value; setItems(copy);}} className="h-9 px-2 border rounded text-sm"><option value="boarded">Boarded</option><option value="missed">Missed</option><option value="absent">Absent</option></select></div>)}
        <button onClick={()=>setItems([...items,{studentId:"",status:"boarded"}])} className="px-2 py-1 border rounded text-xs">+ Add learner</button>
      </div>
      <button onClick={mark} className="mt-3 w-full py-2 rounded bg-primary-800 text-white text-sm">Mark Boarding Attendance — Triggers Absence Notifications to Guardians via Existing Messaging</button>
    </div>
  );
}

function ReportsTab() {
  const [utilisation,setUtilisation]=useState([]);
  const [revenue,setRevenue]=useState([]);
  const [unassigned,setUnassigned]=useState({students:[],totalUnassigned:0});

  useEffect(()=>{
    apiFetch(`${API}/reports/utilisation`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setUtilisation);
    apiFetch(`${API}/reports/revenue?academicYearId=2026&termId=1`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setRevenue);
    apiFetch(`${API}/reports/unassigned-learners`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}}).then(r=>r.json()).then(setUnassigned);
  },[]);

  return (
    <div className="space-y-4">
      <div className="bg-white border rounded-lg p-4">
        <h3 className="font-semibold text-sm">Reports on Utilisation Per Route</h3>
        <div className="max-h-80 overflow-auto border rounded mt-2">
          <table className="w-full text-xs border-collapse"><thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1">Route</th><th className="border p-1">Capacity</th><th className="border p-1">Assigned</th><th className="border p-1">Utilisation</th><th className="border p-1">Boarded Today</th><th className="border p-1">Boarding Rate</th></tr></thead>
          <tbody>{utilisation.map(u=><tr key={u.routeId}><td className="border p-1">{u.routeName}</td><td className="border p-1">{u.capacity}</td><td className="border p-1">{u.assigned}</td><td className="border p-1">{u.utilisationPercent}%</td><td className="border p-1">{u.boardedToday}</td><td className="border p-1">{u.boardingRate}%</td></tr>)}</tbody></table>
        </div>
      </div>

      <div className="bg-white border rounded-lg p-4">
        <h3 className="font-semibold text-sm">Revenue Per Route — Transport Fees Flow into Existing Fee Invoicing</h3>
        <div className="max-h-80 overflow-auto border rounded mt-2">
          <table className="w-full text-xs border-collapse"><thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1">Route</th><th className="border p-1">Fee/Learner</th><th className="border p-1">Assigned</th><th className="border p-1">Invoiced</th><th className="border p-1">Collected</th><th className="border p-1">Arrears</th><th className="border p-1">Collection Rate</th></tr></thead>
          <tbody>{revenue.map(r=><tr key={r.routeId}><td className="border p-1">{r.routeName}</td><td className="border p-1">{r.feePerLearner}</td><td className="border p-1">{r.assignedLearners}</td><td className="border p-1">{r.totalInvoiced}</td><td className="border p-1">{r.totalCollected}</td><td className="border p-1">{r.totalArrears}</td><td className="border p-1">{r.collectionRate}%</td></tr>)}</tbody></table>
        </div>
      </div>

      <div className="bg-white border rounded-lg p-4">
        <h3 className="font-semibold text-sm">Unassigned Learners — Not Assigned to Any Route</h3>
        <div>Total unassigned: {unassigned.totalUnassigned}</div>
        <div className="max-h-80 overflow-auto border rounded mt-2">
          <table className="w-full text-xs border-collapse"><thead className="sticky top-0 bg-neutral-50"><tr><th className="border p-1">Student</th><th className="border p-1">Grade</th><th className="border p-1">Stream</th></tr></thead>
          <tbody>{(unassigned.students||[]).map(s=><tr key={s.studentId}><td className="border p-1">{s.studentName} {s.studentNumber}</td><td className="border p-1">{s.gradeName}</td><td className="border p-1">{s.streamName}</td></tr>)}</tbody></table>
        </div>
      </div>
    </div>
  );
}

function ManifestTab() {
  const [routeId,setRouteId]=useState("");
  const [manifest,setManifest]=useState(null);
  async function load(){
    const res=await apiFetch(`${API}/routes/${routeId}/manifest`,{headers:{Authorization:`Bearer ${getAccessToken()}` // SECURITY: memory not localStorage}});
    if(res.ok) setManifest(await res.json());
  }
  return (
    <div className="bg-white border rounded-lg p-4">
      <h3 className="font-semibold text-sm">Printable Route Manifest for Each Driver</h3>
      <div className="flex gap-2">
        <input placeholder="RouteId" value={routeId} onChange={e=>setRouteId(e.target.value)} className="flex-1 h-9 px-2 border rounded text-sm" />
        <button onClick={load} className="px-3 py-1.5 rounded bg-primary-800 text-white text-sm">Load Manifest</button>
        {manifest && <a href={`${API}/routes/${routeId}/manifest/print`} target="_blank" className="px-3 py-1.5 rounded border bg-white text-sm">Print A4</a>}
      </div>
      {manifest && (
        <div className="mt-4 border rounded p-3">
          <div className="font-bold">{manifest.routeName} ({manifest.code}) — {manifest.direction}</div>
          <div className="text-xs">Vehicle {manifest.vehicle?.registrationNumber} {manifest.vehicle?.make} {manifest.vehicle?.model} Cap {manifest.vehicle?.capacity} | Driver {manifest.driver?.fullName} {manifest.driver?.phone} | Assistant {manifest.assistant?.fullName}</div>
          <div className="mt-2 space-y-2">
            {manifest.stops.map(s=>(
              <div key={s.stopId} className="border rounded p-2">
                <div className="font-medium text-sm">{s.orderNumber}. {s.stopName} {s.address} — Expected {s.expectedArrivalTime} — Learners {s.learnersCount}</div>
                <div className="mt-1 text-xs space-y-0.5">
                  {s.learners.map(l=><div key={l.studentId} className="flex justify-between border-b py-0.5"><span>{l.studentName} {l.studentNumber} {l.gradeName} {l.streamName}</span><span>{l.guardianName} {l.guardianPhone} Pickup {l.pickupStopName} Drop {l.dropStopName||""}</span></div>)}
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}