/**
 * Phase 1: Subjects - React List and Detail Pages
 * Every list: server-side search, filter, sort, pagination, and CSV export
 * Every mutation: permission-checked, validated, audited, soft-deleting
 */

import React, { useState, useEffect } from 'react';
import { apiFetch, getAccessToken, setAccessToken } from '../../LearnCloud.Web/src/lib/apiClient.js'; // SECURITY C2 FIX

const API = "/api/academic/subjects";

function usePermissions() {
  // Simplified - in real app reads JWT perm claims
  return { canCreate: true, canEdit: true, canDelete: false, canExport: true };
}

export default function SubjectsList() {
  const [data, setData] = useState({ items: [], total: 0, page: 1, pageSize: 25, totalPages: 0 });
  const [search, setSearch] = useState("");
  const [department, setDepartment] = useState("");
  const [isCore, setIsCore] = useState("");
  const [sortBy, setSortBy] = useState("name");
  const [sortDesc, setSortDesc] = useState(false);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const { canCreate, canExport } = usePermissions();

  async function load() {
    setLoading(true);
    const params = new URLSearchParams({
      search,
      department,
      isCore,
      sortBy,
      sortDesc: sortDesc.toString(),
      page: page.toString(),
      pageSize: "25"
    });
    const res = await apiFetch(`${API}?${params}`, { headers: { Authorization: `Bearer ${getAccessToken()}` // SECURITY: memory not localStorage } });
    if (res.ok) {
      const result = await res.json();
      setData(result);
    }
    setLoading(false);
  }

  useEffect(() => { load(); }, [search, department, isCore, sortBy, sortDesc, page]);

  async function exportCsv() {
    const params = new URLSearchParams({ search, department, sortBy, sortDesc: sortDesc.toString(), page: "1", pageSize: "1000" });
    const res = await apiFetch(`${API}/export?${params}`, { headers: { Authorization: `Bearer ${getAccessToken()}` // SECURITY: memory not localStorage } });
    if (res.ok) {
      const blob = await res.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `subjects_${new Date().toISOString().slice(0,10)}.csv`;
      a.click();
    }
  }

  return (
    <div className="p-4 max-w-6xl mx-auto">
      <div className="flex justify-between items-center">
        <h1 className="text-2xl font-bold">Subjects — Foundation for Teaching</h1>
        {canCreate && <a href="/academic/subjects/new" className="px-4 py-2 rounded-lg bg-primary-800 text-white text-sm">Add Subject</a>}
      </div>

      <div className="mt-4 bg-white border rounded-lg p-4">
        <div className="grid sm:grid-cols-4 gap-3">
          <input placeholder="Search name, code, department" value={search} onChange={e => { setSearch(e.target.value); setPage(1); }} className="h-10 px-3 border rounded-lg text-sm" />
          <select value={department} onChange={e => { setDepartment(e.target.value); setPage(1); }} className="h-10 px-3 border rounded-lg text-sm">
            <option value="">All Departments</option>
            <option value="Sciences">Sciences</option>
            <option value="Languages">Languages</option>
            <option value="Commercials">Commercials</option>
            <option value="Arts">Arts</option>
            <option value="Practical">Practical</option>
          </select>
          <select value={isCore} onChange={e => { setIsCore(e.target.value); setPage(1); }} className="h-10 px-3 border rounded-lg text-sm">
            <option value="">All Types</option>
            <option value="true">Core Only</option>
            <option value="false">Non-Core Only</option>
          </select>
          <div className="flex gap-2">
            <select value={sortBy} onChange={e => setSortBy(e.target.value)} className="h-10 px-3 border rounded-lg text-sm flex-1">
              <option value="name">Sort by Name</option>
              <option value="code">Sort by Code</option>
              <option value="department">Sort by Department</option>
              <option value="created_at">Sort by Created</option>
            </select>
            <button onClick={() => setSortDesc(!sortDesc)} className="h-10 w-10 grid place-items-center border rounded-lg">{sortDesc ? "↓" : "↑"}</button>
          </div>
        </div>

        <div className="mt-4 flex justify-between items-center text-sm">
          <span className="text-neutral-600">{data.total} subjects • Page {data.page} of {data.totalPages}</span>
          {canExport && <button onClick={exportCsv} className="px-3 py-1.5 rounded border text-xs hover:bg-neutral-50">Export CSV • Server-side filtered</button>}
        </div>

        <div className="mt-3 overflow-auto border rounded-lg max-h-[500px]">
          <table className="w-full text-sm border-collapse">
            <thead className="sticky top-0 bg-neutral-50">
              <tr><th className="border p-2 text-left">Name</th><th className="border p-2">Code</th><th className="border p-2">Department</th><th className="border p-2">Core</th><th className="border p-2">Grades Offered</th><th className="border p-2">Created</th></tr>
            </thead>
            <tbody>
              {loading ? <tr><td colSpan="6" className="p-4 text-center">Loading server-side...</td></tr> :
                data.items.map(s => (
                  <tr key={s.id} className="hover:bg-primary-50 cursor-pointer" onClick={() => window.location.href = `/academic/subjects/${s.id}`}>
                    <td className="border p-2 font-medium">{s.name}</td>
                    <td className="border p-2">{s.code}</td>
                    <td className="border p-2">{s.department}</td>
                    <td className="border p-2">{s.isCore ? <span className="px-1.5 py-0.5 rounded bg-primary-100 text-primary-800 text-xs">Core</span> : <span className="px-1.5 py-0.5 rounded bg-neutral-100 text-xs">Optional</span>}</td>
                    <td className="border p-2">{s.gradesOffered}</td>
                    <td className="border p-2 text-xs text-neutral-500">{new Date(s.createdAt).toLocaleDateString()}</td>
                  </tr>
                ))
              }
            </tbody>
          </table>
        </div>

        <div className="mt-3 flex justify-center gap-2">
          <button disabled={page <= 1} onClick={() => setPage(p => p - 1)} className="px-3 py-1 rounded border disabled:opacity-50">Prev</button>
          <span className="px-3 py-1 text-sm">Page {data.page} / {data.totalPages}</span>
          <button disabled={page >= data.totalPages} onClick={() => setPage(p => p + 1)} className="px-3 py-1 rounded border disabled:opacity-50">Next</button>
        </div>
      </div>
    </div>
  );
}

export function SubjectDetail({ id }) {
  const [subject, setSubject] = useState(null);
  const [grades, setGrades] = useState([]);
  const { canEdit } = usePermissions();

  useEffect(() => {
    apiFetch(`${API}/${id}`, { headers: { Authorization: `Bearer ${getAccessToken()}` // SECURITY: memory not localStorage } }).then(r => r.json()).then(setSubject);
    apiFetch(`${API}/by-grade/1?academicYearId=2026`, { headers: { Authorization: `Bearer ${getAccessToken()}` // SECURITY: memory not localStorage } }).then(r => r.json()).then(setGrades).catch(() => {});
  }, [id]);

  if (!subject) return <div className="p-8">Loading subject detail...</div>;

  return (
    <div className="p-4 max-w-4xl mx-auto">
      <a href="/academic/subjects" className="text-sm text-primary-700 hover:underline">← Back to Subjects</a>
      <div className="mt-4 bg-white border rounded-xl p-6 shadow-sm">
        <div className="flex justify-between items-start">
          <div>
            <h1 className="text-2xl font-bold">{subject.name}</h1>
            <p className="text-sm text-neutral-600 mt-1">{subject.code} • {subject.department} • {subject.isCore ? "Core" : "Optional"} • {subject.status}</p>
            <p className="text-sm mt-2">{subject.description}</p>
          </div>
          {canEdit && <a href={`/academic/subjects/${id}/edit`} className="px-3 py-1.5 rounded border text-sm">Edit</a>}
        </div>

        <div className="mt-6 grid sm:grid-cols-2 gap-4 text-sm">
          <div className="p-3 rounded-lg bg-neutral-50 border">
            <div className="font-medium">Grades Offered</div>
            <div className="text-2xl font-bold mt-1">{subject.gradesOffered}</div>
            <div className="text-xs text-neutral-500">Number of grades where this subject is assigned</div>
          </div>
          <div className="p-3 rounded-lg bg-neutral-50 border">
            <div className="font-medium">Created</div>
            <div className="text-sm mt-1">{new Date(subject.createdAt).toLocaleDateString()}</div>
            <div className="text-xs text-neutral-500">Audit: created_at, created_by tracked</div>
          </div>
        </div>

        <div className="mt-6">
          <h3 className="font-semibold">Assigned to Grades (Academic Year 2026)</h3>
          <div className="mt-2 border rounded-lg max-h-60 overflow-auto">
            <table className="w-full text-sm">
              <thead className="bg-neutral-50"><tr><th className="border p-2">Grade</th><th className="border p-2">Code</th><th className="border p-2">Compulsory</th></tr></thead>
              <tbody>
                {grades.map(g => (
                  <tr key={g.id}><td className="border p-2">{g.gradeName}</td><td className="border p-2">{g.gradeCode}</td><td className="border p-2">{g.isCompulsory ? "Yes" : "No"}</td></tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="mt-6 p-3 rounded-lg bg-primary-50 border border-primary-100 text-xs">
          <strong>Audit & Permissions:</strong> Every mutation permission-checked (roles.manage), validated (FluentValidation), audited (audit_logs old/new values), soft-deleting (is_deleted). Same screens reachable from Settings → Academic → Subjects later, not one-time wizard path.
        </div>
      </div>
    </div>
  );
}