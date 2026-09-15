import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import AppShell from '../components/layout/AppShell';
import { Button } from '../components/ui/Button';
import { Dialog } from '../components/ui/Dialog';
import { Input } from '../components/ui/Input';
import { Badge, Card, CheckboxField, FormError, LoadState, Notice, SelectField } from '../components/ui/Fields';
import { apiJson } from '../lib/apiClient';
import { useApiErrors } from '../lib/schoolRecords';

// Grades and their streams for one academic year, backed by /api/academic/grades and
// /api/academic/streams. A class is a grade's stream, e.g. "Form 1 Blue".

export default function GradesPage() {
  const apiError = useApiErrors();
  const [years, setYears] = useState(null);
  const [yearId, setYearId] = useState('');
  const [grades, setGrades] = useState([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(null);
  const [notice, setNotice] = useState(null);

  const [gradeForm, setGradeForm] = useState(null);
  const [streamForm, setStreamForm] = useState(null);
  const [errors, setErrors] = useState({});
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const list = await apiJson('/api/academic/years');
        setYears(list);
        setYearId(String((list.find(y => y.isCurrent) || list[0])?.id ?? ''));
        if (!list.length) setLoading(false);
      } catch (error) {
        const message = apiError(error, 'Your role cannot view grades.');
        if (message) setLoadError(message);
        setLoading(false);
      }
    })();
  }, [apiError]);

  const load = useCallback(async () => {
    if (!yearId) return;
    setLoading(true);
    setLoadError(null);
    try {
      setGrades(await apiJson(`/api/academic/grades?academicYearId=${yearId}`));
    } catch (error) {
      const message = apiError(error, 'Your role cannot view grades.');
      if (message) setLoadError(message);
    } finally {
      setLoading(false);
    }
  }, [yearId, apiError]);

  useEffect(() => { load(); }, [load]);

  async function remove(url, success, forbidden) {
    try {
      await apiJson(url, { method: 'DELETE' });
      setNotice({ type: 'success', message: success });
      await load();
    } catch (error) {
      const message = apiError(error, forbidden);
      if (message) setNotice({ type: 'error', message });
    }
  }

  async function saveGrade(event) {
    event.preventDefault();
    const f = gradeForm;
    const next = {};
    if (!f.name.trim()) next.name = 'Name the grade, e.g. Form 1.';
    if (!/^[A-Za-z0-9_-]{1,20}$/.test(f.code.trim())) next.code = 'Code: up to 20 letters, digits, - or _, e.g. F1.';
    if (!(Number(f.levelOrder) >= 0 && Number(f.levelOrder) <= 100)) next.levelOrder = 'Level must be between 0 and 100.';
    setErrors(next);
    if (Object.keys(next).length) return;

    setSaving(true);
    try {
      const body = { name: f.name.trim(), code: f.code.trim().toUpperCase(), levelOrder: Number(f.levelOrder) };
      if (f.id) await apiJson(`/api/academic/grades/${f.id}`, { method: 'PUT', body: { ...body, isActive: f.isActive } });
      else await apiJson('/api/academic/grades', { method: 'POST', body: { ...body, academicYearId: Number(yearId) } });
      setGradeForm(null);
      setNotice({ type: 'success', message: `${body.name} saved.` });
      await load();
    } catch (error) {
      const message = apiError(error, 'Only school admins and head teachers can change grades.');
      if (message) setErrors({ ...error.fieldErrors, form: message });
    } finally {
      setSaving(false);
    }
  }

  async function saveStream(event) {
    event.preventDefault();
    const f = streamForm;
    const next = {};
    if (!f.name.trim()) next.name = 'Name the stream, e.g. Blue.';
    if (!(Number(f.capacity) >= 1 && Number(f.capacity) <= 500)) next.capacity = 'Capacity must be between 1 and 500.';
    setErrors(next);
    if (Object.keys(next).length) return;

    setSaving(true);
    try {
      const body = { name: f.name.trim(), capacity: Number(f.capacity) };
      if (f.id) await apiJson(`/api/academic/streams/${f.id}`, { method: 'PUT', body });
      else await apiJson(`/api/academic/grades/${f.gradeId}/streams`, { method: 'POST', body });
      setStreamForm(null);
      setNotice({ type: 'success', message: `${f.gradeName} ${body.name} saved.` });
      await load();
    } catch (error) {
      const message = apiError(error, 'Only school admins and head teachers can change streams.');
      if (message) setErrors({ ...error.fieldErrors, form: message });
    } finally {
      setSaving(false);
    }
  }

  const year = years?.find(y => String(y.id) === yearId);

  return (
    <AppShell
      title="Grades & streams"
      description="The classes of each academic year. Capacity limits how many students a stream can take."
      breadcrumbs={[{ label: 'Academic' }, { label: 'Grades & streams' }]}
      actions={year && <Button size="sm" onClick={() => { setErrors({}); setGradeForm({ id: null, name: '', code: '', levelOrder: String(grades.length + 1), isActive: true }); }}>Add grade</Button>}
    >
      <Notice notice={notice} onDismiss={() => setNotice(null)} />

      {years && years.length === 0 ? (
        <Card><div className="p-10 text-center text-[14px] text-neutral-700">Grades belong to an academic year. <Link to="/academic-years" className="text-secondary-600 underline underline-offset-4">Add an academic year</Link> first.</div></Card>
      ) : (
        <>
          {years && (
            <div className="mb-4 max-w-xs">
              <SelectField label="Academic year" id="grades-year" value={yearId} onChange={e => setYearId(e.target.value)}>
                {years.map(y => <option key={y.id} value={y.id}>{y.name}{y.isCurrent ? ' (current)' : ''}</option>)}
              </SelectField>
            </div>
          )}

          <LoadState loading={loading} error={loadError} onRetry={load} empty={!grades.length} emptyMessage={`No grades in ${year?.name ?? 'this year'} yet. Add grades such as Form 1, then their streams.`}>
            <div className="grid gap-4 lg:grid-cols-2">
              {grades.map(grade => (
                <Card
                  key={grade.id}
                  title={<span className="flex items-center gap-2">{grade.name} <Badge>{grade.code}</Badge> {!grade.isActive && <Badge tone="warning">Inactive</Badge>}</span>}
                  actions={
                    <>
                      <Button variant="secondary" size="sm" onClick={() => { setErrors({}); setStreamForm({ id: null, gradeId: grade.id, gradeName: grade.name, name: '', capacity: '40' }); }}>Add stream</Button>
                      <Button variant="ghost" size="sm" onClick={() => { setErrors({}); setGradeForm({ id: grade.id, name: grade.name, code: grade.code, levelOrder: String(grade.levelOrder), isActive: grade.isActive }); }}>Edit</Button>
                      <Button variant="ghost" size="sm" onClick={() => window.confirm(`Delete ${grade.name}?`) && remove(`/api/academic/grades/${grade.id}`, `${grade.name} deleted.`, 'Only school admins can delete grades.')}>Delete</Button>
                    </>
                  }
                >
                  {grade.streams.length === 0 ? (
                    <p className="p-5 text-[13px] text-neutral-600">No streams yet. Students are enrolled into streams.</p>
                  ) : (
                    <ul className="divide-y divide-neutral-100">
                      {grade.streams.map(stream => {
                        const pct = Math.min(100, Math.round((stream.enrolledCount / stream.capacity) * 100));
                        return (
                          <li key={stream.id} className="px-4 py-3 flex flex-wrap items-center gap-3">
                            <div className="flex-1 min-w-[10rem]">
                              <Link to={`/students?streamId=${stream.id}&academicYearId=${yearId}`} className="text-[14px] font-medium text-neutral-900 hover:underline">{stream.displayName}</Link>
                              <div className="mt-1.5 h-1.5 rounded-full bg-neutral-100 overflow-hidden" aria-hidden="true">
                                <div className={`h-full ${pct >= 100 ? 'bg-danger-500' : pct >= 90 ? 'bg-warning-500' : 'bg-success-500'}`} style={{ width: `${pct}%` }} />
                              </div>
                            </div>
                            <span className="text-[12px] text-neutral-600">{stream.enrolledCount} of {stream.capacity} places</span>
                            <Button variant="ghost" size="sm" onClick={() => { setErrors({}); setStreamForm({ id: stream.id, gradeId: grade.id, gradeName: grade.name, name: stream.name, capacity: String(stream.capacity) }); }}>Edit</Button>
                            <Button variant="ghost" size="sm" onClick={() => window.confirm(`Delete ${stream.displayName}?`) && remove(`/api/academic/streams/${stream.id}`, `${stream.displayName} deleted.`, 'Only school admins can delete streams.')}>Delete</Button>
                          </li>
                        );
                      })}
                    </ul>
                  )}
                </Card>
              ))}
            </div>
          </LoadState>
        </>
      )}

      <Dialog
        open={!!gradeForm}
        onOpenChange={open => !open && setGradeForm(null)}
        title={gradeForm?.id ? 'Edit grade' : `Add grade to ${year?.name ?? ''}`}
        description="Level orders grades from youngest to oldest; moving a student to a higher level in a later year counts as a promotion."
        footer={<><Button variant="ghost" onClick={() => setGradeForm(null)}>Cancel</Button><Button loading={saving} type="submit" form="grade-form">Save</Button></>}
      >
        {gradeForm && (
          <form id="grade-form" onSubmit={saveGrade} noValidate className="space-y-4">
            <FormError message={errors.form} />
            <Input label="Name" id="grade-name" required value={gradeForm.name} onChange={e => setGradeForm({ ...gradeForm, name: e.target.value })} error={errors.name} placeholder="Form 1" />
            <div className="grid sm:grid-cols-2 gap-4">
              <Input label="Code" id="grade-code" required value={gradeForm.code} onChange={e => setGradeForm({ ...gradeForm, code: e.target.value.toUpperCase() })} error={errors.code} placeholder="F1" />
              <Input label="Level" id="grade-level" type="number" min="0" max="100" required value={gradeForm.levelOrder} onChange={e => setGradeForm({ ...gradeForm, levelOrder: e.target.value })} error={errors.levelOrder} />
            </div>
            {gradeForm.id && <CheckboxField label="Taking enrolments (untick to mark the grade inactive)" checked={gradeForm.isActive} onChange={v => setGradeForm({ ...gradeForm, isActive: v })} />}
          </form>
        )}
      </Dialog>

      <Dialog
        open={!!streamForm}
        onOpenChange={open => !open && setStreamForm(null)}
        title={streamForm?.id ? `Edit ${streamForm.gradeName} stream` : `Add stream to ${streamForm?.gradeName ?? ''}`}
        footer={<><Button variant="ghost" onClick={() => setStreamForm(null)}>Cancel</Button><Button loading={saving} type="submit" form="stream-form">Save</Button></>}
      >
        {streamForm && (
          <form id="stream-form" onSubmit={saveStream} noValidate className="space-y-4">
            <FormError message={errors.form} />
            <Input label="Name" id="stream-name" required value={streamForm.name} onChange={e => setStreamForm({ ...streamForm, name: e.target.value })} error={errors.name} placeholder="Blue" />
            <Input label="Capacity" id="stream-capacity" type="number" min="1" max="500" required value={streamForm.capacity} onChange={e => setStreamForm({ ...streamForm, capacity: e.target.value })} error={errors.capacity} />
          </form>
        )}
      </Dialog>
    </AppShell>
  );
}
