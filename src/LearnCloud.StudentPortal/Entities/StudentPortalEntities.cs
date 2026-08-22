using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.StudentPortal.Entities;

// School-level setting controlling whether students may see fee information at all
public class StudentPortalSettings : TenantOwnedEntity
{
    public bool AllowStudentsViewFees { get; set; } = false; // default false - school must opt-in
    public bool AllowStudentsViewGuardianContacts { get; set; } = false;
    public bool AllowStudentsSubmitAssignments { get; set; } = true;
    public bool AllowStudentsViewAttendance { get; set; } = true;
    public bool AllowStudentsViewResults { get; set; } = true;
    public bool AllowStudentsViewTimetable { get; set; } = true;
    public bool AllowStudentsMessageTeacher { get; set; } = false;
    public string? WelcomeMessage { get; set; }
}

// Extended Student with User link for login (in real app, this column exists in Students table)
public class StudentUserLink
{
    // For V1 we reuse Student.UserId nullable column - this class is just for documentation
    // Student table already has UserId? In previous modules we had StaffProfile.UserId, Guardian.UserId, but Student did not. We add it via migration:
    // ALTER TABLE students ADD COLUMN user_id BIGINT UNSIGNED NULL, ADD FOREIGN KEY (user_id) REFERENCES users(id);
}

// For assignment submission by student
public class StudentAssignmentSubmission : TenantOwnedEntity
{
    public long AssignmentId { get; set; }
    public long StudentId { get; set; }
    public string Status { get; set; } = "pending"; // pending, submitted, late, graded
    public DateTime? SubmittedAt { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? Note { get; set; }
    public string? TeacherFeedback { get; set; }
    public decimal? Score { get; set; }
}
