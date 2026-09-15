# School records

The core records every other module builds on: academic years, terms, grades, streams,
students, enrolments and guardians. API code is in `src/LearnCloud.Core`, database rules in
`src/LearnCloud.Domain/Configurations/SchoolRecordsConfiguration.cs`, pages in
`src/LearnCloud.Web/src/pages`.

## Model

```
AcademicYear ──< Term
     │
     └──< Grade ──< ClassStream          a class is a grade's stream: "Form 1 Blue"

Student ──< StudentEnrolment >── AcademicYear, Term, Grade, ClassStream
   │
   └──< GuardianStudentLink >── Guardian
```

- **Academic year:** the name is unique within the school, and years cannot overlap. At
  most one year is current.
- **Term:** falls within its year, and a year's terms cannot overlap. The term number is
  unique within the year. At most one term is current, and it always belongs to the
  current year: making a term current also makes its year current.
- **Grade:** belongs to one academic year, with a code that is unique within that year.
  `LevelOrder` ranks grades from youngest to oldest. An inactive grade takes no
  enrolments.
- **Stream:** the name is unique within its grade. `Capacity` caps the current
  enrolments.
- **Student:** the student number is unique within the school. When none is given it is
  generated as `<admission year>-<sequence>`, for example `2026-0001`.
- **Guardian:** can be linked to several students. Each student has at most one primary
  contact.

## Enrolments (read this before querying students from another module)

- A student has **at most one enrolment with `IsCurrent = true`**, which is the class they
  are in now. The database enforces this (`uq_student_enrolments_one_current`).
- **Choosing a class for a module:** attendance registers, invoicing and messaging should
  select through `student_enrolments` with `is_current = true`.
- **Placement on the student row:** `Student.GradeId`, `StreamId` and `AcademicYearId`
  mirror the current enrolment. After the student leaves they keep the last placement, and
  `CurrentEnrolmentId` is null.

| Action | Endpoint | Effect |
|---|---|---|
| Admit | `POST /api/students` | Student plus a current enrolment (`new` or `transfer`), optionally a first guardian. |
| Change class, same year | `POST /api/students/{id}/enrolments` | Closes the current enrolment (exit date, status stays `enrolled`) and adds a `continuing` one. |
| Move to a later year | same | Closes the old enrolment as `promoted` when the new grade's level is higher, otherwise `repeated`, and adds a `continuing` or `repeat` enrolment. Earlier years are refused. |
| Leave | `POST /api/students/{id}/exit` | Closes the enrolment as `withdrawn`, `transferred_out` or `graduated`. The student becomes `inactive`, `transferred` or `alumni`. |
| Readmit | `POST /api/students/{id}/enrolments` with no current enrolment | Adds a `readmission` enrolment; the student is `active` again. |

When no term is given, the enrolment is filed under the first of these that exists:
1. the term containing the enrolment date;
2. the year's current term;
3. the next term that has not ended;
4. the year's last term.

## Isolation

- **HTTP:** every query filters by school. Another school's ids answer 404.
- **Database:** references between these tables include `tenant_id`, and each referenced
  table has a unique `(tenant_id, id)` key. PostgreSQL itself refuses a row that points at
  another school's row, whatever the application code does.

## Endpoints and roles

| Area | Read | Write | Delete |
|---|---|---|---|
| `/api/academic/years`, `/terms`, `/current` | admin, head, deputy, registrar, bursar, teacher | admin, head | admin |
| `/api/academic/grades`, `/streams` | same as above | admin, head | admin |
| `/api/students` (incl. `/export`, `/enrolments`, `/exit`, `/guardians`) | admin, head, deputy, registrar, bursar | admin, head, registrar | admin |
| `/api/guardians` | admin, head, deputy, registrar, bursar | admin, head, registrar | admin |

- **Teachers:** no access to student or guardian records here. Teacher portal endpoints are
  scoped to the teacher's own classes.
- **Deletes:** all soft.
  - Years, terms, grades and streams that enrolments refer to cannot be deleted. Mark a
    grade inactive instead.
  - Deleting a student also removes their enrolments and guardian links. It is meant only
    for records created by mistake; use "leave" for students who left.
- **Errors:**
  - validation: 422, with field errors;
  - not found: 404;
  - duplicates: 409;
  - other rule violations (overlaps, capacity, ordering): 400, with a readable `detail`.
- **CSV export:** quotes every field and prefixes values starting with `=`, `+`, `-` or `@`
  with an apostrophe, so spreadsheets do not execute them.

## Setup wizard

Steps 3 to 5 (academic year, terms, classes) create these records through the same
services, and every step's data is validated before it is saved. Saving the terms step
again replaces the year's terms by term number. A term that enrolments refer to cannot be
removed.
