import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import AppShell from '../components/layout/AppShell';
import { Button } from '../components/ui/Button';
import { Dialog } from '../components/ui/Dialog';
import { Input } from '../components/ui/Input';
import { Badge, FormError, Notice } from '../components/ui/Fields';
import { apiJson } from '../lib/apiClient';
import { capitalise, label, useApiErrors } from '../lib/schoolRecords';

// Guardians, backed by /api/guardians. Guardians are linked to students from the student's
// page; one guardian can be linked to several children.

const PAGE_SIZE = 25;
const EMPTY = { id: null, firstName: '', lastName: '', phone: '', email: '', address: '', nationalId: '' };

export default function GuardiansPage() {
  const apiError = useApiErrors();
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [result, setResult] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(null);
  const [notice, setNotice] = useState(null);

  const [selected, setSelected] = useState(null);
  const [form, setForm] = useState(null);
  const [errors, setErrors] = useState({});
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      const params = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE) });
      if (search.trim()) params.set('search', search.trim());
      setResult(await apiJson(`/api/guardians?${params}`));
    } catch (error) {
      const message = apiError(error, 'Your role cannot view guardian records.');
      if (message) setLoadError(message);
    } finally {
      setLoading(false);
    }
  }, [search, page, apiError]);

  useEffect(() => {
    const timer = setTimeout(load, 250);
    return () => clearTimeout(timer);
  }, [load]);

  async function open(id) {
    try {
      setSelected(await apiJson(`/api/guardians/${id}`));
    } catch (error) {
      const message = apiError(error, 'Your role cannot view guardian records.');
      if (message) setNotice({ type: 'error', message });
    }
  }

  async function save(event) {
    event.preventDefault();
    const next = {};
    if (!form.firstName.trim()) next.firstName = 'Enter the first name.';
    if (!form.lastName.trim()) next.lastName = 'Enter the last name.';
    if (!/^\+?[0-9 ()-]{7,20}$/.test(form.phone.trim())) next.phone = 'Enter a phone number, e.g. +263 77 123 4567.';
    setErrors(next);
    if (Object.keys(next).length) return;

    setSaving(true);
    try {
      const body = {
        firstName: form.firstName.trim(), lastName: form.lastName.trim(), phone: form.phone.trim(),
        email: form.email.trim() || null, address: form.address.trim() || null, nationalId: form.nationalId.trim() || null,
      };
      const saved = form.id
        ? await apiJson(`/api/guardians/${form.id}`, { method: 'PUT', body })
        : await apiJson('/api/guardians', { method: 'POST', body });
      setForm(null);
      if (selected) setSelected(saved);
      setNotice({ type: 'success', message: `${saved.firstName} ${saved.lastName} saved.` });
      await load();
    } catch (error) {
      const message = apiError(error, 'Only school admins, head teachers and registrars can change guardians.');
      if (message) setErrors({ ...error.fieldErrors, form: message });
    } finally {
      setSaving(false);
    }
  }

  async function remove(guardian) {
    if (!window.confirm(`Delete ${guardian.firstName} ${guardian.lastName}?`)) return;
    try {
      await apiJson(`/api/guardians/${guardian.id}`, { method: 'DELETE' });
      setSelected(null);
      setNotice({ type: 'success', message: `${guardian.firstName} ${guardian.lastName} deleted.` });
      await load();
    } catch (error) {
      const message = apiError(error, 'Only school admins can delete guardians.');
      if (message) setNotice({ type: 'error', message });
    }
  }

  const items = result?.items ?? [];
  const total = result?.total ?? 0;
  const totalPages = Math.max(result?.totalPages ?? 1, 1);

  return (
    <AppShell
      title="Guardians"
      description="Parents and guardians. Link them to students from the student's page."
      breadcrumbs={[{ label: 'People' }, { label: 'Guardians' }]}
      actions={<Button size="sm" onClick={() => { setErrors({}); setForm(EMPTY); }}>Add guardian</Button>}
    >
      <Notice notice={notice} onDismiss={() => setNotice(null)} />

      <div className="rounded-2xl bg-white border border-neutral-200 shadow-sm overflow-hidden">
        <div className="p-4 border-b border-neutral-100 flex flex-col sm:flex-row gap-3 sm:items-center">
          <input
            aria-label="Search guardians"
            placeholder="Search name, phone or email"
            value={search}
            onChange={e => { setSearch(e.target.value); setPage(1); }}
            className="flex-1 h-10 px-3 border border-neutral-200 rounded-xl text-sm bg-neutral-50 focus:bg-white focus:border-secondary-500 focus:ring-4 focus:ring-secondary-500/20 focus:outline-none"
          />
          <span className="text-[12px] text-neutral-500">{loading ? 'Loading...' : `${total} guardian${total === 1 ? '' : 's'}`}</span>
        </div>

        {loadError ? (
          <div className="p-8 text-center">
            <p className="text-[14px] text-danger-600">{loadError}</p>
            <Button variant="secondary" size="sm" className="mt-3" onClick={load}>Try again</Button>
          </div>
        ) : !loading && items.length === 0 ? (
          <div className="p-10 text-center text-[14px] text-neutral-800">{search ? 'No guardians match this search.' : 'No guardians yet. Guardians added with a student appear here.'}</div>
        ) : (
          <div className="overflow-auto">
            <table className="w-full text-[13px]">
              <thead className="bg-neutral-50 border-b border-neutral-200">
                <tr className="text-left">
                  <th className="p-3 font-semibold text-neutral-700">Guardian</th>
                  <th className="p-3 font-semibold text-neutral-700">Phone</th>
                  <th className="p-3 font-semibold text-neutral-700">Email</th>
                  <th className="p-3 font-semibold text-neutral-700">Students</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-neutral-100">
                {items.map(g => (
                  <tr key={g.id} className="hover:bg-primary-50/50 transition">
                    <td className="p-3"><button onClick={() => open(g.id)} className="font-medium text-neutral-900 hover:underline text-left">{g.lastName}, {g.firstName}</button></td>
                    <td className="p-3 text-neutral-700">{g.phone}</td>
                    <td className="p-3 text-neutral-700">{g.email || '—'}</td>
                    <td className="p-3">{g.studentCount}</td>
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
        open={!!selected && !form}
        onOpenChange={o => !o && setSelected(null)}
        size="lg"
        title={selected ? `${selected.firstName} ${selected.lastName}` : ''}
        description={selected ? [selected.phone, selected.email].filter(Boolean).join(' • ') : undefined}
        footer={selected && (
          <>
            <Button variant="ghost" onClick={() => remove(selected)}>Delete</Button>
            <Button variant="secondary" onClick={() => { setErrors({}); setForm({ ...EMPTY, ...selected, email: selected.email || '', address: selected.address || '', nationalId: selected.nationalId || '' }); }}>Edit</Button>
          </>
        )}
      >
        {selected && (
          <div className="space-y-3 text-[13px]">
            {selected.address && <p className="text-neutral-700">{selected.address}</p>}
            <h3 className="font-semibold text-neutral-900">Students</h3>
            {selected.students.length === 0 ? (
              <p className="text-neutral-600">Not linked to any student.</p>
            ) : (
              <ul className="divide-y divide-neutral-100 rounded-xl border border-neutral-200">
                {selected.students.map(s => (
                  <li key={s.linkId} className="p-3 flex flex-wrap items-center gap-2">
                    <Link to={`/students/${s.studentId}`} className="flex-1 font-medium text-neutral-900 hover:underline">{s.firstName} {s.lastName}</Link>
                    <span className="text-neutral-600">{s.className}</span>
                    <Badge>{capitalise(s.relationshipType)}</Badge>
                    {s.isPrimaryContact && <Badge tone="primary">Primary</Badge>}
                    {s.status !== 'active' && <Badge tone="warning">{label(s.status)}</Badge>}
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </Dialog>

      <Dialog
        open={!!form}
        onOpenChange={o => !o && setForm(null)}
        size="lg"
        title={form?.id ? 'Edit guardian' : 'Add guardian'}
        footer={<><Button variant="ghost" onClick={() => setForm(null)}>Cancel</Button><Button loading={saving} type="submit" form="guardian-form">Save</Button></>}
      >
        {form && (
          <form id="guardian-form" onSubmit={save} noValidate className="space-y-4">
            <FormError message={errors.form} />
            <div className="grid sm:grid-cols-2 gap-4">
              <Input label="First name" id="g-first" required value={form.firstName} onChange={e => setForm({ ...form, firstName: e.target.value })} error={errors.firstName} />
              <Input label="Last name" id="g-last" required value={form.lastName} onChange={e => setForm({ ...form, lastName: e.target.value })} error={errors.lastName} />
              <Input label="Phone" id="g-phone" type="tel" required value={form.phone} onChange={e => setForm({ ...form, phone: e.target.value })} error={errors.phone} placeholder="+263 77 123 4567" />
              <Input label="Email" id="g-email" type="email" value={form.email} onChange={e => setForm({ ...form, email: e.target.value })} error={errors.email} />
              <Input label="National ID" id="g-national" value={form.nationalId} onChange={e => setForm({ ...form, nationalId: e.target.value })} error={errors.nationalId} />
            </div>
            <Input label="Address" id="g-address" value={form.address} onChange={e => setForm({ ...form, address: e.target.value })} error={errors.address} />
          </form>
        )}
      </Dialog>
    </AppShell>
  );
}
