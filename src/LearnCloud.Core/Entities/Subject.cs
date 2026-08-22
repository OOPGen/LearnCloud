using LearnCloud.Domain.Entities;
using LearnCloud.MultiTenancy.Entities;

namespace LearnCloud.Core.Entities;

// Phase 1: Subjects - independent, no dependencies, starter list accept/edit
// C2 CLEANUP: Subject and Grade now canonical in LearnCloud.Domain.Entities, no longer duplicated here.
// GradeSubject links Grade and Subject for academic year.

public class GradeSubject : TenantOwnedEntity
{
    public long GradeId { get; set; }
    public Grade Grade { get; set; } = null!;
    public long SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public long AcademicYearId { get; set; }
    public bool IsCompulsory { get; set; } = true;
}
