#!/usr/bin/env node
// Post-deploy smoke test for LearnCloud.
//
//   node scripts/smoke-test.mjs <base-url>          read-only checks, safe for production
//   node scripts/smoke-test.mjs <base-url> --full   also registers two throwaway schools and
//                                                   runs the Subjects, school records and
//                                                   tenant isolation flow. STAGING ONLY: it
//                                                   writes data.
//
// <base-url> is either the web app (its Cloudflare Worker proxies /api) or the API itself.
// Requires Node 18+. Exits non-zero when any check fails.

const [, , rawBase, ...flags] = process.argv;
if (!rawBase) {
  console.error('Usage: node scripts/smoke-test.mjs <base-url> [--full]');
  process.exit(2);
}
const base = rawBase.replace(/\/+$/, '');
const full = flags.includes('--full');
const results = [];

function check(name, ok, detail = '') {
  results.push(ok);
  console.log(`${ok ? 'PASS' : 'FAIL'}  ${name}${detail ? `  (${detail})` : ''}`);
}

async function call(method, path, { token, body, cookie, headers = {} } = {}) {
  const res = await fetch(base + path, {
    method,
    redirect: 'manual',
    headers: {
      ...(body ? { 'Content-Type': 'application/json' } : {}),
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(cookie ? { Cookie: cookie } : {}),
      ...headers,
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  const text = await res.text();
  let json = null;
  try { json = text ? JSON.parse(text) : null; } catch { /* not JSON */ }
  return { status: res.status, json, setCookies: res.headers.getSetCookie?.() ?? [] };
}

// Read-only checks
const ready = await call('GET', '/api/health');
check('API reachable and database ready', ready.status === 200 && ready.json?.status === 'ready', `HTTP ${ready.status}`);

const anonymous = await call('GET', '/api/academic/subjects');
check('anonymous access to school data is refused', anonymous.status === 401, `HTTP ${anonymous.status}`);

const anonymousStudents = await call('GET', '/api/students');
check('anonymous access to student records is refused', anonymousStudents.status === 401, `HTTP ${anonymousStudents.status}`);

const badLogin = await call('POST', '/api/auth/login', { body: { email: 'nobody@example.invalid', password: 'wrong-password', tenantSlug: null } });
check('invalid credentials are rejected', badLogin.status === 401 || badLogin.status === 429, `HTTP ${badLogin.status}`);

if (full) {
  const run = Date.now().toString(36);
  const password = 'Sm0ke!Test-Passw0rd';

  async function school(tag) {
    const slug = `smoke${tag}${run}`;
    const email = `admin@${slug}.test`;
    const reg = await call('POST', '/api/auth/register-tenant', {
      body: {
        schoolName: `Smoke Test ${tag.toUpperCase()}`, slug, city: 'Bulawayo', contactEmail: email, contactPhone: '+263771000000',
        learnerCountBand: '150-300', adminFullName: 'Smoke Test Admin', adminEmail: email, adminPhone: '+263771000000',
        password, confirmPassword: password,
      },
    });
    check(`register school ${tag}`, reg.status === 200, `HTTP ${reg.status}`);
    const login = await call('POST', '/api/auth/login', { body: { email, password, tenantSlug: slug } });
    const refreshCookie = login.setCookies.find(c => c.startsWith('__Host-refresh_token='));
    check(`sign in to school ${tag}`, login.status === 200 && !!login.json?.accessToken, `HTTP ${login.status}`);
    check(`school ${tag} refresh cookie is __Host-, HttpOnly, Secure, SameSite=Strict`,
      !!refreshCookie && /httponly/i.test(refreshCookie) && /secure/i.test(refreshCookie) && /samesite=strict/i.test(refreshCookie));
    return { slug, token: login.json?.accessToken, cookie: refreshCookie?.split(';')[0] };
  }

  const a = await school('a');
  const b = await school('b');

  const created = await call('POST', '/api/academic/subjects', { token: a.token, body: { name: 'Smoke Mathematics', code: 'SMOKE', description: null, isCore: true, department: 'Smoke' } });
  check('school A creates a subject', created.status === 201, `HTTP ${created.status}`);
  const id = created.json?.id;

  const listA = await call('GET', '/api/academic/subjects', { token: a.token });
  check('school A sees its subject', listA.status === 200 && listA.json?.items?.some(s => s.id === id));

  const listB = await call('GET', '/api/academic/subjects', { token: b.token });
  check('school B does not see it', listB.status === 200 && !listB.json?.items?.some(s => s.id === id));
  check('school B cannot read it by id', (await call('GET', `/api/academic/subjects/${id}`, { token: b.token })).status === 404);
  check('school B cannot delete it', (await call('DELETE', `/api/academic/subjects/${id}`, { token: b.token })).status === 404);

  const refreshed = await call('POST', '/api/auth/refresh', { cookie: a.cookie, body: {}, headers: { 'X-Requested-With': 'XMLHttpRequest' } });
  check('cookie refresh issues a new access token', refreshed.status === 200 && !!refreshed.json?.accessToken, `HTTP ${refreshed.status}`);

  const invalid = await call('POST', '/api/auth/register-tenant', { body: { schoolName: 'x', slug: 'Not A Slug!', password: 'weak', confirmPassword: 'weak' } });
  check('invalid registration is rejected by validation', invalid.status === 422, `HTTP ${invalid.status}`);

  const deleted = await call('DELETE', `/api/academic/subjects/${id}`, { token: a.token });
  check('school A deletes its subject', deleted.status === 204, `HTTP ${deleted.status}`);

  // School records: calendar, class, student with guardian, and isolation of the student.
  const y = new Date().getUTCFullYear();
  const year = await call('POST', '/api/academic/years', { token: a.token, body: { name: `Smoke ${y}`, startDate: `${y}-01-01`, endDate: `${y}-12-31`, isCurrent: true } });
  const term = await call('POST', `/api/academic/years/${year.json?.id}/terms`, { token: a.token, body: { name: 'Term 1', termNumber: 1, startDate: `${y}-01-01`, endDate: `${y}-12-31`, isCurrent: true } });
  const grade = await call('POST', '/api/academic/grades', { token: a.token, body: { academicYearId: year.json?.id, name: 'Smoke Form 1', code: 'SMK1', levelOrder: 1 } });
  const stream = await call('POST', `/api/academic/grades/${grade.json?.id}/streams`, { token: a.token, body: { name: 'Blue', capacity: 30 } });
  check('school A sets up an academic year, term, grade and stream',
    [year, term, grade, stream].every(r => r.status === 201), [year, term, grade, stream].map(r => r.status).join(','));

  const student = await call('POST', '/api/students', {
    token: a.token,
    body: {
      firstName: 'Smoke', lastName: 'Learner', gender: 'female', dob: '2014-02-03', streamId: stream.json?.id,
      guardian: { newGuardian: { firstName: 'Smoke', lastName: 'Parent', phone: '+263771234567' }, relationshipType: 'mother', isPrimaryContact: true, isBillingContact: true, isEmergencyContact: true, canPickup: true },
    },
  });
  check('school A enrols a student with a guardian',
    student.status === 201 && student.json?.currentPlacement?.streamId === stream.json?.id && student.json?.guardians?.length === 1, `HTTP ${student.status}`);
  const studentId = student.json?.id;
  const guardianId = student.json?.guardians?.[0]?.guardianId;

  const studentsB = await call('GET', '/api/students?pageSize=100', { token: b.token });
  check('school B does not see the student', studentsB.status === 200 && !studentsB.json?.items?.some(s => s.id === studentId));
  check('school B cannot read the student or guardian by id',
    (await call('GET', `/api/students/${studentId}`, { token: b.token })).status === 404 && (await call('GET', `/api/guardians/${guardianId}`, { token: b.token })).status === 404);
  const intoOtherSchool = await call('POST', '/api/students', { token: b.token, body: { firstName: 'Cross', lastName: 'School', streamId: stream.json?.id } });
  check("school B cannot enrol into school A's class", intoOtherSchool.status === 404, `HTTP ${intoOtherSchool.status}`);

  const left = await call('POST', `/api/students/${studentId}/exit`, { token: a.token, body: { reason: 'withdrawn' } });
  check('school A records the student leaving', left.status === 200 && left.json?.currentPlacement === null && left.json?.status === 'inactive', `HTTP ${left.status}`);
  console.log(`\nCreated throwaway schools ${a.slug} and ${b.slug}.`);
}

const failed = results.filter(ok => !ok).length;
console.log(`\n${results.length - failed}/${results.length} checks passed`);
process.exit(failed ? 1 : 0);
