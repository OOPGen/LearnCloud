import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom';
import AppShell from '../components/layout/AppShell';
import { Button } from '../components/ui/Button';
import { Dialog } from '../components/ui/Dialog';
import { Input } from '../components/ui/Input';
import { Badge, Card, CheckboxField, FormError, LoadState, Notice, SelectField } from '../components/ui/Fields';
import { apiJson } from '../lib/apiClient';
import { EXIT_REASONS, GENDERS, RELATIONSHIPS, capitalise, classOptions, formatDate, label, toDateInput, todayInput, useApiErrors } from '../lib/schoolRecords';

// One student: details, current class, enrolment history and guardians.
// Backed by /api/students/{id}, /enrolments, /exit and /guardians.

const WRITE_FORBIDDEN = 'Only school admins, head teachers and registrars can change student records.';

export default function StudentDetailPage() {
  const { id } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  const apiError = useApiErrors();

  const [student, setStudent] = useState(null);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(null);
  const [notice, setNotice] = useState(location.state?.notice ? { type: 'success', message: location.state.notice } : null);

  const [years, setYears] = useState([]);
  const [gradesByYear, setGradesByYear] = useState({});

  const [dialog, setDialog] = useState(null); // { kind: 'details' | 'class' | 'exit' | 'link' | 'editLink', ...form }
  const [errors, setErrors] = useState({});
  const [saving, setSaving] = useState(false);
  const [guardianResults, setGuardianResults] = useState([]);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setStudent(await apiJson(`/api/students/${id}`));
    } catch (error) {
      const message = apiError(error, 'Your role cannot view student records.');
      if (message) setLoadError(message);
    } finally {
      setLoading(false);
    }
  }, [id, apiError]);

  useEffect(() => { load(); }, [load]);

  const classYearId = dialog?.kind === 'class' ? dialog.yearId : '';
  useEffect(() => {
    if (!classYearId || gradesByYear[classYearId]) return;
    apiJson(`/api/academic/grades?academicYearId=${classYearId}`)
      .then(grades => setGradesByYear(current => ({ ...current, [classYearId]: grades })))
      .catch(() => {});
  }, [classYearId, gradesByYear]);

  const guardianSearch = dialog?.kind === 'link' && dialog.mode === 'existing' ? dialog.search : null;
  useEffect(() => {
    if (guardianSearch === null) return undefined;
    const timer = setTimeout(() => {
      apiJson(`/api/guardians?pageSize=10&search=${encodeURIComponent(guardianSearch.trim())}`)
        .then(r => setGuardianResults(r.items))
        .catch(() => setGuardianResults([]));
    }, 250);
    return () => clearTimeout(timer);
  }, [guardianSearch]);

  const classes = useMemo(() => classOptions(gradesByYear[classYearId] || []), [gradesByYear, classYearId]);

  async function openClassDialog() {
    let list = years;
    if (!list.length) {
      try { list = await apiJson('/api/academic/years'); setYears(list); } catch { list = []; }
    }
    const placementYear = student.currentPlacement?.academicYearId;
    const defaultYear = list.find(y => y.id === placementYear) || list.find(y => y.isCurrent) || list[0];
    setErrors({});
    setDialog({ kind: 'class', yearId: defaultYear ? String(defaultYear.id) : '', streamId: '', effectiveDate: todayInput() });
  }

  function set(values) {
    setDialog(current => ({ ...current, ...values }));
  }

  async function submit(event, request, success) {
    event.preventDefault();
    setSaving(true);
    try {
      const updated = await request();
      if (updated && updated.enrolments) setStudent(updated); else await load();
      setDialog(null);
      setNotice({ type: 'success', message: success });
    } catch (error) {
      const message = apiError(error, WRITE_FORBIDDEN);
      if (message) setErrors({ ...error.fieldErrors, form: message });
    } finally {
      setSaving(false);
    }
  }

  function saveDetails(event) {
    const f = dialog;
    const next = {};
    if (!f.firstName.trim()) next.firstName = 'Enter the first name.';
    if (!f.lastName.trim()) next.lastName = 'Enter the last name.';
    if (!f.studentNumber.trim()) next.studentNumber = 'Enter the student number.';
    if (f.dob && f.dob > todayInput()) next.dob = 'Date of birth cannot be in the future.';
    setErrors(next);
    if (Object.keys(next).length) { event.preventDefault(); return; }
    submit(event, () => apiJson(`/api/students/${id}`, {
      method: 'PUT',
      body: { firstName: f.firstName.trim(), lastName: f.lastName.trim(), gender: f.gender || null, dob: f.dob || null, nationalId: f.nationalId.trim() || null, studentNumber: f.studentNumber.trim() },
    }), 'Details saved.');
  }

  function saveClass(event) {
    if (!dialog.streamId) { event.preventDefault(); setErrors({ streamId: 'Choose a class.' }); return; }
    const chosen = classes.find(c => String(c.id) === dialog.streamId);
    submit(event, () => apiJson(`/api/students/${id}/enrolments`, {
      method: 'POST',
      body: { streamId: Number(dialog.streamId), effectiveDate: dialog.effectiveDate || null },
    }), `${student.firstName} is now in ${chosen?.label.replace(/ \(.*\)$/, '') ?? 'the new class'}.`);
  }

  function saveExit(event) {
    submit(event, () => apiJson(`/api/students/${id}/exit`, {
      method: 'POST', body: { reason: dialog.reason, exitDate: dialog.exitDate || null },
    }), `${student.firstName}'s enrolment has ended.`);
  }

  function saveLink(event) {
    const f = dialog;
    const next = {};
    if (f.mode === 'existing' && !f.guardianId) next.guardianId = 'Choose a guardian.';
    if (f.mode === 'new') {
      if (!f.firstName.trim()) next.firstName = "Enter the guardian's first name.";
      if (!f.lastName.trim()) next.lastName = "Enter the guardian's last name.";
      if (!/^\+?[0-9 ()-]{7,20}$/.test(f.phone.trim())) next.phone = 'Enter a phone number, e.g. +263 77 123 4567.';
    }
    setErrors(next);
    if (Object.keys(next).length) { event.preventDefault(); return; }
    submit(event, () => apiJson(`/api/students/${id}/guardians`, {
      method: 'POST',
      body: {
        guardianId: f.mode === 'existing' ? Number(f.guardianId) : null,
        newGuardian: f.mode === 'new' ? { firstName: f.firstName.trim(), lastName: f.lastName.trim(), phone: f.phone.trim(), email: f.email.trim() || null } : null,
        relationshipType: f.relationshipType,
        isPrimaryContact: f.isPrimaryContact, isBillingContact: f.isBillingContact, isEmergencyContact: f.isEmergencyContact, canPickup: f.canPickup,
      },
    }), 'Guardian linked.');
  }

  function saveEditLink(event) {
    const f = dialog;
    submit(event, () => apiJson(`/api/students/${id}/guardians/${f.linkId}`, {
      method: 'PUT',
      body: { relationshipType: f.relationshipType, isPrimaryContact: f.isPrimaryContact, isBillingContact: f.isBillingContact, isEmergencyContact: f.isEmergencyContact, canPickup: f.canPickup },
    }), 'Guardian updated.');
  }

  async function unlink(link) {
    if (!window.confirm(`Remove ${link.firstName} ${link.lastName} as a guardian of ${student.firstName}? The guardian's record is kept.`)) return;
    try {
      await apiJson(`/api/students/${id}/guardians/${link.linkId}`, { method: 'DELETE' });
      setNotice({ type: 'success', message: `${link.firstName} ${link.lastName} unlinked.` });
      await load();
    } catch (error) {
      const message = apiError(error, WRITE_FORBIDDEN);
      if (message) setNotice({ type: 'error', message });
    }
  }

  async function deleteStudent() {
    if (!window.confirm(`Delete ${student.firstName} ${student.lastName}? Use "Record leaving" for a student who has left; delete only records created by mistake.`)) return;
    try {
      await apiJson(`/api/students/${id}`, { method: 'DELETE' });
      navigate('/students', { replace: true });
    } catch (error) {
      const message = apiError(error, 'Only school admins can delete student records.');
      if (message) setNotice({ type: 'error', message });
    }
  }

  const placement = student?.currentPlacement;
  const fullName = student ? `${student.firstName} ${student.lastName}` : 'Student';

  return (
    <AppShell
      title={fullName}
      description={student ? `Student number ${student.studentNumber}` : undefined}
      breadcrumbs={[{ label: 'People' }, { label: 'Students', path: '/students' }, { label: fullName }]}
      actions={student && (
        <>
          <Button variant="secondary" size="sm" onClick={() => { setErrors({}); setDialog({ kind: 'details', firstName: student.firstName, lastName: student.lastName, gender: student.gender || '', dob: toDateInput(student.dob), nationalId: student.nationalId || '', studentNumber: student.studentNumber }); }}>Edit details</Button>
          <Button variant="ghost" size="sm" onClick={deleteStudent}>Delete</Button>
        </>
      )}
    >
      <Notice notice={notice} onDismiss={() => setNotice(null)} />

      <LoadState loading={loading && !student} error={loadError} onRetry={load}>
        {student && (
          <div className="grid gap-4 lg:grid-cols-3">
            <div className="space-y-4 lg:col-span-2">
              <Card
                title="Current class"
                actions={placement ? (
                  <>
                    <Button variant="secondary" size="sm" onClick={openClassDialog}>Change class or promote</Button>
                    <Button variant="ghost" size="sm" onClick={() => { setErrors({}); setDialog({ kind: 'exit', reason: 'withdrawn', exitDate: todayInput() }); }}>Record leaving</Button>
                  </>
                ) : (
                  <Button size="sm" onClick={openClassDialog}>Readmit</Button>
                )}
              >
                <div className="p-4">
                  {placement ? (
                    <dl className="grid grid-cols-2 sm:grid-cols-4 gap-4 text-[13px]">
                      <div><dt className="text-neutral-500">Class</dt><dd className="font-semibold text-neutral-900">{placement.gradeName} {placement.streamName}</dd></div>
                      <div><dt className="text-neutral-500">Academic year</dt><dd className="text-neutral-900">{placement.academicYearName}</dd></div>
                      <div><dt className="text-neutral-500">Joined in</dt><dd className="text-neutral-900">{placement.termName}</dd></div>
                      <div><dt className="text-neutral-500">Since</dt><dd className="text-neutral-900">{formatDate(placement.enrolmentDate)}</dd></div>
                    </dl>
                  ) : (
                    <p className="text-[13px] text-neutral-700">{student.firstName} has no current enrolment ({label(student.status)}).</p>
                  )}
                </div>
              </Card>

              <Card
                title="Guardians"
                actions={<Button variant="secondary" size="sm" onClick={() => { setErrors({}); setGuardianResults([]); setDialog({ kind: 'link', mode: 'new', search: '', guardianId: '', firstName: '', lastName: '', phone: '', email: '', relationshipType: 'mother', isPrimaryContact: student.guardians.length === 0, isBillingContact: student.guardians.length === 0, isEmergencyContact: true, canPickup: true }); }}>Add guardian</Button>}
              >
                {student.guardians.length === 0 ? (
                  <p className="p-4 text-[13px] text-neutral-600">No guardians linked yet.</p>
                ) : (
                  <ul className="divide-y divide-neutral-100">
                    {student.guardians.map(g => (
                      <li key={g.linkId} className="px-4 py-3 flex flex-wrap items-center gap-3">
                        <div className="flex-1 min-w-[12rem]">
                          <div className="text-[14px] font-medium text-neutral-900">{g.firstName} {g.lastName} <span className="text-[12px] font-normal text-neutral-500">({capitalise(g.relationshipType)})</span></div>
                          <div className="text-[12px] text-neutral-600">{g.phone}{g.email ? ` • ${g.email}` : ''}</div>
                          <div className="mt-1 flex flex-wrap gap-1">
                            {g.isPrimaryContact && <Badge tone="primary">Primary contact</Badge>}
                            {g.isBillingContact && <Badge>Billing</Badge>}
                            {g.isEmergencyContact && <Badge>Emergency</Badge>}
                            {g.canPickup ? <Badge tone="success">May collect</Badge> : <Badge tone="warning">May not collect</Badge>}
                          </div>
                        </div>
                        <Button variant="ghost" size="sm" onClick={() => { setErrors({}); setDialog({ kind: 'editLink', linkId: g.linkId, name: `${g.firstName} ${g.lastName}`, relationshipType: g.relationshipType, isPrimaryContact: g.isPrimaryContact, isBillingContact: g.isBillingContact, isEmergencyContact: g.isEmergencyContact, canPickup: g.canPickup }); }}>Edit</Button>
                        <Button variant="ghost" size="sm" onClick={() => unlink(g)}>Unlink</Button>
                      </li>
                    ))}
                  </ul>
                )}
              </Card>

              <Card title="Enrolment history">
                <div className="overflow-auto">
                  <table className="w-full text-[13px]">
                    <thead className="bg-neutral-50 border-b border-neutral-200">
                      <tr className="text-left">
                        <th className="p-3 font-semibold text-neutral-700">Class</th>
                        <th className="p-3 font-semibold text-neutral-700">Year and term</th>
                        <th className="p-3 font-semibold text-neutral-700">From</th>
                        <th className="p-3 font-semibold text-neutral-700">To</th>
                        <th className="p-3 font-semibold text-neutral-700">Outcome</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-neutral-100">
                      {student.enrolments.map(e => (
                        <tr key={e.id}>
                          <td className="p-3 font-medium text-neutral-900">{e.gradeName} {e.streamName}<div className="text-[12px] font-normal text-neutral-500">{label(e.enrolmentType)}</div></td>
                          <td className="p-3 text-neutral-700">{e.academicYearName}, {e.termName}</td>
                          <td className="p-3 text-neutral-700">{formatDate(e.enrolmentDate)}</td>
                          <td className="p-3 text-neutral-700">{e.isCurrent ? '—' : formatDate(e.exitDate)}</td>
                          <td className="p-3">{e.isCurrent ? <Badge tone="success">Current</Badge> : <Badge>{e.enrolmentStatus === 'enrolled' ? 'Moved class' : label(e.enrolmentStatus)}</Badge>}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </Card>
            </div>

            <Card title="Details" className="h-fit">
              <dl className="p-4 space-y-3 text-[13px]">
                <div className="flex justify-between gap-4"><dt className="text-neutral-500">Status</dt><dd><Badge tone={student.status === 'active' ? 'success' : 'neutral'}>{label(student.status)}</Badge></dd></div>
                <div className="flex justify-between gap-4"><dt className="text-neutral-500">Student number</dt><dd className="font-mono">{student.studentNumber}</dd></div>
                <div className="flex justify-between gap-4"><dt className="text-neutral-500">Gender</dt><dd>{capitalise(student.gender) || '—'}</dd></div>
                <div className="flex justify-between gap-4"><dt className="text-neutral-500">Date of birth</dt><dd>{formatDate(student.dob)}</dd></div>
                <div className="flex justify-between gap-4"><dt className="text-neutral-500">National ID / birth entry</dt><dd>{student.nationalId || '—'}</dd></div>
                <div className="flex justify-between gap-4"><dt className="text-neutral-500">Record created</dt><dd>{formatDate(student.createdAt)}</dd></div>
              </dl>
            </Card>
          </div>
        )}
      </LoadState>

      <Dialog open={dialog?.kind === 'details'} onOpenChange={open => !open && setDialog(null)} size="lg" title="Edit student details"
        footer={<><Button variant="ghost" onClick={() => setDialog(null)}>Cancel</Button><Button loading={saving} type="submit" form="details-form">Save</Button></>}>
        {dialog?.kind === 'details' && (
          <form id="details-form" onSubmit={saveDetails} noValidate className="space-y-4">
            <FormError message={errors.form} />
            <div className="grid sm:grid-cols-2 gap-4">
              <Input label="First name" id="d-first" required value={dialog.firstName} onChange={e => set({ firstName: e.target.value })} error={errors.firstName} />
              <Input label="Last name" id="d-last" required value={dialog.lastName} onChange={e => set({ lastName: e.target.value })} error={errors.lastName} />
              <SelectField label="Gender" id="d-gender" value={dialog.gender} onChange={e => set({ gender: e.target.value })}>
                <option value="">Not recorded</option>
                {GENDERS.map(g => <option key={g.value} value={g.value}>{g.label}</option>)}
              </SelectField>
              <Input label="Date of birth" id="d-dob" type="date" max={todayInput()} value={dialog.dob} onChange={e => set({ dob: e.target.value })} error={errors.dob} />
              <Input label="Student number" id="d-number" required value={dialog.studentNumber} onChange={e => set({ studentNumber: e.target.value })} error={errors.studentNumber} />
              <Input label="National ID / birth entry" id="d-national" value={dialog.nationalId} onChange={e => set({ nationalId: e.target.value })} error={errors.nationalId} />
            </div>
          </form>
        )}
      </Dialog>

      <Dialog open={dialog?.kind === 'class'} onOpenChange={open => !open && setDialog(null)} size="lg"
        title={placement ? 'Change class or promote' : `Readmit ${student?.firstName ?? ''}`}
        description={placement ? 'A class in the same year moves the student; a class in a later year promotes them (or records a repeat at the same level). The history keeps every enrolment.' : 'Choose the class the student returns to.'}
        footer={<><Button variant="ghost" onClick={() => setDialog(null)}>Cancel</Button><Button loading={saving} type="submit" form="class-form">Save</Button></>}>
        {dialog?.kind === 'class' && (
          <form id="class-form" onSubmit={saveClass} noValidate className="space-y-4">
            <FormError message={errors.form} />
            <SelectField label="Academic year" id="c-year" value={dialog.yearId} onChange={e => set({ yearId: e.target.value, streamId: '' })}>
              {years.map(y => <option key={y.id} value={y.id}>{y.name}{y.isCurrent ? ' (current)' : ''}</option>)}
            </SelectField>
            <SelectField label="Class" id="c-stream" required value={dialog.streamId} onChange={e => set({ streamId: e.target.value })} error={errors.streamId}
              helpText={dialog.yearId && !classes.length ? 'This year has no classes yet.' : undefined}>
              <option value="">Choose a class</option>
              {classes.map(c => <option key={c.id} value={c.id} disabled={c.full || !c.active || c.id === placement?.streamId}>{c.label}{c.id === placement?.streamId ? ' – current' : c.full ? ' – full' : ''}</option>)}
            </SelectField>
            <Input label="Takes effect" id="c-date" type="date" value={dialog.effectiveDate} onChange={e => set({ effectiveDate: e.target.value })} error={errors.effectiveDate} />
          </form>
        )}
      </Dialog>

      <Dialog open={dialog?.kind === 'exit'} onOpenChange={open => !open && setDialog(null)} title={`Record ${student?.firstName ?? ''} leaving`}
        description="Ends the current enrolment. The student's record and history are kept, and they can be readmitted later."
        footer={<><Button variant="ghost" onClick={() => setDialog(null)}>Cancel</Button><Button variant="destructive" loading={saving} type="submit" form="exit-form">End enrolment</Button></>}>
        {dialog?.kind === 'exit' && (
          <form id="exit-form" onSubmit={saveExit} noValidate className="space-y-4">
            <FormError message={errors.form} />
            <SelectField label="Reason" id="x-reason" value={dialog.reason} onChange={e => set({ reason: e.target.value })}>
              {EXIT_REASONS.map(r => <option key={r.value} value={r.value}>{r.label}</option>)}
            </SelectField>
            <Input label="Last day" id="x-date" type="date" value={dialog.exitDate} onChange={e => set({ exitDate: e.target.value })} error={errors.exitDate} />
          </form>
        )}
      </Dialog>

      <Dialog open={dialog?.kind === 'link'} onOpenChange={open => !open && setDialog(null)} size="lg" title="Add guardian"
        footer={<><Button variant="ghost" onClick={() => setDialog(null)}>Cancel</Button><Button loading={saving} type="submit" form="link-form">Link guardian</Button></>}>
        {dialog?.kind === 'link' && (
          <form id="link-form" onSubmit={saveLink} noValidate className="space-y-4">
            <FormError message={errors.form} />
            <div role="radiogroup" aria-label="Guardian" className="flex gap-4 text-[13px]">
              <label className="flex items-center gap-2"><input type="radio" checked={dialog.mode === 'new'} onChange={() => set({ mode: 'new' })} /> New guardian</label>
              <label className="flex items-center gap-2"><input type="radio" checked={dialog.mode === 'existing'} onChange={() => set({ mode: 'existing' })} /> Existing guardian (e.g. a sibling's parent)</label>
            </div>

            {dialog.mode === 'new' ? (
              <div className="grid sm:grid-cols-2 gap-4">
                <Input label="First name" id="l-first" required value={dialog.firstName} onChange={e => set({ firstName: e.target.value })} error={errors.firstName} />
                <Input label="Last name" id="l-last" required value={dialog.lastName} onChange={e => set({ lastName: e.target.value })} error={errors.lastName} />
                <Input label="Phone" id="l-phone" type="tel" required value={dialog.phone} onChange={e => set({ phone: e.target.value })} error={errors.phone} placeholder="+263 77 123 4567" />
                <Input label="Email" id="l-email" type="email" value={dialog.email} onChange={e => set({ email: e.target.value })} error={errors.email} />
              </div>
            ) : (
              <div className="space-y-2">
                <Input label="Search guardians" id="l-search" value={dialog.search} onChange={e => set({ search: e.target.value, guardianId: '' })} placeholder="Name or phone" error={errors.guardianId} />
                <ul className="max-h-48 overflow-y-auto divide-y divide-neutral-100 rounded-xl border border-neutral-200">
                  {guardianResults.length === 0 && <li className="p-3 text-[13px] text-neutral-500">No guardians found.</li>}
                  {guardianResults.map(g => (
                    <li key={g.id}>
                      <label className="flex items-center gap-3 p-3 text-[13px] cursor-pointer hover:bg-neutral-50">
                        <input type="radio" name="guardian" checked={dialog.guardianId === String(g.id)} onChange={() => set({ guardianId: String(g.id) })} />
                        <span className="flex-1">{g.firstName} {g.lastName} <span className="text-neutral-500">• {g.phone}</span></span>
                        <span className="text-[12px] text-neutral-500">{g.studentCount} student{g.studentCount === 1 ? '' : 's'}</span>
                      </label>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            <LinkFlags dialog={dialog} set={set} />
          </form>
        )}
      </Dialog>

      <Dialog open={dialog?.kind === 'editLink'} onOpenChange={open => !open && setDialog(null)} title={`Edit ${dialog?.name ?? 'guardian'}`}
        footer={<><Button variant="ghost" onClick={() => setDialog(null)}>Cancel</Button><Button loading={saving} type="submit" form="edit-link-form">Save</Button></>}>
        {dialog?.kind === 'editLink' && (
          <form id="edit-link-form" onSubmit={saveEditLink} noValidate className="space-y-4">
            <FormError message={errors.form} />
            <LinkFlags dialog={dialog} set={set} />
            <p className="text-[12px] text-neutral-500">To change the guardian's name or phone, use the <Link to="/guardians" className="text-secondary-600 underline underline-offset-4">Guardians</Link> page.</p>
          </form>
        )}
      </Dialog>
    </AppShell>
  );
}

function LinkFlags({ dialog, set }) {
  return (
    <div className="space-y-3 pt-2 border-t border-neutral-100">
      <SelectField label="Relationship" id="link-relationship" value={dialog.relationshipType} onChange={e => set({ relationshipType: e.target.value })}>
        {RELATIONSHIPS.map(r => <option key={r} value={r}>{capitalise(r)}</option>)}
      </SelectField>
      <div className="grid sm:grid-cols-2 gap-2">
        <CheckboxField label="Primary contact" checked={dialog.isPrimaryContact} onChange={v => set({ isPrimaryContact: v })} />
        <CheckboxField label="Receives fee statements" checked={dialog.isBillingContact} onChange={v => set({ isBillingContact: v })} />
        <CheckboxField label="Emergency contact" checked={dialog.isEmergencyContact} onChange={v => set({ isEmergencyContact: v })} />
        <CheckboxField label="May collect the student" checked={dialog.canPickup} onChange={v => set({ canPickup: v })} />
      </div>
      {dialog.isPrimaryContact && <p className="text-[12px] text-neutral-500">A student has one primary contact; this replaces the current one.</p>}
    </div>
  );
}
