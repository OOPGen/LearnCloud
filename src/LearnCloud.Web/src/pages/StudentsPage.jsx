import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import AppShell from '../components/layout/AppShell';
import { Button } from '../components/ui/Button';
import { Dialog } from '../components/ui/Dialog';
import { Input } from '../components/ui/Input';
import { Badge, CheckboxField, FormError, Notice, SelectField } from '../components/ui/Fields';
import { apiFetch, apiJson } from '../lib/apiClient';
import { GENDERS, RELATIONSHIPS, STUDENT_STATUSES, capitalise, classOptions, formatDate, label, todayInput, useApiErrors } from '../lib/schoolRecords';

// Student register, backed by /api/students: server-side search, filters, sort and
// pagination; CSV export; adding a student with their first class and guardian.

const PAGE_SIZE = 25;

const EMPTY_STUDENT = {
  firstName: '', lastName: '', gender: '', dob: '', studentNumber: '',
  yearId: '', streamId: '', enrolmentDate: todayInput(), enrolmentType: 'new',
  addGuardian: true, guardianFirstName: '', guardianLastName: '', guardianPhone: '', guardianEmail: '', relationship: 'mother',
};

function buildQuery(filters, page) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(PAGE_SIZE), sortBy: filters.sortBy });
  if (filters.search.trim()) params.set('search', filters.search.trim());
  if (filters.status) params.set('status', filters.status);
  if (filters.academicYearId) params.set('academicYearId', filters.academicYearId);
  if (filters.streamId) params.set('streamId', filters.streamId);
  return params.toString();
}

export default function StudentsPage() {
  const apiError = useApiErrors();
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  const [filters, setFilters] = useState({
    search: '',
    status: searchParams.get('streamId') ? '' : 'active',
    academicYearId: searchParams.get('academicYearId') || '',
    streamId: searchParams.get('streamId') || '',
    sortBy: 'name',
  });
  const [page, setPage] = useState(1);
  const [result, setResult] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(null);
  const [notice, setNotice] = useState(null);

  const [years, setYears] = useState([]);
  const [gradesByYear, setGradesByYear] = useState({});

  const [form, setForm] = useState(null);
  const [errors, setErrors] = useState({});
  const [saving, setSaving] = useState(false);

  const loadGrades = useCallback(async yearId => {
    if (!yearId || gradesByYear[yearId]) return;
    try {
      const grades = await apiJson(`/api/academic/grades?academicYearId=${yearId}`);
      setGradesByYear(current => ({ ...current, [yearId]: grades }));
    } catch { /* the class filter stays empty; the list itself reports errors */ }
  }, [gradesByYear]);

  useEffect(() => {
    apiJson('/api/academic/years').then(list => {
      setYears(list);
      const current = list.find(y => y.isCurrent) || list[0];
      setFilters(f => (f.academicYearId || !current ? f : { ...f, academicYearId: String(current.id) }));
    }).catch(() => { /* reported by the list request */ });
  }, []);

  useEffect(() => { loadGrades(filters.academicYearId); }, [filters.academicYearId, loadGrades]);
  useEffect(() => { if (form?.yearId) loadGrades(form.yearId); }, [form?.yearId, loadGrades]);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setResult(await apiJson(`/api/students?${buildQuery(filters, page)}`));
    } catch (error) {
      const message = apiError(error, 'Your role cannot view student records.');
      if (message) setLoadError(message);
    } finally {
      setLoading(false);
    }
  }, [filters, page, apiError]);

  useEffect(() => {
    const timer = setTimeout(load, 250);
    return () => clearTimeout(timer);
  }, [load]);

  function updateFilter(key, value) {
    setFilters(current => ({ ...current, [key]: value, ...(key === 'academicYearId' ? { streamId: '' } : {}) }));
    setPage(1);
  }

  const filterClasses = useMemo(() => classOptions(gradesByYear[filters.academicYearId] || []), [gradesByYear, filters.academicYearId]);
  const formClasses = useMemo(() => classOptions(gradesByYear[form?.yearId] || []), [gradesByYear, form?.yearId]);

  function openCreate() {
    const current = years.find(y => y.isCurrent) || years[0];
    setErrors({});
    setForm({ ...EMPTY_STUDENT, enrolmentDate: todayInput(), yearId: current ? String(current.id) : '' });
  }

  async function createStudent(event) {
    event.preventDefault();
    const f = form;
    const next = {};
    if (!f.firstName.trim()) next.firstName = 'Enter the first name.';
    if (!f.lastName.trim()) next.lastName = 'Enter the last name.';
    if (f.dob && f.dob > todayInput()) next.dob = 'Date of birth cannot be in the future.';
    if (!f.streamId) next.streamId = 'Choose a class.';
    if (f.addGuardian) {
      if (!f.guardianFirstName.trim()) next.guardianFirstName = "Enter the guardian's first name.";
      if (!f.guardianLastName.trim()) next.guardianLastName = "Enter the guardian's last name.";
      if (!/^\+?[0-9 ()-]{7,20}$/.test(f.guardianPhone.trim())) next.guardianPhone = 'Enter a phone number, e.g. +263 77 123 4567.';
    }
    setErrors(next);
    if (Object.keys(next).length) return;

    setSaving(true);
    try {
      const created = await apiJson('/api/students', {
        method: 'POST',
        body: {
          firstName: f.firstName.trim(),
          lastName: f.lastName.trim(),
          gender: f.gender || null,
          dob: f.dob || null,
          studentNumber: f.studentNumber.trim() || null,
          streamId: Number(f.streamId),
          enrolmentDate: f.enrolmentDate || null,
          enrolmentType: f.enrolmentType,
          guardian: f.addGuardian ? {
            newGuardian: { firstName: f.guardianFirstName.trim(), lastName: f.guardianLastName.trim(), phone: f.guardianPhone.trim(), email: f.guardianEmail.trim() || null },
            relationshipType: f.relationship,
            isPrimaryContact: true, isBillingContact: true, isEmergencyContact: true, canPickup: true,
          } : null,
        },
      });
      setForm(null);
      navigate(`/students/${created.id}`, { state: { notice: `${created.firstName} ${created.lastName} added as ${created.studentNumber}.` } });
    } catch (error) {
      const message = apiError(error, 'Only school admins, head teachers and registrars can add students.');
      if (message) setErrors({ ...error.fieldErrors, form: message });
    } finally {
      setSaving(false);
    }
  }

  async function exportCsv() {
    const response = await apiFetch(`/api/students/export?${buildQuery(filters, 1)}`);
    if (!response.ok) {
      setNotice({ type: 'error', message: response.status === 403 ? 'Your role cannot export student records.' : 'Export failed.' });
      return;
    }
    const url = URL.createObjectURL(await response.blob());
    const link = document.createElement('a');
    link.href = url;
    link.download = `students_${todayInput()}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  const items = result?.items ?? [];
  const total = result?.total ?? 0;
  const totalPages = Math.max(result?.totalPages ?? 1, 1);
  const filtered = filters.search || filters.streamId || filters.status !== 'active';

  return (
    <AppShell
      title="Students"
      description="Every learner, their class and their primary guardian."
      breadcrumbs={[{ label: 'People' }, { label: 'Students' }]}
      actions={
        <>
          <Button variant="secondary" size="sm" onClick={exportCsv} disabled={!total}>Export CSV</Button>
          <Button size="sm" onClick={openCreate}>Add student</Button>
        </>
      }
    >
      <Notice notice={notice} onDismiss={() => setNotice(null)} />

      <div className="rounded-2xl bg-white border border-neutral-200 shadow-sm overflow-hidden">
        <div className="p-4 border-b border-neutral-100 grid gap-3 sm:grid-cols-2 lg:grid-cols-6">
          <input
            aria-label="Search students"
            placeholder="Search name or student number"
            value={filters.search}
            onChange={e => updateFilter('search', e.target.value)}
            className="lg:col-span-2 h-10 px-3 border border-neutral-200 rounded-xl text-sm bg-neutral-50 focus:bg-white focus:border-secondary-500 focus:ring-4 focus:ring-secondary-500/20 focus:outline-none"
          />
          <select aria-label="Academic year" value={filters.academicYearId} onChange={e => updateFilter('academicYearId', e.target.value)} className="h-10 px-3 border border-neutral-200 rounded-xl text-sm bg-white">
            <option value="">All years</option>
            {years.map(y => <option key={y.id} value={y.id}>{y.name}{y.isCurrent ? ' (current)' : ''}</option>)}
          </select>
          <select aria-label="Class" value={filters.streamId} onChange={e => updateFilter('streamId', e.target.value)} className="h-10 px-3 border border-neutral-200 rounded-xl text-sm bg-white" disabled={!filters.academicYearId}>
            <option value="">All classes</option>
            {filterClasses.map(c => <option key={c.id} value={c.id}>{c.label}</option>)}
          </select>
          <select aria-label="Status" value={filters.status} onChange={e => updateFilter('status', e.target.value)} className="h-10 px-3 border border-neutral-200 rounded-xl text-sm bg-white">
            <option value="">All statuses</option>
            {STUDENT_STATUSES.map(s => <option key={s.value} value={s.value}>{s.label}</option>)}
          </select>
          <select aria-label="Sort by" value={filters.sortBy} onChange={e => updateFilter('sortBy', e.target.value)} className="h-10 px-3 border border-neutral-200 rounded-xl text-sm bg-white">
            <option value="name">Sort by name</option>
            <option value="class">Sort by class</option>
            <option value="student_number">Sort by number</option>
            <option value="created_at">Newest first</option>
          </select>
        </div>
        <div className="px-4 py-2 border-b border-neutral-100 text-[12px] text-neutral-500">
          {loading ? 'Loading...' : `${total} student${total === 1 ? '' : 's'}`}
        </div>

        {loadError ? (
          <div className="p-8 text-center">
            <p className="text-[14px] text-danger-600">{loadError}</p>
            <Button variant="secondary" size="sm" className="mt-3" onClick={load}>Try again</Button>
          </div>
        ) : !loading && items.length === 0 ? (
          <div className="p-10 text-center">
            <p className="text-[14px] font-medium text-neutral-800">{filtered ? 'No students match these filters.' : 'No students yet.'}</p>
            {!filtered && (years.length
              ? <Button size="sm" className="mt-3" onClick={openCreate}>Add your first student</Button>
              : <p className="mt-2 text-[13px] text-neutral-600">Set up an <Link to="/academic-years" className="text-secondary-600 underline underline-offset-4">academic year</Link> and its <Link to="/grades" className="text-secondary-600 underline underline-offset-4">classes</Link> first.</p>)}
          </div>
        ) : (
          <div className="overflow-auto">
            <table className="w-full text-[13px]">
              <thead className="bg-neutral-50 border-b border-neutral-200">
                <tr className="text-left">
                  <th className="p-3 font-semibold text-neutral-700">Student</th>
                  <th className="p-3 font-semibold text-neutral-700">Number</th>
                  <th className="p-3 font-semibold text-neutral-700">Class</th>
                  <th className="p-3 font-semibold text-neutral-700">Status</th>
                  <th className="p-3 font-semibold text-neutral-700">Primary guardian</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-neutral-100">
                {items.map(s => (
                  <tr key={s.id} className="hover:bg-primary-50/50 transition">
                    <td className="p-3">
                      <Link to={`/students/${s.id}`} className="font-medium text-neutral-900 hover:underline">{s.lastName}, {s.firstName}</Link>
                      <div className="text-[12px] text-neutral-500">{capitalise(s.gender) || '—'}{s.dob ? ` • born ${formatDate(s.dob)}` : ''}</div>
                    </td>
                    <td className="p-3 font-mono text-[12px]">{s.studentNumber}</td>
                    <td className="p-3 text-neutral-700">{s.gradeName} {s.streamName}<div className="text-[12px] text-neutral-500">{s.academicYearName}</div></td>
                    <td className="p-3"><Badge tone={s.status === 'active' ? 'success' : 'neutral'}>{label(s.status)}</Badge></td>
                    <td className="p-3 text-neutral-700">{s.primaryGuardianName || <span className="text-neutral-400">None</span>}<div className="text-[12px] text-neutral-500">{s.primaryGuardianPhone}</div></td>
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
        open={!!form}
        onOpenChange={open => !open && setForm(null)}
        size="xl"
        title="Add student"
        description="The student is enrolled in the class you choose. Leave the student number blank to generate one."
        footer={<><Button variant="ghost" onClick={() => setForm(null)}>Cancel</Button><Button loading={saving} type="submit" form="student-form">Add student</Button></>}
      >
        {form && (
          <form id="student-form" onSubmit={createStudent} noValidate className="space-y-4 max-h-[60vh] overflow-y-auto pr-1">
            <FormError message={errors.form} />
            <div className="grid sm:grid-cols-2 gap-4">
              <Input label="First name" id="student-first" required value={form.firstName} onChange={e => setForm({ ...form, firstName: e.target.value })} error={errors.firstName} />
              <Input label="Last name" id="student-last" required value={form.lastName} onChange={e => setForm({ ...form, lastName: e.target.value })} error={errors.lastName} />
              <SelectField label="Gender" id="student-gender" value={form.gender} onChange={e => setForm({ ...form, gender: e.target.value })} error={errors.gender}>
                <option value="">Not recorded</option>
                {GENDERS.map(g => <option key={g.value} value={g.value}>{g.label}</option>)}
              </SelectField>
              <Input label="Date of birth" id="student-dob" type="date" max={todayInput()} value={form.dob} onChange={e => setForm({ ...form, dob: e.target.value })} error={errors.dob} />
              <Input label="Student number" id="student-number" value={form.studentNumber} onChange={e => setForm({ ...form, studentNumber: e.target.value })} error={errors.studentNumber} helpText="Optional" />
              <SelectField label="Admission" id="student-type" value={form.enrolmentType} onChange={e => setForm({ ...form, enrolmentType: e.target.value })}>
                <option value="new">New admission</option>
                <option value="transfer">Transfer from another school</option>
              </SelectField>
            </div>

            <fieldset className="grid sm:grid-cols-3 gap-4 pt-2 border-t border-neutral-100">
              <legend className="text-[13px] font-semibold text-neutral-800 pt-3">Class</legend>
              <SelectField label="Academic year" id="student-year" value={form.yearId} onChange={e => setForm({ ...form, yearId: e.target.value, streamId: '' })}>
                {years.map(y => <option key={y.id} value={y.id}>{y.name}{y.isCurrent ? ' (current)' : ''}</option>)}
              </SelectField>
              <SelectField label="Class" id="student-stream" required value={form.streamId} onChange={e => setForm({ ...form, streamId: e.target.value })} error={errors.streamId}
                helpText={form.yearId && !formClasses.length ? 'This year has no classes yet.' : undefined}>
                <option value="">Choose a class</option>
                {formClasses.map(c => <option key={c.id} value={c.id} disabled={c.full || !c.active}>{c.label}{c.full ? ' – full' : ''}</option>)}
              </SelectField>
              <Input label="Enrolment date" id="student-enrolled" type="date" value={form.enrolmentDate} onChange={e => setForm({ ...form, enrolmentDate: e.target.value })} error={errors.enrolmentDate} />
            </fieldset>

            <fieldset className="space-y-4 pt-2 border-t border-neutral-100">
              <legend className="text-[13px] font-semibold text-neutral-800 pt-3">Primary guardian</legend>
              <CheckboxField label="Add a guardian now (you can also add guardians later)" checked={form.addGuardian} onChange={v => setForm({ ...form, addGuardian: v })} />
              {form.addGuardian && (
                <div className="grid sm:grid-cols-2 gap-4">
                  <Input label="First name" id="guardian-first" required value={form.guardianFirstName} onChange={e => setForm({ ...form, guardianFirstName: e.target.value })} error={errors.guardianFirstName} />
                  <Input label="Last name" id="guardian-last" required value={form.guardianLastName} onChange={e => setForm({ ...form, guardianLastName: e.target.value })} error={errors.guardianLastName} />
                  <Input label="Phone" id="guardian-phone" type="tel" required value={form.guardianPhone} onChange={e => setForm({ ...form, guardianPhone: e.target.value })} error={errors.guardianPhone} placeholder="+263 77 123 4567" />
                  <Input label="Email" id="guardian-email" type="email" value={form.guardianEmail} onChange={e => setForm({ ...form, guardianEmail: e.target.value })} error={errors.guardianEmail} />
                  <SelectField label="Relationship" id="guardian-relationship" value={form.relationship} onChange={e => setForm({ ...form, relationship: e.target.value })}>
                    {RELATIONSHIPS.map(r => <option key={r} value={r}>{capitalise(r)}</option>)}
                  </SelectField>
                </div>
              )}
            </fieldset>
          </form>
        )}
      </Dialog>
    </AppShell>
  );
}
