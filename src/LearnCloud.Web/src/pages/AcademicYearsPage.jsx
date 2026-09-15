import { useCallback, useEffect, useState } from 'react';
import AppShell from '../components/layout/AppShell';
import { Button } from '../components/ui/Button';
import { Dialog } from '../components/ui/Dialog';
import { Input } from '../components/ui/Input';
import { Badge, Card, CheckboxField, FormError, LoadState, Notice } from '../components/ui/Fields';
import { apiJson } from '../lib/apiClient';
import { formatDate, toDateInput, useApiErrors } from '../lib/schoolRecords';

// Academic years and their terms, backed by /api/academic/years and /api/academic/terms.

const EMPTY_YEAR = { id: null, name: '', startDate: '', endDate: '', isCurrent: false };
const EMPTY_TERM = { id: null, yearId: null, name: '', termNumber: '1', startDate: '', endDate: '', isCurrent: false };

export default function AcademicYearsPage() {
  const apiError = useApiErrors();
  const [years, setYears] = useState([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(null);
  const [notice, setNotice] = useState(null);

  const [yearForm, setYearForm] = useState(null);
  const [termForm, setTermForm] = useState(null);
  const [errors, setErrors] = useState({});
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setLoadError(null);
    try {
      setYears(await apiJson('/api/academic/years'));
    } catch (error) {
      const message = apiError(error, 'Your role cannot view the academic calendar.');
      if (message) setLoadError(message);
    } finally {
      setLoading(false);
    }
  }, [apiError]);

  useEffect(() => { load(); }, [load]);

  async function run(action, success, forbidden) {
    try {
      await action();
      setNotice({ type: 'success', message: success });
      await load();
      return true;
    } catch (error) {
      const message = apiError(error, forbidden);
      if (message) setNotice({ type: 'error', message });
      return false;
    }
  }

  async function saveYear(event) {
    event.preventDefault();
    const f = yearForm;
    const next = {};
    if (!f.name.trim()) next.name = 'Name the year, e.g. 2026.';
    if (!f.startDate) next.startDate = 'Enter the start date.';
    if (!f.endDate || f.endDate <= f.startDate) next.endDate = 'The year must end after it starts.';
    setErrors(next);
    if (Object.keys(next).length) return;

    setSaving(true);
    try {
      const body = { name: f.name.trim(), startDate: f.startDate, endDate: f.endDate, isCurrent: f.isCurrent };
      if (f.id) await apiJson(`/api/academic/years/${f.id}`, { method: 'PUT', body });
      else await apiJson('/api/academic/years', { method: 'POST', body });
      setYearForm(null);
      setNotice({ type: 'success', message: `${body.name} saved.` });
      await load();
    } catch (error) {
      const message = apiError(error, 'Only school admins and head teachers can change the calendar.');
      if (message) setErrors({ ...error.fieldErrors, form: message });
    } finally {
      setSaving(false);
    }
  }

  async function saveTerm(event) {
    event.preventDefault();
    const f = termForm;
    const next = {};
    if (!f.name.trim()) next.name = 'Name the term, e.g. Term 1.';
    if (!(Number(f.termNumber) >= 1 && Number(f.termNumber) <= 6)) next.termNumber = 'Term number must be between 1 and 6.';
    if (!f.startDate) next.startDate = 'Enter the start date.';
    if (!f.endDate || f.endDate <= f.startDate) next.endDate = 'The term must end after it starts.';
    setErrors(next);
    if (Object.keys(next).length) return;

    setSaving(true);
    try {
      const body = { name: f.name.trim(), termNumber: Number(f.termNumber), startDate: f.startDate, endDate: f.endDate, isCurrent: f.isCurrent };
      if (f.id) await apiJson(`/api/academic/terms/${f.id}`, { method: 'PUT', body });
      else await apiJson(`/api/academic/years/${f.yearId}/terms`, { method: 'POST', body });
      setTermForm(null);
      setNotice({ type: 'success', message: `${body.name} saved.` });
      await load();
    } catch (error) {
      const message = apiError(error, 'Only school admins and head teachers can change the calendar.');
      if (message) setErrors({ ...error.fieldErrors, form: message });
    } finally {
      setSaving(false);
    }
  }

  function openYear(year) {
    setErrors({});
    setYearForm(year
      ? { id: year.id, name: year.name, startDate: toDateInput(year.startDate), endDate: toDateInput(year.endDate), isCurrent: year.isCurrent }
      : { ...EMPTY_YEAR, isCurrent: years.length === 0 });
  }

  function openTerm(year, term) {
    setErrors({});
    setTermForm(term
      ? { id: term.id, yearId: year.id, name: term.name, termNumber: String(term.termNumber), startDate: toDateInput(term.startDate), endDate: toDateInput(term.endDate), isCurrent: term.isCurrent, yearName: year.name }
      : { ...EMPTY_TERM, yearId: year.id, yearName: year.name, termNumber: String(year.terms.length + 1), name: `Term ${year.terms.length + 1}` });
  }

  const calendarForbidden = 'Only school admins and head teachers can change the calendar.';

  return (
    <AppShell
      title="Academic years"
      description="The school calendar: each academic year and its terms. The current year and term are used when students are enrolled."
      breadcrumbs={[{ label: 'Academic' }, { label: 'Academic years' }]}
      actions={<Button size="sm" onClick={() => openYear(null)}>Add academic year</Button>}
    >
      <Notice notice={notice} onDismiss={() => setNotice(null)} />

      <LoadState loading={loading} error={loadError} onRetry={load} empty={!years.length}
        emptyMessage={<>No academic years yet. <button className="text-secondary-600 underline underline-offset-4" onClick={() => openYear(null)}>Add your first year</button>, then its terms.</>}>
        <div className="space-y-4">
          {years.map(year => (
            <Card
              key={year.id}
              title={<span className="flex items-center gap-2">{year.name} {year.isCurrent && <Badge tone="primary">Current</Badge>}</span>}
              actions={
                <>
                  <span className="text-[12px] text-neutral-500">{formatDate(year.startDate)} – {formatDate(year.endDate)}</span>
                  {!year.isCurrent && <Button variant="secondary" size="sm" onClick={() => run(() => apiJson(`/api/academic/years/${year.id}/set-current`, { method: 'POST' }), `${year.name} is now the current year.`, calendarForbidden)}>Make current</Button>}
                  <Button variant="secondary" size="sm" onClick={() => openYear(year)}>Edit</Button>
                  <Button variant="ghost" size="sm" onClick={() => window.confirm(`Delete academic year ${year.name}?`) && run(() => apiJson(`/api/academic/years/${year.id}`, { method: 'DELETE' }), `${year.name} deleted.`, 'Only school admins can delete academic years.')}>Delete</Button>
                </>
              }
            >
              {year.terms.length === 0 ? (
                <div className="p-6 text-center text-[13px] text-neutral-600">
                  No terms yet. <button className="text-secondary-600 underline underline-offset-4" onClick={() => openTerm(year, null)}>Add a term</button>
                </div>
              ) : (
                <div className="overflow-auto">
                  <table className="w-full text-[13px]">
                    <thead className="bg-neutral-50 border-b border-neutral-200">
                      <tr className="text-left">
                        <th className="p-3 font-semibold text-neutral-700">Term</th>
                        <th className="p-3 font-semibold text-neutral-700">Starts</th>
                        <th className="p-3 font-semibold text-neutral-700">Ends</th>
                        <th className="p-3"><span className="sr-only">Actions</span></th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-neutral-100">
                      {year.terms.map(term => (
                        <tr key={term.id}>
                          <td className="p-3 font-medium text-neutral-900"><span className="flex items-center gap-2">{term.name} {term.isCurrent && <Badge tone="primary">Current</Badge>}</span></td>
                          <td className="p-3 text-neutral-700">{formatDate(term.startDate)}</td>
                          <td className="p-3 text-neutral-700">{formatDate(term.endDate)}</td>
                          <td className="p-3 text-right whitespace-nowrap">
                            {!term.isCurrent && <Button variant="ghost" size="sm" onClick={() => run(() => apiJson(`/api/academic/terms/${term.id}/set-current`, { method: 'POST' }), `${term.name} of ${year.name} is now the current term.`, calendarForbidden)}>Make current</Button>}
                            <Button variant="ghost" size="sm" onClick={() => openTerm(year, term)}>Edit</Button>
                            <Button variant="ghost" size="sm" onClick={() => window.confirm(`Delete ${term.name} of ${year.name}?`) && run(() => apiJson(`/api/academic/terms/${term.id}`, { method: 'DELETE' }), `${term.name} deleted.`, 'Only school admins can delete terms.')}>Delete</Button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
              {year.terms.length > 0 && (
                <div className="p-3 border-t border-neutral-100 bg-neutral-50">
                  <Button variant="secondary" size="sm" onClick={() => openTerm(year, null)}>Add term</Button>
                </div>
              )}
            </Card>
          ))}
        </div>
      </LoadState>

      <Dialog
        open={!!yearForm}
        onOpenChange={open => !open && setYearForm(null)}
        title={yearForm?.id ? 'Edit academic year' : 'Add academic year'}
        description="Years cannot overlap. Terms must fall within their year."
        footer={<><Button variant="ghost" onClick={() => setYearForm(null)}>Cancel</Button><Button loading={saving} type="submit" form="year-form">Save</Button></>}
      >
        {yearForm && (
          <form id="year-form" onSubmit={saveYear} noValidate className="space-y-4">
            <FormError message={errors.form} />
            <Input label="Name" id="year-name" required value={yearForm.name} onChange={e => setYearForm({ ...yearForm, name: e.target.value })} error={errors.name} placeholder="2026" />
            <div className="grid sm:grid-cols-2 gap-4">
              <Input label="Starts" id="year-start" type="date" required value={yearForm.startDate} onChange={e => setYearForm({ ...yearForm, startDate: e.target.value })} error={errors.startDate} />
              <Input label="Ends" id="year-end" type="date" required value={yearForm.endDate} onChange={e => setYearForm({ ...yearForm, endDate: e.target.value })} error={errors.endDate} />
            </div>
            {!yearForm.id && <CheckboxField label="Make this the current year" checked={yearForm.isCurrent} onChange={v => setYearForm({ ...yearForm, isCurrent: v })} />}
          </form>
        )}
      </Dialog>

      <Dialog
        open={!!termForm}
        onOpenChange={open => !open && setTermForm(null)}
        title={termForm?.id ? 'Edit term' : `Add term to ${termForm?.yearName ?? ''}`}
        description="Terms of a year cannot overlap."
        footer={<><Button variant="ghost" onClick={() => setTermForm(null)}>Cancel</Button><Button loading={saving} type="submit" form="term-form">Save</Button></>}
      >
        {termForm && (
          <form id="term-form" onSubmit={saveTerm} noValidate className="space-y-4">
            <FormError message={errors.form} />
            <div className="grid sm:grid-cols-3 gap-4">
              <Input label="Name" id="term-name" required containerClassName="sm:col-span-2" value={termForm.name} onChange={e => setTermForm({ ...termForm, name: e.target.value })} error={errors.name} />
              <Input label="Number" id="term-number" type="number" min="1" max="6" required value={termForm.termNumber} onChange={e => setTermForm({ ...termForm, termNumber: e.target.value })} error={errors.termNumber} />
            </div>
            <div className="grid sm:grid-cols-2 gap-4">
              <Input label="Starts" id="term-start" type="date" required value={termForm.startDate} onChange={e => setTermForm({ ...termForm, startDate: e.target.value })} error={errors.startDate} />
              <Input label="Ends" id="term-end" type="date" required value={termForm.endDate} onChange={e => setTermForm({ ...termForm, endDate: e.target.value })} error={errors.endDate} />
            </div>
            {!termForm.id && <CheckboxField label="Make this the current term" checked={termForm.isCurrent} onChange={v => setTermForm({ ...termForm, isCurrent: v })} />}
          </form>
        )}
      </Dialog>
    </AppShell>
  );
}
