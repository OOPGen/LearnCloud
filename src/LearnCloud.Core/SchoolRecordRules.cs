using System.Data.Common;

namespace LearnCloud.Core;

// Role sets for the school records endpoints ([Authorize(Roles = ...)] needs constants).
// Student and guardian records hold children's personal data, so teachers do not get them
// here; teacher portal endpoints are scoped to the teacher's own classes.
public static class SchoolRecordRoles
{
    public const string CalendarReaders = "SCHOOL_ADMIN,HEAD_TEACHER,DEPUTY_HEAD,REGISTRAR,BURSAR,TEACHER";
    public const string CalendarWriters = "SCHOOL_ADMIN,HEAD_TEACHER";
    public const string RecordReaders = "SCHOOL_ADMIN,HEAD_TEACHER,DEPUTY_HEAD,REGISTRAR,BURSAR";
    public const string RecordWriters = "SCHOOL_ADMIN,HEAD_TEACHER,REGISTRAR";
    public const string Administrators = "SCHOOL_ADMIN";
}

// Values stored in student_enrolments and students. Other modules read them: attendance
// registers and fee invoicing select enrolments with is_current = true.
public static class EnrolmentValues
{
    public const string Enrolled = "enrolled";
    public const string Promoted = "promoted";
    public const string Repeated = "repeated";
    public const string Withdrawn = "withdrawn";
    public const string TransferredOut = "transferred_out";
    public const string Graduated = "graduated";

    public const string TypeNew = "new";
    public const string TypeTransfer = "transfer";
    public const string TypeContinuing = "continuing";
    public const string TypeRepeat = "repeat";
    public const string TypeReadmission = "readmission";

    public static readonly string[] AdmissionTypes = { TypeNew, TypeTransfer };
    public static readonly string[] ExitReasons = { Withdrawn, TransferredOut, Graduated };

    public const string StudentActive = "active";
    public static readonly string[] StudentStatuses = { "applicant", StudentActive, "inactive", "alumni", "transferred" };

    /// <summary>The student status that follows leaving for a reason in <see cref="ExitReasons"/>.</summary>
    public static string StudentStatusAfterExit(string reason) => reason switch
    {
        Graduated => "alumni",
        TransferredOut => "transferred",
        _ => "inactive",
    };

    public static readonly string[] Genders = { "female", "male", "other" };
    public static readonly string[] Relationships = { "mother", "father", "guardian", "grandparent", "sibling", "aunt", "uncle", "other" };
}

public static class DatabaseErrors
{
    /// <summary>True when PostgreSQL refused the change because of a unique index (SQLSTATE 23505).</summary>
    public static bool IsUniqueViolation(DbUpdateException ex) => (ex.InnerException as DbException)?.SqlState == "23505";
}
