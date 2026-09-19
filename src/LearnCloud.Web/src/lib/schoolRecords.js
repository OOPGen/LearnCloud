import { useCallback } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { ApiError } from './apiClient';

// Shared helpers for the school records pages (academic years, grades, students, guardians).

/** "2026-01-13T00:00:00Z" -> "13 Jan 2026". Dates are calendar dates, shown without time zones. */
export function formatDate(value) {
  if (!value) return '—';
  const [y, m, d] = String(value).slice(0, 10).split('-').map(Number);
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' });
}

/** API date -> value for <input type="date">. */
export const toDateInput = value => (value ? String(value).slice(0, 10) : '');

export const todayInput = () => new Date().toISOString().slice(0, 10);

export const STUDENT_STATUSES = [
  { value: 'active', label: 'Active' },
  { value: 'inactive', label: 'Withdrawn' },
  { value: 'transferred', label: 'Transferred out' },
  { value: 'alumni', label: 'Graduated' },
  { value: 'applicant', label: 'Applicant' },
];

export const EXIT_REASONS = [
  { value: 'withdrawn', label: 'Withdrawn' },
  { value: 'transferred_out', label: 'Transferred to another school' },
  { value: 'graduated', label: 'Graduated' },
];

export const RELATIONSHIPS = ['mother', 'father', 'guardian', 'grandparent', 'sibling', 'aunt', 'uncle', 'other'];

export const GENDERS = [
  { value: 'female', label: 'Female' },
  { value: 'male', label: 'Male' },
  { value: 'other', label: 'Other' },
];

const ENROLMENT_LABELS = {
  enrolled: 'Enrolled', promoted: 'Promoted', repeated: 'Repeated', withdrawn: 'Withdrawn',
  transferred_out: 'Transferred out', graduated: 'Graduated',
  new: 'New admission', transfer: 'Transfer in', continuing: 'Continuing', repeat: 'Repeating', readmission: 'Readmitted',
};

export const label = value => ENROLMENT_LABELS[value] || STUDENT_STATUSES.find(s => s.value === value)?.label || capitalise(value);

export const capitalise = value => (value ? value.charAt(0).toUpperCase() + value.slice(1).replace(/_/g, ' ') : '');

/**
 * Handles API errors common to every page: sends the user to sign in on 401 and returns a
 * readable message otherwise. `forbidden` is the message for 403.
 */
export function useApiErrors() {
  const navigate = useNavigate();
  const location = useLocation();

  return useCallback((error, forbidden = 'Your role does not allow this.') => {
    if (error instanceof ApiError && error.status === 401) {
      navigate('/login', { replace: true, state: { from: location.pathname + location.search } });
      return null;
    }
    if (error instanceof ApiError && error.status === 403) return forbidden;
    // 402 is either a suspended or expired school (the message says what to do) or a module
    // outside the school's plan.
    if (error instanceof ApiError && error.status === 402)
      return error.code === 'account_read_only' ? `Your school is read-only. ${error.message}` : 'Your plan does not include this module.';
    return error?.message || 'Something went wrong. Please try again.';
  }, [navigate, location]);
}

/** Grades of a year flattened to selectable classes: [{ id, label, gradeId, full }]. */
export function classOptions(grades) {
  return grades.flatMap(g => g.streams.map(s => ({
    id: s.id,
    gradeId: g.id,
    label: `${s.displayName} (${s.enrolledCount}/${s.capacity})`,
    full: s.enrolledCount >= s.capacity,
    active: g.isActive,
  })));
}
