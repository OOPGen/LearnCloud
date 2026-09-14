import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import AppShell from '../components/layout/AppShell';
import { Button } from '../components/ui/Button';
import { Dialog } from '../components/ui/Dialog';
import { Input } from '../components/ui/Input';
import { ApiError, apiFetch, apiJson } from '../lib/apiClient';

// Subjects, backed by /api/academic/subjects: server-side search, filter, sort and
// pagination; create; delete (soft delete on the server); CSV export.

const PAGE_SIZE = 25;
const EMPTY_FORM = { name: '', code: '', department: '', description: '', isCore: true };

function buildQuery(filters, page) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) });
  if (filters.search.trim()) params.set('search', filters.search.trim());
  if (filters.isCore !== '') params.set('isCore', filters.isCore);
  if (filters.sortBy) params.set('sortBy', filters.sortBy);
  return params.toString();
}

export default function SubjectsPage() {
  const navigate = useNavigate();
  const [filters, setFilters] = useState({ search: '', isCore: '', sortBy: 'name' });
  const [page, setPage] = useState(1);
  const [result, setResult] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(null);

  const [createOpen, setCreateOpen] = useState(false);
  const [form, setForm] = useState(EMPTY_FORM);
  const [formErrors, setFormErrors] = useState({});
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState(null);

  const handleAuthFailure = useCallback((error) => {
    if (error instanceof ApiError && error.status === 401) {
      navigate('/login', { replace: true, state: { from: '/subjects' } });
      return true;
    }
    return false;
  }, [navigate]);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setResult(await apiJson(`/api/academic/subjects?${buildQuery(filters, page)}`));
    } catch (error) {
      if (!handleAuthFailure(error)) {
        setLoadError(error.status === 403 ? 'Your role cannot view subjects.' : error.message);
      }
    } finally {
      setLoading(false);
    }
  }, [filters, page, handleAuthFailure]);

  // Debounce typing in the search box; other filters apply at once.
  useEffect(() => {
    const timer = setTimeout(load, 250);
    return () => clearTimeout(timer);
  }, [load]);

  function updateFilter(key, value) {
    setFilters(current => ({ ...current, [key]: value }));
    setPage(1);
  }

  function validate() {
    const errors = {};
    if (form.name.trim().length < 2) errors.name = 'Name must be at least 2 characters';
    if (!/^[A-Z0-9_]{2,20}$/.test(form.code)) errors.code = 'Code: 2-20 capital letters, digits or underscore, e.g. MATH';
    setFormErrors(errors);
    return Object.keys(errors).length === 0;
  }

  async function createSubject(event) {
    event.preventDefault();
    if (!validate()) return;
    setSaving(true);
    try {
      const created = await apiJson('/api/academic/subjects', {
        method: 'POST',
        body: {
          name: form.name.trim(),
          code: form.code,
          description: form.description.trim() || null,
          isCore: form.isCore,
          department: form.department.trim() || null,
        },
      });
      setCreateOpen(false);
      setForm(EMPTY_FORM);
      setFormErrors({});
      setNotice({ type: 'success', message: `${created.name} added.` });
      await load();
    } catch (error) {
      if (handleAuthFailure(error)) return;
      setFormErrors({ ...error.fieldErrors, form: error.status === 403 ? 'Only school admins and head teachers can add subjects.' : error.message });
    } finally {
      setSaving(false);
    }
  }

  async function deleteSubject(subject) {
    if (!window.confirm(`Delete ${subject.name} (${subject.code})?`)) return;
    try {
      await apiJson(`/api/academic/subjects/${subject.id}`, { method: 'DELETE' });
      setNotice({ type: 'success', message: `${subject.name} deleted.` });
      await load();
    } catch (error) {
      if (handleAuthFailure(error)) return;
      setNotice({ type: 'error', message: error.status === 403 ? 'Only school admins can delete subjects.' : error.message });
    }
  }

  async function exportCsv() {
    const response = await apiFetch(`/api/academic/subjects/export?${buildQuery(filters, 1)}`);
    if (!response.ok) {
      setNotice({ type: 'error', message: response.status === 403 ? 'Your role cannot export subjects.' : 'Export failed.' });
      return;
    }
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement('a');
    link.href = url;
    link.download = 'subjects.csv';
    link.click();
    URL.revokeObjectURL(url);
  }

  const items = result?.items ?? [];
  const total = result?.total ?? 0;
  const totalPages = Math.max(result?.totalPages ?? 1, 1);

  return (
    <AppShell
      title="Subjects"
      description="The subjects your school teaches."
      breadcrumbs={[{ label: 'Academic' }, { label: 'Subjects' }]}
      actions={
        <>
          <Button variant="secondary" size="sm" onClick={exportCsv} disabled={!total}>Export CSV</Button>
          <Button size="sm" onClick={() => setCreateOpen(true)}>Add Subject</Button>
        </>
      }
    >
      {notice && (
        <div role="status" className={`mb-4 p-3 rounded-xl border text-[13px] flex items-center justify-between ${notice.type === 'success' ? 'bg-success-50 border-success-100 text-success-600' : 'bg-danger-50 border-danger-100 text-danger-600'}`}>
          <span>{notice.message}</span>
          <button onClick={() => setNotice(null)} aria-label="Dismiss" className="ml-3 opacity-60 hover:opacity-100">✕</button>
        </div>
      )}

      <div className="rounded-2xl bg-white border border-neutral-200 shadow-sm overflow-hidden">
        <div className="p-4 border-b border-neutral-100 flex flex-col sm:flex-row gap-3">
          <div className="flex-1 grid sm:grid-cols-3 gap-3">
            <input
              aria-label="Search subjects"
              placeholder="Search name, code, department"
              value={filters.search}
              onChange={e => updateFilter('search', e.target.value)}
              className="h-10 px-3 border border-neutral-200 rounded-xl text-sm bg-neutral-50 focus:bg-white focus:border-secondary-500 focus:ring-4 focus:ring-secondary-500/20 focus:outline-none"
            />
            <select aria-label="Subject type" value={filters.isCore} onChange={e => updateFilter('isCore', e.target.value)} className="h-10 px-3 border border-neutral-200 rounded-xl text-sm bg-white">
              <option value="">All types</option>
              <option value="true">Core only</option>
              <option value="false">Optional only</option>
            </select>
            <select aria-label="Sort by" value={filters.sortBy} onChange={e => updateFilter('sortBy', e.target.value)} className="h-10 px-3 border border-neutral-200 rounded-xl text-sm bg-white">
              <option value="name">Sort by name</option>
              <option value="code">Sort by code</option>
              <option value="department">Sort by department</option>
              <option value="created_at">Sort by date added</option>
            </select>
          </div>
          <div className="flex items-center text-[12px] text-neutral-500">
            {loading ? 'Loading...' : `${total} subject${total === 1 ? '' : 's'}`}
          </div>
        </div>

        {loadError ? (
          <div className="p-8 text-center">
            <p className="text-[14px] text-danger-600">{loadError}</p>
            <Button variant="secondary" size="sm" className="mt-3" onClick={load}>Try again</Button>
          </div>
        ) : !loading && items.length === 0 ? (
          <div className="p-10 text-center">
            <p className="text-[14px] font-medium text-neutral-800">{filters.search || filters.isCore ? 'No subjects match these filters.' : 'No subjects yet.'}</p>
            {!filters.search && !filters.isCore && <Button size="sm" className="mt-3" onClick={() => setCreateOpen(true)}>Add your first subject</Button>}
          </div>
        ) : (
          <div className="overflow-auto">
            <table className="w-full text-[13px]">
              <thead className="bg-neutral-50 border-b border-neutral-200">
                <tr className="text-left">
                  <th className="p-3 font-semibold text-neutral-700">Name</th>
                  <th className="p-3 font-semibold text-neutral-700">Code</th>
                  <th className="p-3 font-semibold text-neutral-700">Department</th>
                  <th className="p-3 font-semibold text-neutral-700">Type</th>
                  <th className="p-3 font-semibold text-neutral-700">Grades offered</th>
                  <th className="p-3 font-semibold text-neutral-700">Added</th>
                  <th className="p-3"><span className="sr-only">Actions</span></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-neutral-100">
                {items.map(s => (
                  <tr key={s.id} className="hover:bg-primary-50/50 transition">
                    <td className="p-3 font-medium text-neutral-900">{s.name}</td>
                    <td className="p-3"><span className="px-2 py-0.5 rounded-full bg-neutral-100 text-[11px] font-mono">{s.code}</span></td>
                    <td className="p-3 text-neutral-600">{s.department || '—'}</td>
                    <td className="p-3">{s.isCore ? <span className="px-2 py-0.5 rounded-full bg-primary-100 text-primary-800 text-[11px] font-medium">Core</span> : <span className="px-2 py-0.5 rounded-full bg-neutral-100 text-[11px]">Optional</span>}</td>
                    <td className="p-3">{s.gradesOffered}</td>
                    <td className="p-3 text-neutral-500 text-[12px]">{new Date(s.createdAt).toLocaleDateString()}</td>
                    <td className="p-3 text-right"><Button variant="ghost" size="sm" onClick={() => deleteSubject(s)}>Delete</Button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="p-3 bg-neutral-50 border-t border-neutral-100 flex items-center justify-between text-[12px]">
          <span className="text-neutral-600">Page {Math.min(page, totalPages)} of {totalPages}</span>
          <div className="flex gap-2">
            <Button variant="secondary" size="sm" disabled={page <= 1 || loading} onClick={() => setPage(p => p - 1)}>Prev</Button>
            <Button variant="secondary" size="sm" disabled={page >= totalPages || loading} onClick={() => setPage(p => p + 1)}>Next</Button>
          </div>
        </div>
      </div>

      <Dialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        title="Add subject"
        description="Codes are short and unique within your school, for example MATH or ENG."
        footer={
          <>
            <Button variant="ghost" onClick={() => setCreateOpen(false)}>Cancel</Button>
            <Button loading={saving} type="submit" form="create-subject-form">Add subject</Button>
          </>
        }
      >
        <form id="create-subject-form" onSubmit={createSubject} noValidate className="space-y-4">
          {formErrors.form && <p role="alert" className="text-[13px] text-danger-600">{formErrors.form}</p>}
          <Input label="Name" id="subject-name" required value={form.name} onChange={e => setForm({ ...form, name: e.target.value })} error={formErrors.name} placeholder="Mathematics" />
          <Input label="Code" id="subject-code" required value={form.code} onChange={e => setForm({ ...form, code: e.target.value.toUpperCase() })} error={formErrors.code} placeholder="MATH" />
          <Input label="Department" id="subject-department" value={form.department} onChange={e => setForm({ ...form, department: e.target.value })} error={formErrors.department} placeholder="Sciences" />
          <Input label="Description" id="subject-description" value={form.description} onChange={e => setForm({ ...form, description: e.target.value })} error={formErrors.description} />
          <label className="flex items-center gap-2 text-[13px] text-neutral-700">
            <input type="checkbox" checked={form.isCore} onChange={e => setForm({ ...form, isCore: e.target.checked })} />
            Core subject
          </label>
        </form>
      </Dialog>
    </AppShell>
  );
}
